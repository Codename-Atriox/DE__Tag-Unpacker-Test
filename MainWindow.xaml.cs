using OodleSharp;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using static Infinite_module_test.module_structs;
using static System.Net.Mime.MediaTypeNames;
using static Infinite_module_test.code_utils;

namespace Infinite_module_test
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        

        public FileStream module_reader;
        public T read_and_convert_to<T>(int read_length)
        {
            byte[] bytes = new byte[read_length];
            module_reader.Read(bytes, 0, read_length);
            return KindaSafe_SuperCast<T>(bytes);
        }
        public T[] struct_array_assign_bytes<T>(byte[] data, ref ulong ReadPosition, int count, int struct_size)
        {
            T[] output = new T[count];
            for (int i = 0; i < count; i++)
            {
                output[i] = KindaSafe_SuperCast<T>(data, ReadPosition);
                ReadPosition += (ulong)struct_size;
            }
            return output;
        }

        module_data module;
        public MainWindow()
        {
            InitializeComponent();

            // this one is a basic small test, with only a few tags
            //string path = "D:\\Programs\\Steam\\steamapps\\common\\Halo Infinite\\deploy\\pc\\levels\\multi\\academy_tutorial\\academy_tutorial-rtx-new.module";

            // another basic module but it uses resources a little differently
            //string path = "D:\\Programs\\Steam\\steamapps\\common\\Halo Infinite\\deploy\\any\\globals\\forge-rtx-new.module";


            //string path = "D:\\Programs\\Steam\\steamapps\\common\\Halo Infinite\\deploy\\any\\globals\\common-rtx-new.module";
            //string path = "D:\\Programs\\Steam\\steamapps\\common\\Halo Infinite\\deploy\\pc\\globals\\forge-rtx-new.module";
            //string path = "D:\\Programs\\Steam\\steamapps\\common\\Halo Infinite\\deploy\\ds\\levels\\ui\\mainmenu\\mainmenu-rtx-new.module";


            string path = "D:\\Programs\\Steam\\steamapps\\common\\Halo Infinite\\deploy\\pc\\levels\\multi\\ctf_bazaar\\ctf_bazaar-rtx-new.module";



            // and then he said "it's module'n time"
            module = new();
            using (module_reader = new FileStream(path,FileMode.Open, FileAccess.Read))
            {
                // read module header
                module.module_info = read_and_convert_to<module_header>(module_header_size);

                // read module file headers
                module.files = new module_file[module.module_info.FileCount];
                for (int i = 0; i < module.files.Length; i++)
                    module.files[i] = read_and_convert_to<module_file>(module_file_size);

                // read the string table
                module.string_table = new byte[module.module_info.StringsSize];
                module_reader.Read(module.string_table, 0, module.module_info.StringsSize);

                // read the resource indicies?
                module.resource_table = new int[module.module_info.ResourceCount];
                for (int i = 0; i < module.resource_table.Length; i++)
                    module.resource_table[i] = read_and_convert_to<int>(4); // we should also fix this one too

                // read the data blocks
                module.blocks = new block_header[module.module_info.BlockCount];
                for (int i = 0; i < module.blocks.Length; i++)
                    module.blocks[i] = read_and_convert_to<block_header>(block_header_size);


                // now to read the compressed data junk

                // align accordingly to 0x?????000 padding to read data
                long aligned_address = (module_reader.Position / 0x1000 + 1) * 0x1000;
                //module_reader.Seek(aligned_address, SeekOrigin.Begin);

                // setup a new array for the actual tags
                module.file_contents = new tag[module.files.Length];
                for (int i = 0; i < module.file_contents.Length; i++)
                    module.file_contents[i] = new();

                for (int i = 0; i < module.files.Length; i++)
                {
                    // read the flags to determine how to process this file
                    bool using_compression       = (module.files[i].Flags & 0b00000001) > 0; // pretty sure this is true if reading_seperate_blocks is also true, confirmation needed
                    bool reading_separate_blocks = (module.files[i].Flags & 0b00000010) > 0;
                    bool reading_raw_file        = (module.files[i].Flags & 0b00000100) > 0;

                    byte[] decompressed_data = new byte[module.files[i].TotalUncompressedSize];
                    long data_Address = aligned_address + module.files[i].DataOffset;

                    if (reading_separate_blocks)
                    {
                        for (int b = 0; b < module.files[i].BlockCount; b++)
                        {
                            var bloc = module.blocks[module.files[i].BlockIndex + b];
                            byte[] block_bytes;

                            if (bloc.Compressed == 1)
                            {
                                module_reader.Seek(data_Address + bloc.CompressedOffset, SeekOrigin.Begin);

                                byte[] bytes = new byte[bloc.CompressedSize];
                                module_reader.Read(bytes, 0, bytes.Length);
                                block_bytes = Oodle.Decompress(bytes, bytes.Length, bloc.UncompressedSize);
                            }
                            else // uncompressed
                            {
                                module_reader.Seek(data_Address + bloc.UncompressedOffset, SeekOrigin.Begin);

                                block_bytes = new byte[bloc.UncompressedSize];
                                module_reader.Read(block_bytes, 0, block_bytes.Length);
                            }
                            System.Buffer.BlockCopy(block_bytes, 0, decompressed_data, bloc.UncompressedOffset, block_bytes.Length);
                        }
                    }
                    else // is the manifest thingo, aka raw file, read data based off compressed and uncompressed length
                    {
                        module_reader.Seek(data_Address, SeekOrigin.Begin);
                        if (using_compression)
                        {
                            byte[] bytes = new byte[module.files[i].TotalCompressedSize];
                            module_reader.Read(bytes, 0, bytes.Length);
                            decompressed_data = Oodle.Decompress(bytes, bytes.Length, module.files[i].TotalUncompressedSize);
                        }
                        else
                        {
                            module_reader.Read(decompressed_data, 0, module.files[i].TotalUncompressedSize);
                        }
                    }

                    // now we process the data, either into a tag structure, or a raw file structure
                    if (reading_raw_file) // if reading the manifest thingo
                    {
                        module.file_contents[i].raw_file_bytes = decompressed_data;
                        //take_that_mfing_array_and_stick_it_in_my_clipboard_RIGHT_NOW(decompressed_data);
                    }
                    else // reading a regular tag file
                    {
                        // read tag header
                        module.file_contents[i].header = KindaSafe_SuperCast<tag_header>(decompressed_data); // start index not needed as its zero here
                        ulong read_pos = (ulong)tag_header_size;

                        // read tag dependencies
                        module.file_contents[i].dependencies = struct_array_assign_bytes<tag_dependency>(decompressed_data, ref read_pos, module.file_contents[i].header.DependencyCount, tag_dependency_size);

                        // read tag data blocks
                        module.file_contents[i].data_blocks = struct_array_assign_bytes<data_block>(decompressed_data, ref read_pos, module.file_contents[i].header.DataBlockCount, data_block_size);

                        // read tag ref structures
                        module.file_contents[i].tag_structs = struct_array_assign_bytes<tag_def_structure>(decompressed_data, ref read_pos, module.file_contents[i].header.TagStructCount, tag_def_structure_size);

                        // read tag data references?
                        module.file_contents[i].data_references = struct_array_assign_bytes<data_reference>(decompressed_data, ref read_pos, module.file_contents[i].header.DataReferenceCount, data_reference_size);

                        // read tag tag fixup references 
                        module.file_contents[i].tag_fixup_references = struct_array_assign_bytes<tag_fixup_reference>(decompressed_data, ref read_pos, module.file_contents[i].header.TagReferenceCount, tag_fixup_reference_size);

                        // read tag string ids // no descernable count for this array // probably doesn't exist
                        //module.file_contents[i].string_id_references = struct_array_assign_bytes<string_id_reference>(decompressed_block, ref read_pos, module.file_contents[i].header.strin, tag_fixup_reference_size);

                        // assign the string table bytes, wow this is not convienent at all lol
                        if (module.file_contents[i].header.StringTableSize > 0)
                        {
                            //module.file_contents[i].local_string_table = new byte[module.file_contents[i].header.StringTableSize];
                            module.file_contents[i].local_string_table = decompressed_data.Skip((int)read_pos).Take((int)module.file_contents[i].header.StringTableSize).ToArray();
                            read_pos += module.file_contents[i].header.StringTableSize;
                        }

                        // read the zoneset header
                        module.file_contents[i].zoneset_info = KindaSafe_SuperCast<zoneset_header>(decompressed_data, read_pos);
                        read_pos += (ulong)zoneset_header_size;

                        // read all the zoneset instances
                        module.file_contents[i].zonesets = new zoneset_instance[module.file_contents[i].zoneset_info.ZonesetCount];
                        // its literally not possible for that to be a null reference, we just set it above
                        for (int m = 0; m < module.file_contents[i].zonesets.Length; m++)
                        {
                            // read the header
                            module.file_contents[i].zonesets[m].header = KindaSafe_SuperCast<zoneset_instance_header>(decompressed_data, read_pos);
                            read_pos += (ulong)zoneset_instance_header_size;

                            // read the regular zoneset tags
                            module.file_contents[i].zonesets[m].zonset_tags = struct_array_assign_bytes<zoneset_tag>(decompressed_data, ref read_pos, module.file_contents[i].zonesets[m].header.TagCount, zoneset_tag_size);

                            // read the zoneset footer tags (whatever they are?)
                            module.file_contents[i].zonesets[m].zonset_footer_tags = struct_array_assign_bytes<zoneset_tag>(decompressed_data, ref read_pos, module.file_contents[i].zonesets[m].header.FooterCount, zoneset_tag_size);

                            // read the parents
                            module.file_contents[i].zonesets[m].zonset_parents = struct_array_assign_bytes<int>(decompressed_data, ref read_pos, module.file_contents[i].zonesets[m].header.ParentCount, 4);
                        }
                        // end of header, double check to make sure we read it all correctly // APPARENTLY THERES A LOT OF CASES WITH DATA THAT WE DONT READ !!!!!!!!!!!!!! FU BUNGIE
                        //if (module.file_contents[i].header.HeaderSize != read_pos)
                        //{
                        //    //module.file_contents[i].unmapped_header_data = decompressed_data.Skip((int)read_pos).Take((int)module.file_contents[i].header.HeaderSize - (int)read_pos).ToArray();
                        //    //take_that_mfing_array_and_stick_it_in_my_clipboard_RIGHT_NOW(module.file_contents[i].unmapped_header_data);
                        //}
                        // we'll store the whole thing for now, until we find a better alternative
                        module.file_contents[i].header_data = decompressed_data.Take((int)module.file_contents[i].header.HeaderSize).ToArray();

                        // read tag data
                        if (module.file_contents[i].header.DataSize > 0)
                            module.file_contents[i].tag_data = decompressed_data.Skip((int)module.file_contents[i].header.HeaderSize).Take((int)module.file_contents[i].header.DataSize).ToArray();

                        // read resource data
                        if (module.file_contents[i].header.ResourceDataSize > 0)
                        {
                            module.file_contents[i].tag_resource = decompressed_data.Skip((int)module.file_contents[i].header.HeaderSize + (int)module.file_contents[i].header.DataSize).Take((int)module.file_contents[i].header.ResourceDataSize).ToArray();
                            
                        }

                        // read actual resource data
                        if (module.file_contents[i].header.ActualResoureDataSize > 0)
                        {
                            int offset = (int)(module.file_contents[i].header.HeaderSize + module.file_contents[i].header.DataSize + module.file_contents[i].header.ResourceDataSize);
                            module.file_contents[i].actual_tag_resource = decompressed_data.Skip(offset).Take((int)module.file_contents[i].header.ActualResoureDataSize).ToArray();
                            //take_that_mfing_array_and_stick_it_in_my_clipboard_RIGHT_NOW(module.file_contents[i].actual_tag_resource);
                        }

                        //if (module.files[i].ClassId == 1651078253) // check out the data for bitmaps
                        //{
                        //    //take_that_mfing_array_and_stick_it_in_my_clipboard_RIGHT_NOW(decompressed_data);
                        //    //take_that_mfing_array_and_stick_it_in_my_clipboard_RIGHT_NOW(module.file_contents[i].tag_resource);
                        //}


                        //take_that_mfing_array_and_stick_it_in_my_clipboard_RIGHT_NOW(module.file_contents[i].tag_data);

                    }
                }
                // ok thats all, the tags have been read
                // this is so we could preview the datas 
                tag_header[] debug_tag_headers = new tag_header[module.file_contents.Length];
                for (int i = 0; i < debug_tag_headers.Length; i++)
                {
                    debug_tag_headers[i] = module.file_contents[i].header;
                }

                // DEBUG STUFF

                string[] tags_string_list = new string[module.files.Length];

                for (int i = 0; i < module.files.Length; i++)
                    tags_string_list[i] = getstring_of_tag(i);

                tag_box.ItemsSource = tags_string_list;

            }
        }
        string  getstring_of_tag(int file_index)
        {
            string tag_name = "";
            int byte_offset = 0;
            while (true)
            {
                byte next_character = module.string_table[module.files[file_index].NameOffset + byte_offset];
                if (next_character == 0) break;

                tag_name += (char)next_character;
                byte_offset++;
            }
            return tag_name;
        }

        void unpack_tag(int tag_index, string classid)
        {
            // figure out the name of the tag to dump
            string file = getstring_of_tag(tag_index);


        }

        // ui testing junk
        private void raw_Click(object sender, RoutedEventArgs e)
        {
            if (tag_box.SelectedIndex > -1)
                pass_array_into_clipboard(module.file_contents[tag_box.SelectedIndex].raw_file_bytes);
        }
        private void data_Click(object sender, RoutedEventArgs e)
        {
            if (tag_box.SelectedIndex > -1)
                pass_array_into_clipboard(module.file_contents[tag_box.SelectedIndex].tag_data);
        }

        private void resource_Click(object sender, RoutedEventArgs e)
        {
            if (tag_box.SelectedIndex > -1)
                pass_array_into_clipboard(module.file_contents[tag_box.SelectedIndex].tag_resource);
        }

        private void other_source_Click(object sender, RoutedEventArgs e)
        {
            if (tag_box.SelectedIndex > -1)
                pass_array_into_clipboard(module.file_contents[tag_box.SelectedIndex].actual_tag_resource);
        }

        private void unmarked_Click(object sender, RoutedEventArgs e)
        {
            if (tag_box.SelectedIndex > -1)
            {
                byte[] test = BitConverter.GetBytes(module.files[tag_box.SelectedIndex].ClassId);
                Array.Reverse(test);
                string xml_group = System.Text.Encoding.Default.GetString(test);
                TagDataExporter new_exporter = new TagDataExporter("C:\\Users\\Joe bingle\\Downloads\\test exported tags\\tag1.xml",
                                                                  "C:\\Users\\Joe bingle\\Downloads\\plugins\\" + xml_group + ".xml",
                                                                  null,
                                                                  module.file_contents[tag_box.SelectedIndex]);
                if (new_exporter.Unpack_tag())
                {

                }
            }
                //pass_array_into_clipboard(module.file_contents[tag_box.SelectedIndex].unmapped_header_data);


            //pass_array_into_clipboard(module.file_contents[tag_box.SelectedIndex].header_data);
        }

        public void dump_tag()
        {
            // set this up later
            // setup button click to unpack tag
            // tag blocks should be easy, calculatae offset and searcg for matching tag strucct def
            // 
        }

        private void tag_box_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            select_ind.Text = "Selected_Index: " + tag_box.SelectedIndex;
            if (tag_box.SelectedIndex > -1)
            {
                // check raw data
                if (module.file_contents[tag_box.SelectedIndex].raw_file_bytes != null)
                    raw.IsEnabled = true; else
                    raw.IsEnabled = false;

                // check tag data
                if (module.file_contents[tag_box.SelectedIndex].tag_data != null)
                    data.IsEnabled = true; else
                    data.IsEnabled = false;

                // check resource data
                if (module.file_contents[tag_box.SelectedIndex].tag_resource != null)
                    resource.IsEnabled = true; else
                    resource.IsEnabled = false;

                // check other resource data
                if (module.file_contents[tag_box.SelectedIndex].actual_tag_resource != null)
                    other_source.IsEnabled = true; else
                    other_source.IsEnabled = false;

                // check unmapped data
                //if (module.file_contents[tag_box.SelectedIndex].unmapped_header_data != null)
                if (module.file_contents[tag_box.SelectedIndex].header_data != null)
                    unmarked.IsEnabled = true; else
                    unmarked.IsEnabled = false;
            }
        }

    }
}
