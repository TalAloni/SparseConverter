using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace SparseConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            args = CommandLineParser.GetCommandLineArgsIgnoreEscape();
            if (args.Length < 2)
            {
                PrintHelp();
                return;
            }

            string inputPath = args[1];
            if (String.Equals(args[0], "/compress", StringComparison.InvariantCultureIgnoreCase))
            {
                if (args.Length != 4)
                {
                    PrintHelp();
                    return;
                }
                string outputPath = args[2];
                long maxSparseSize = ParseStandardSizeString(args[3]);
                int minSparseSize = SparseHeader.Length + 3 * ChunkHeader.Length + SparseCompressionHelper.BlockSize;
                if (maxSparseSize >= minSparseSize)
                {
                    Compress(inputPath, outputPath, maxSparseSize);
                }
                else
                {
                    PrintHelp();
                    return;
                }
            }
            else if (String.Equals(args[0], "/decompress", StringComparison.InvariantCultureIgnoreCase))
            {
                if (args.Length != 3)
                {
                    PrintHelp();
                    return;
                }
                string outputPath = args[2];
                List<string> sparseList = GetSparseList(inputPath);
                Decompress(sparseList, outputPath);
            }
            else if (String.Equals(args[0], "/stats", StringComparison.InvariantCultureIgnoreCase))
            {
                PrintSparseImageStatistics(inputPath);
            }
            else
            {
                PrintHelp();
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("SparseConverter v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("Author: Tal Aloni (tal.aloni.il@gmail.com)");
            Console.WriteLine("About:");
            Console.WriteLine("This software is designed to create / decompress compressed ext4 file system");
            Console.WriteLine("sparse image format, which is defined by AOSP.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("SparseConverter /compress <image-path> <output-folder> <max-sparse-size>");
            Console.WriteLine("SparseConverter /decompress <first-sparse-path> <output-image-path>");
            Console.WriteLine("SparseConverter /stats <sparse-path>");
        }

        private static void Compress(string inputPath, string outputPath, long maxSparseSize)
        {
            FileStream input;
            try
            {
                input = File.Open(inputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (IOException)
            {
                Console.WriteLine("Cannot open " + inputPath);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Cannot open {0} - Access Denied", inputPath);
                return;
            }

            if (input.Length % SparseCompressionHelper.BlockSize > 0)
            {
                Console.WriteLine("Image size is not a multiple of {0} bytes", SparseCompressionHelper.BlockSize);
                return;
            }

            if (!Directory.Exists(outputPath))
            {
                Console.WriteLine("Output directory does not exist");
                return;
            }

            if (!outputPath.EndsWith(":") && !outputPath.EndsWith(@"\"))
            {
                outputPath += @"\";
            }

            string imageFileName = Path.GetFileName(inputPath);
            string prefix = outputPath + imageFileName + "_sparsechunk";
            int sparseIndex = 1;
            while(true)
            {
                string sparsePath = prefix + sparseIndex.ToString();
                FileStream output;
                try
                {
                    output = File.Open(sparsePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                    output.SetLength(0);
                }
                catch (IOException)
                {
                    Console.WriteLine("Cannot open " + sparsePath);
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("Cannot open {0} - Access Denied", sparsePath);
                    return;
                }

                Console.WriteLine("Writing: {0}", sparsePath);
                bool complete = SparseCompressionHelper.WriteCompressedSparse(input, output, maxSparseSize);
                if (complete)
                {
                    break;
                }
                sparseIndex++;
            }
            input.Close();
        }

        private static List<string> GetSparseList(string inputPath)
        {
            List<string> sparseList = new List<string>();
            sparseList.Add(inputPath);
            if (inputPath.EndsWith("0") || inputPath.EndsWith("1"))
            {
                int firstSparseIndex = Convert.ToInt32(inputPath.Substring(inputPath.Length - 1));
                string prefix = inputPath.Substring(0, inputPath.Length - 1);
                int sparseIndex = firstSparseIndex + 1;
                string sparsePath = prefix + sparseIndex.ToString();
                while (File.Exists(sparsePath))
                {
                    sparseList.Add(sparsePath);
                    sparseIndex++;
                    sparsePath = prefix + sparseIndex.ToString();
                }
            }

            return sparseList;
        }

        private static void Decompress(List<string> sparseList, string outputPath)
        {
            FileStream output;
            try
            {
                output = File.Open(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                output.SetLength(0);
            }
            catch (IOException)
            {
                Console.WriteLine("Cannot open " + outputPath);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Cannot open {0} - Access Denied", outputPath);
                return;
            }

            Console.WriteLine("Output: {0}", outputPath);
            foreach (string sparsePath in sparseList)
            {
                FileStream input;
                try
                {
                    input = File.Open(sparsePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                catch (IOException)
                {
                    Console.WriteLine("Cannot open " + sparsePath);
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("Cannot open {0} - Access Denied", sparsePath);
                    return;
                }
                Console.WriteLine("Processing: {0}", sparsePath);

                try
                {
                    SparseDecompressionHelper.DecompressSparse(input, output);
                }
                catch(ArgumentException)
                {
                    Console.WriteLine("Invalid Sparse Image Format");
                    return;
                }
            }
            output.Close();
        }

        private static void PrintSparseImageStatistics(string path)
        {
            FileStream stream;
            try
            {
                stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (IOException)
            {
                Console.WriteLine("Cannot open " + path);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Cannot open {0} - Access Denied", path);
                return;
            }

            SparseHeader sparseHeader = SparseHeader.Read(stream);
            if (sparseHeader == null)
            {
                Console.WriteLine("Invalid Sparse Image Format");
                return;
            }

            Console.WriteLine("Total Blocks: " + sparseHeader.TotalBlocks);
            Console.WriteLine("Total Chunks: " + sparseHeader.TotalChunks);
            long outputSize = 0;
            for(uint index = 0; index < sparseHeader.TotalChunks; index++)
            {
                ChunkHeader chunkHeader = ChunkHeader.Read(stream);
                Console.Write("Chunk type: {0}, size: {1}, total size: {2}", chunkHeader.ChunkType.ToString(), chunkHeader.ChunkSize, chunkHeader.TotalSize);
                int dataLength = (int)(chunkHeader.ChunkSize * sparseHeader.BlockSize);
                switch(chunkHeader.ChunkType)
                {
                    case ChunkType.Raw:
                    {
                        SparseDecompressionHelper.ReadBytes(stream, dataLength);
                        Console.WriteLine();
                        outputSize += dataLength;
                        break;
                    }
                    case ChunkType.Fill:
                    {
                        byte[] fillBytes = SparseDecompressionHelper.ReadBytes(stream, 4);
                        uint fill = LittleEndianConverter.ToUInt32(fillBytes, 0);
                        Console.WriteLine(", value: 0x{0}", fill.ToString("X8"));
                        outputSize += dataLength;
                        break;
                    }
                    case ChunkType.DontCare:
                    {
                        Console.WriteLine();
                        break;
                    }
                    case ChunkType.CRC:
                    {
                        byte[] crcBytes = SparseDecompressionHelper.ReadBytes(stream, 4);
                        uint crc = LittleEndianConverter.ToUInt32(crcBytes, 0);
                        Console.WriteLine(", value: 0x{0}", crc.ToString("X8"));
                        break;
                    }
                    default:
                    {
                        Console.WriteLine();
                        Console.WriteLine("Error: Invalid Chunk Type");
                        return;
                    }
                }
            }
            stream.Close();
            Console.WriteLine("Output size: {0}", outputSize);
        }

        public static long ParseStandardSizeString(string value)
        {
            if (value.ToUpper().EndsWith("TB"))
            {
                return (long)1024 * 1024 * 1024 * 1024 * Conversion.ToInt64(value.Substring(0, value.Length - 2), -1);
            }
            else if (value.ToUpper().EndsWith("GB"))
            {
                return 1024 * 1024 * 1024 * Conversion.ToInt64(value.Substring(0, value.Length - 2), -1);
            }
            else if (value.ToUpper().EndsWith("MB"))
            {
                return 1024 * 1024 * Conversion.ToInt64(value.Substring(0, value.Length - 2), -1);
            }
            else if (value.ToUpper().EndsWith("KB"))
            {
                return 1024 * Conversion.ToInt64(value.Substring(0, value.Length - 2), -1);
            }
            if (value.ToUpper().EndsWith("B"))
            {
                return Conversion.ToInt64(value.Substring(0, value.Length - 1), -1);
            }
            else
            {
                return Conversion.ToInt64(value, -1);
            }
        }
    }
}
