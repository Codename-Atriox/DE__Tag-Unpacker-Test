using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Infinite_module_test
{
    // VERSION 52 // VERSION 52 // VERSION 52 //
    public static class module_structs
    {
        public struct module_data // sizeof = DYNAMIC (sizeof = file.size - mohd.sizeof)
        {
            public module_header module_info;

            public module_file[] files; // FileCount

            public byte[] string_table; // current_offset + tag.NameOffset = string* // StringsSize

            public int[] resource_table; // the function of this array is to index all of the tags that rely on resource things (not blocks), like models i think // ResourceCount

            public block_header[] blocks; // BlockCount

            // skip 00's??
            // padding until next 0x????0000 address


            // local data stuff, no structures for that though, just random data i guess, 
            //public byte[] data;
            public tag[] file_contents;
        }

        public const int module_header_size = 0x50;
        [StructLayout(LayoutKind.Explicit, Size = module_header_size)] public struct module_header 
        {
            [FieldOffset(0x00)] public int     Head;           //  used to determine if this file is actually a module, should be "mohd"
            [FieldOffset(0x04)] public int     Version;        //  48 flight1, 51 flight2 & retail
            [FieldOffset(0x08)] public ulong   ModuleId;       //  randomized between modules, algo unknown
            [FieldOffset(0x10)] public int     FileCount;      //  the total number of tags contained by the module
 
            [FieldOffset(0x14)] public int     ManifestCount;       //  'FFFFFFFF' "Number of tags in the load manifest (0 if the module doesn't have one, see the "Load Manifest" section below)"
            [FieldOffset(0x18)] public int     Manifest_Unk_0x18;   //  'FFFFFFFF' on blank modules, 0 on non blanks, assumedly this the index of the manifest file in the module files array
            [FieldOffset(0x1C)] public int     Manifest_Unk_0x1C;   //  'FFFFFFFF'
   
            [FieldOffset(0x20)] public int     ResourceIndex;   //  "Index of the first resource entry (numFiles - numResources)"
            [FieldOffset(0x24)] public int     StringsSize;     //  total size (in bytes) of the strings table
            [FieldOffset(0x28)] public int     ResourceCount;   //  number of resource files
            [FieldOffset(0x2C)] public int     BlockCount;      //  number of data blocks
  
            [FieldOffset(0x30)] public ulong   BuildVersion;    // this should be the same between each module
            [FieldOffset(0x38)] public ulong   Checksum;        // "Murmur3_x64_128 of the header (set this field to 0 first), file list, resource list, and block list"
            // new with infinite
            [FieldOffset(0x40)] public int     Unk_0x040;       //  0
            [FieldOffset(0x44)] public int     Unk_0x044;       //  0
            [FieldOffset(0x48)] public int     Unk_0x048;       //  2
            [FieldOffset(0x4C)] public int     Unk_0x04C;       //  0
        }

        public const int module_file_size = 0x58;
        [StructLayout(LayoutKind.Explicit, Size = module_file_size)] public struct module_file
        {
                                
            [FieldOffset(0x00)] public byte    ClassGroup;     //  
            [FieldOffset(0x01)] public byte    Flags;          // refer to flag bits below this struct
            [FieldOffset(0x02)] public ushort  BlockCount;     // "The number of blocks that make up the file. Only valid if the HasBlocks flag is set"
            [FieldOffset(0x04)] public uint    BlockIndex;     // "The index of the first block in the file. Only valid if the HasBlocks flag is set"
            [FieldOffset(0x08)] public uint    ResourceIndex;  // "Index of the first resource in the module's resource list that this file owns"
                                
            [FieldOffset(0x0C)] public int     ClassId;        // this is the tag group, should be a string right?
                                
            [FieldOffset(0x10)] public uint    DataOffset;     // for now just read as a long // wow we were not infact reading this a long
            [FieldOffset(0x14)] public uint    Unk_0x14;       // we will now need to double check each file to make sure if this number is ever anything // its used in the very big files

            [FieldOffset(0x18)] public int     TotalCompressedSize;    // "The total size of compressed data."
            [FieldOffset(0x1C)] public int     TotalUncompressedSize;  // "The total size of the data after it is uncompressed. If this is 0, then the file is empty."
                                
            [FieldOffset(0x20)] public int     GlobalTagId;   // this is the murmur3 hash; autogenerate from tag path
                                
            [FieldOffset(0x24)] public int     UncompressedHeaderSize;      
            [FieldOffset(0x28)] public int     UncompressedTagDataSize;     
            [FieldOffset(0x2C)] public int     UncompressedResourceDataSize;
            [FieldOffset(0x30)] public int     UncompressedActualResourceDataSize;   // used with bitmaps, and likely other tags idk

            [FieldOffset(0x34)] public byte    HeaderAlignment;             // Power of 2 to align the header buffer to (e.g. 4 = align to a multiple of 16 bytes).
            [FieldOffset(0x35)] public byte    TagDataAlightment;           // Power of 2 to align the tag data buffer to.
            [FieldOffset(0x36)] public byte    ResourceDataAligment;        // Power of 2 to align the resource data buffer to.
            [FieldOffset(0x37)] public byte    ActualResourceDataAligment;  // Power of 2 to align the actual resource data buffer to.

            [FieldOffset(0x38)] public uint    NameOffset;       // 
            [FieldOffset(0x3C)] public int     ParentIndex;      // "Used with resources to point back to the parent file. -1 = none"
            [FieldOffset(0x40)] public ulong   AssetChecksum;    // "Murmur3_x64_128 hash of (what appears to be) the original file that this file was built from. This is not always the same thing as the file stored in the module. Only verified if the HasBlocks flag is not set."
            [FieldOffset(0x48)] public ulong   AssetId;          // "The asset ID (-1 if not a tag)." maybe other files reference this through its id?

            [FieldOffset(0x50)] public uint    ResourceCount;  // "Number of resources this file owns"
            [FieldOffset(0x54)] public int     Unk_0x54;       // so far has just been 0, may relate to hd files?
        }
        // // // // FLAGS // // // // 
        // (these are probably flipped as i was figuring this out straight from the module files)
        // 0000-0001 <- Uses Compression
        // 0000-0010 <- has blocks, which means to read the data across several data blocks, otherwise read straight from data offset
        // 0000-0100 <- is a raw file, meaning it has no tag header

        public const int block_header_size = 0x14;
        [StructLayout(LayoutKind.Explicit, Size = block_header_size)] public struct block_header // sizeof = 0x14
        {
            // these SHOULD be uints, however oodle does not like that, so if we get any issues here blame it on oodle
            // so max decompression size is 2gb, which is unlikely that we'll breach that so this is ok for now
            [FieldOffset(0x00)] public int    CompressedOffset;  
            [FieldOffset(0x04)] public int    CompressedSize;    
            [FieldOffset(0x08)] public int    UncompressedOffset;
            [FieldOffset(0x0C)] public int    UncompressedSize;  
            [FieldOffset(0x10)] public int    Compressed;        
        }




        // VERSION 27 // VERSION 27 // VERSION 27 //
        // an interesting thing to note is that version 27 was the version of halo 5 forge
        // it seems someone on the engine team failed to update this number lol, because this struct has certainly changed since
        // or at least its child structs definitely changed
        /////////////// //////////////////////////////////////////////
        // TAG STUFF // //////////////////////////////////////////////
        /////////////// //////////////////////////////////////////////
        //    _____________         ___            ____________
        //   |             |       /   \          /            \ 
        //   |_____   _____|      /     \        /    ______    \ 
        //        |   |          /  /\   \      /    /      \____\ 
        //        |   |         /  /__\   \     |   /     ________
        //        |   |        /   ____    \    |   \     |___    |
        //        |   |       /   /     \   \   \    \______/    /
        //        |   |      /   /       \   \   \              /
        //        |___|     /___/         \___\   \____________/
        //                            
        // currently having this as a class, so that we can just copy pointers to this structure for effiency
        public class tag
        {
            // not nullable because we are not checking if its not null 100 times
            public tag_header header; 
            public tag_dependency[]? dependencies; // header.DependencyCount
            public data_block[]? data_blocks; // header.DataBlockCount
            public tag_def_structure[]? tag_structs; // header.TagStructCount
            public data_reference[]? data_references; // header.DataReferenceCount
            public tag_fixup_reference[]? tag_fixup_references; // header.TagReferenceCount

            //public string_id_reference[] string_id_references; // potentially unused? double check
            public byte[]? local_string_table; // header.StringTableSize
            // also non-nullable so we dont have to check if its null or not
            public zoneset_header zoneset_info;
            public zoneset_instance[]? zonesets; // zoneset_info.ZoneSetCount

            // and non-required stuff i guess
            public byte[]? tag_data; // like the actual values that the tag holds, eg. projectile speed and all those thingos
            public byte[]? tag_resource; // ONLY SEEN TO BE USED IN MAT FILES
            public byte[]? actual_tag_resource; // used in bitmap files

            public byte[]? raw_file_bytes; // used for reading raw files


            //public byte[]? unmapped_header_data; // used for debugging unmapped structures in headers
            public byte[]? header_data; // tag structs require us to store the WHOLE header data so the offsets match
            // we could likely setup something so we only use the unmarked and subtract the 
        }

        public const int tag_header_size = 0x50;
        [StructLayout(LayoutKind.Explicit, Size = tag_header_size)] public struct tag_header
        {
            [FieldOffset(0x00)] public int     Magic; 
            [FieldOffset(0x04)] public uint    Version; 
            [FieldOffset(0x08)] public ulong   Unk_0x08; 
            [FieldOffset(0x10)] public ulong   AssetChecksum;  
            // these cant be uints because of how the code is setup, should probably assert if -1
            [FieldOffset(0x18)] public int     DependencyCount; 
            [FieldOffset(0x1C)] public int     DataBlockCount;  
            [FieldOffset(0x20)] public int     TagStructCount; 
            [FieldOffset(0x24)] public int     DataReferenceCount; 
            [FieldOffset(0x28)] public int     TagReferenceCount; 
                                
            [FieldOffset(0x2C)] public uint    StringTableSize; 
                                
            [FieldOffset(0x30)] public uint    ZoneSetDataSize;    // this is the literal size in bytes
            [FieldOffset(0x34)] public uint    Unk_0x34; // new with infinite, cold be an enum of how to read the alignment bytes
                                
            [FieldOffset(0x38)] public uint    HeaderSize; 
            [FieldOffset(0x3C)] public uint    DataSize; 
            [FieldOffset(0x40)] public uint    ResourceDataSize; 
            [FieldOffset(0x44)] public uint    ActualResoureDataSize;  // also new with infinite
                                
            [FieldOffset(0x48)] public byte    HeaderAlignment; 
            [FieldOffset(0x49)] public byte    TagDataAlightment; 
            [FieldOffset(0x4A)] public byte    ResourceDataAligment; 
            [FieldOffset(0x4B)] public byte    ActualResourceDataAligment; 

            [FieldOffset(0x4C)] public int     Unk_0x4C; 
        }


        public const int tag_dependency_size = 0x18;
        [StructLayout(LayoutKind.Explicit, Size = tag_dependency_size)] public struct tag_dependency // this struct looks just like a regular tag reference
        {
            [FieldOffset(0x00)] public int     GroupTag;
            [FieldOffset(0x04)] public uint    NameOffset;
                                
            [FieldOffset(0x08)] public long    AssetID;
            [FieldOffset(0x10)] public int     GlobalID;
            [FieldOffset(0x14)] public int     Unk_0x14;   // possibly padding?
        }

        public const int data_block_size = 0x10;
        [StructLayout(LayoutKind.Explicit, Size = data_block_size)] public struct data_block
        {
            [FieldOffset(0x00)] public uint    Size;
            [FieldOffset(0x04)] public ushort  Unk_0x04;   // "(0 - 14, probably an enum)", potentially the index of the resource file
                                
            [FieldOffset(0x06)] public ushort  Section;    // "0 = Header, 1 = Tag Data, 2 = Resource Data" 3 would be that actual resource thingo
            [FieldOffset(0x08)] public ulong   Offset;     // "The offset of the start of the data block, relative to the start of its section."
        }

        public const int tag_def_structure_size = 0x20;
        [StructLayout(LayoutKind.Explicit, Size = tag_def_structure_size)] public struct tag_def_structure
        {
            //byte[16] GUID; // 0x00 // ok lets not attempt to use a byte array then
            [FieldOffset(0x00)] public long   GUID_1;
            [FieldOffset(0x08)] public long   GUID_2;
                                
            [FieldOffset(0x10)] public ushort  Type;           // "0 = Main Struct, 1 = Tag Block, 2 = Resource, 3 = Custom"
            [FieldOffset(0x12)] public ushort  Unk_0x12;       // likely padding
                                
            [FieldOffset(0x14)] public int     TargetIndex;    // "For Main Struct and Tag Block structs, the index of the block containing the struct. For Resource structs, this (probably) is the index of the resource. This can be -1 if the tag field doesn't point to anything (null Tag Blocks or Custom structs)."
            [FieldOffset(0x18)] public int     FieldBlock;     // "The index of the data block containing the tag field which refers to this struct. Can be -1 for the Main Struct."
            [FieldOffset(0x1C)] public uint    FieldOffset;    // "The offset of the tag field inside the data block. (Unlike in Halo Online, this points to the tag field, not the pointer inside the tag field. Some tag fields for structs don't even have a pointer.)"
        }
        // further reading:https://github.com/ElDewrito/AusarDocs/blob/master/FileFormats/CachedTag.md#main-struct-type-0


        public const int data_reference_size = 0x14;
        [StructLayout(LayoutKind.Explicit, Size = data_reference_size)] public struct data_reference
        {
            [FieldOffset(0x00)] public int     ParentStructIndex;  // "The index of the tag struct containing the tag field."
            [FieldOffset(0x04)] public int     Unk_0x04; 
                                
            [FieldOffset(0x08)] public int     TargetIndex;        // "The index of the data block containing the referenced data. Can be -1 for null references."
            [FieldOffset(0x0C)] public int     FieldBlock;         // "The index of the data block containing the tag field."
            [FieldOffset(0x10)] public int     FieldOffset;        // "The offset of the tag field inside the data block."
        }

        public const int tag_fixup_reference_size = 0x10;
        [StructLayout(LayoutKind.Explicit, Size = tag_fixup_reference_size)] public struct tag_fixup_reference
        {
            [FieldOffset(0x00)] public int     FieldBlock;     // "The index of the data block containing the tag field."
            [FieldOffset(0x04)] public uint    FieldOffset;    // "The offset of the tag field inside the data block. (Unlike in Halo Online, this points to the tag field, not the handle inside the tag field.)"
                                
            [FieldOffset(0x08)] public uint    NameOffset;     // "The offset of the tag filename within the String Table."
            [FieldOffset(0x0C)] public int     DepdencyIndex;  // "The index of the tag dependency in the Tag Dependency List. Can be -1 for null tag references."
        }

        //public const int string_id_reference_size = 0x08;
        //[StructLayout(LayoutKind.Explicit, Size = string_id_reference_size)] public struct string_id_reference
        //{
        //    [FieldOffset(0x00)] public uint    Unk_0x00;       // "(Flags?)"
        //    [FieldOffset(0x04)] public uint    StringOffset;   // "The offset of the string value within the String Table"
        //}
        
        // zoneset junk

        public const int zoneset_header_size = 0x10;
        [StructLayout(LayoutKind.Explicit, Size = zoneset_header_size)] public struct zoneset_header
        {
            [FieldOffset(0x00)] public int     Unk_0x00;       // could be the version??? this value seems to be the same between all entrants
            [FieldOffset(0x04)] public int     ZonesetCount;   // the total amount of zoneset instances to read
            [FieldOffset(0x08)] public int     Unk_0x08;       // potentially the sum of all the zonests footer counts?
            [FieldOffset(0x0C)] public int     Unk_0x0C;       // potentially the sum of all the zonesets parents?
        }

        public const int zoneset_instance_header_size = 0x10;
        [StructLayout(LayoutKind.Explicit, Size = zoneset_instance_header_size)] public struct zoneset_instance_header
        {
            [FieldOffset(0x00)] public int     StringID;       // the name of the zoneset that this tag should belong to?
            [FieldOffset(0x04)] public int     TagCount;       // "Number of tags to load for the zoneset"
            [FieldOffset(0x08)] public int     ParentCount;    // the count of 4 byte items that come after voth the tags, and footer tags, potentially parent zoneset??
            [FieldOffset(0x0C)] public int     FooterCount;    // seems to be the same struct as tagCount
        }

        public struct zoneset_instance
        {
            public zoneset_instance_header header;
            public zoneset_tag[] zonset_tags;
            public zoneset_tag[] zonset_footer_tags;
            public int[] zonset_parents;
        }

        public const int zoneset_tag_size = 0x08;
        [StructLayout(LayoutKind.Explicit, Size = zoneset_tag_size)] public struct zoneset_tag
        {
            [FieldOffset(0x00)] public uint    GlobalID;   // id of the tag? why are we referencing other tags???
            [FieldOffset(0x04)] public int     StringID;   // name of the zoneset (as a hash?)
        }



        // MISC STRUCTS, mainly for non tag files
        //   ______         ______   _____ 
        //   |     \       /     |   |   |   
        //   |      \     /      |   |   |   
        //   |       \   /       |   |   |   
        //   |   |\   \ /   /|   |   |   |   
        //   |   | \   V   / |   |   |   |   
        //   |   |  \     /  |   |   |   |   
        //   |   |   \   /   |   |   |   |   
        //   |   |    \ /    |   |   |   |   
        //   |___|     V     |___|   |___|   

        public struct runtimeload_metadata_tag_instance
        {
            runtimeload_metadata_tag header;    // the header that contains all the information about this metadata tag
            uint[] dependencies;                // the list of tags that this tag relies on
        }

        // to read the manifest file, first read the 4 bytes, this is the tag count, it should probably match the count of tags in the module (specifically the tags, not just any files)
        public const int runtimeload_metadata_size = 0x1A;
        [StructLayout(LayoutKind.Explicit, Size = runtimeload_metadata_size)]
        public struct runtimeload_metadata_tag
        {
            [FieldOffset(0x00)] public uint TagCount;           // the data format is broken it seems, each instance has allocated 4 bytes for the tag count, however those bytes are only ever set at the first 4 bytes in the file
            [FieldOffset(0x04)] public uint CachedTagID;        // tag id referencing a tag inside this module, might also be allowed to index tags not in this module
            
            [FieldOffset(0x08)] public int ZonesetCount;        // unsure about this one, but this is likely what this value is for
            [FieldOffset(0x0C)] public uint ZonesetStringID;    // name of the zoneset that this tag will belong to
            [FieldOffset(0x10)] public int Unk_0x10;            // only seen as -1 so far
            [FieldOffset(0x14)] public short Unk_0x14;          // only seen as 0 so far

            [FieldOffset(0x16)] public short DependentTagCount; // this tells us how many TagIDs to read for the tags that this tag depends on
        }





    }

}
