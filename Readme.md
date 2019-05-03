About:
======
Android fastboot stores each file it receives in RAM before flashing it to your Android device.  
For this reason, large partitions (such as the /system partition) must be splitted using the "compressed ext4 file system sparse image format", this format is defined by the AOSP (Android Open Source Project) and was designed for the single purpose of flashing a large partition.  

If you tried to flash system.img from backup and received the "Invalid sparse file format at header magi" error, you have come to the right place.  

The solution:
=============
SparseConverter is a tool that can create / decompress compressed ext4 file system sparse image format (e.g. system.img_sparsechunk1).  

> Note for Motorola phone owners:  
> The factory images from Motorola contains a 128KB motorola header and a 4KB motorola footer, if you decompress those images you may want to remove the header and footer.  
> I had no problem using the standard Android fastboot with my unlocked Moto G and flashing images without the Motorola header / footer.  
> (If you keep the header / footer, then you must use Motorola's fastboot)

Usage Examples:
---------------
`SparseConverter.exe /compress D:\system.img E:\ 256MB`  
( will compress D:\system.img to 256MB sparse files starting from E:\system.img_sparsechunk1 )  
`SparseConverter.exe /decompress E:\system.img_sparsechunk1 D:\system.img`  
( will decompress E:\system.img_sparsechunk1, E:\system.img_sparsechunk2 and etc. to D:\system.img )  

The software may contain bugs and/or limitations that may result in damage to your phone, I take no responsibility for any damage that may occur.  

For additional information about the "compressed ext4 file system sparse image format" see libsparse/sparse_format.h  
