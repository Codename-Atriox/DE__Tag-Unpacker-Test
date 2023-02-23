using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using static Infinite_module_test.module_structs;
using static Infinite_module_test.code_utils;
using System.Windows.Controls;
using System.Text.RegularExpressions;

namespace Infinite_module_test{
    public class TagDataExporter {

        private XmlWriterSettings xmlWriterSettings = new() { Indent = true, IndentChars = "\t" };

        public TagDataExporter(string _destination, string _plugin_file, string? _game_version, tag _file_to_unpack){
            file_to_unpack = _file_to_unpack;
            destination = _destination;
            plugin_file = _plugin_file;
            game_version = _game_version;
            reference_xml = null;
            reference_root = null;
            TagWriter = null;
        }
        public tag file_to_unpack;
        public string destination;
        string plugin_file;
        string? game_version;
        XmlDocument reference_xml;
        XmlNode? reference_root;

        XmlWriter TagWriter;

        Regex make_my_param_name_xml_compatible = new Regex("[^a-zA-Z0-9-]");
        public bool Unpack_tag(){
            if (file_to_unpack == null) return false; // failed to unpacked tag due to no tag reference
            if (file_to_unpack.tag_structs == null || file_to_unpack.data_blocks == null) return false; // failed due to poorly configured tag

            if (!File.Exists(plugin_file)) return false; // failed to unpack tag due to reference plugin missing

            // setup reference plugin file
            reference_xml = new XmlDocument();
            reference_xml.Load(plugin_file);
            reference_root = reference_xml.SelectSingleNode("root");
            if (reference_root == null) return false; // failed due to reference plugin being a bad reference plugin 
            if (reference_root.Attributes == null) return false; // failed due to bad root node

            if (game_version != null){ // if we provided a string, then we intended to make sure the versions match, otherwise math regardless, even if the plugin is unversioned
                XmlAttribute? xna = reference_root.Attributes["GameVersion"];
                if (xna == null) return false; // failed due to game version not specified in plugin
                if (xna.Value != game_version) return false; // failed due to version mismatch
            }

            // if we made it past all that, thne we're good to start writing the file
            // setup file to write to
            TagWriter = XmlWriter.Create(destination, xmlWriterSettings);
            TagWriter.WriteStartDocument();
            TagWriter.WriteStartElement("tagdata");

            // first thing we need to do is to find the first tag struct to read
            // then we loop through each param and process it
            // if we come a cross a param that is a struct, we'll do some recursion to read the elements of that struct
            // if we come across a tagblock, then we'll have to do something cheeky to read it inline,i think we can just refer to the tag structures and get their resulting file offset
            // we might havet to sort the structs by some criteria to make this easier, probably the guid

            // loop through all of the structs until we find the one that is the main struct (type == 0)
            // process that then break 
            for (int i = 0; i < file_to_unpack.tag_structs.Length; i++){
                if (file_to_unpack.tag_structs[i].Type == 0){
                    process_highlevel_struct(ref file_to_unpack.tag_structs[i]);
                    break;
            }}



            // close document
            TagWriter.WriteEndElement();
            TagWriter.WriteEndDocument();
            TagWriter.Close();
            return true;
        }

        public void process_highlevel_struct(ref tag_def_structure tag_struct, int block_count = -1)
        {
            // before we do any of that, lets determine whether this struct is null or not
            if (tag_struct.TargetIndex == -1) return; // this struct is null, nothing here needs to be read

            // first we need to read the xml reference, to return all the children of this struct
            // to do that, lets get the guid of the struct, so we can select the right reference struct
            string GUID = "_" + tag_struct.GUID_1.ToString("X8") + tag_struct.GUID_2.ToString("X8");
            // now we can select the struct
            XmlNode currentStruct = reference_root.SelectSingleNode(GUID);
            // we dont write an attribute for the struct, as we'll write attributes 

            // we then begin the process of reading through each param of this struct
            // because of how the struct group params work, we must abstract this to ANOTHER function
            // if (currentStruct == null) return; // we should bring up an exception for this realistically 
            if (block_count == -1)
                process_literal_struct(currentStruct, ref tag_struct);
            else{
                for (int i = 0; i < block_count; i++)
                {
                    TagWriter.WriteStartElement("_" + i.ToString());
                    process_literal_struct(currentStruct, ref tag_struct);
                    TagWriter.WriteEndElement();
                }
            }
        }

        public void process_literal_struct(XmlNode structparent, ref tag_def_structure tag_struct, ulong current_offset = 0)
        {
            // if not, then we shall retrive the information to actually beable to read this struct
            data_block struct_file_offset = file_to_unpack.data_blocks[tag_struct.TargetIndex];
            // BREAKPOINT incase this is wrong
            byte[] struct_bytes;
            if (struct_file_offset.Section == 0) // then its in the tag header block
                struct_bytes = file_to_unpack.header_data;
            else if (struct_file_offset.Section == 1) // then its in the tag data block
                struct_bytes = file_to_unpack.tag_data;
            else if (struct_file_offset.Section == 2) // then its in the tag data block
                struct_bytes = file_to_unpack.tag_resource;
            else if (struct_file_offset.Section == 3) // then its in the tag data block
                struct_bytes = file_to_unpack.actual_tag_resource;
            else
                struct_bytes = null; // we should never hit this block


            for (int i = 0; i < structparent.ChildNodes.Count; i++)
            {
                XmlNode currentParam = structparent.ChildNodes[i];
                // test whether its a group of type that we aren't supposed to read
                if (currentParam.Name == "_36" || currentParam.Name == "_37" || currentParam.Name == "_3B")
                    continue; // we hate these types; dont need to write them to the individual tags

                ulong relative_offset = (ulong)Convert.ToInt32(currentParam.Attributes["Offset"].Value, 16);
                relative_offset += current_offset;
                ulong param_offset = relative_offset + struct_file_offset.Offset;

                // write the node, then write attributes
                // we're going to have to hash the string for this to work
                string filtered_name = "_" + make_my_param_name_xml_compatible.Replace(currentParam.Attributes["Name"].Value, "");
                TagWriter.WriteStartElement(filtered_name);
                switch (currentParam.Name)
                {
                    case "_0": // _field_string - 32byte
                        string byte_stringus = System.Text.Encoding.Default.GetString(struct_bytes.Skip((int)param_offset).Take(32).ToArray());
                        byte_stringus = byte_stringus.Split('\0').First();
                        //if (byte_stringus == "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0") 
                        //    byte_stringus = "";
                        TagWriter.WriteAttributeString("v", byte_stringus);
                        break;
                    case "_1": // _field_long_string - 256byte
                        string byte_big_stringus = System.Text.Encoding.Default.GetString(struct_bytes.Skip((int)param_offset).Take(256).ToArray());
                        byte_big_stringus = byte_big_stringus.Split('\0').First();
                        TagWriter.WriteAttributeString("v", byte_big_stringus);
                        break;
                    case "_2": // _field_string_id - 4byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<int>(struct_bytes, param_offset).ToString());
                        break;
                    //case "_3": // UNMAPPED //
                    //
                    //    break;
                    case "_4": // _field_char_integer - 1byte
                        TagWriter.WriteAttributeString("v", struct_bytes[param_offset].ToString());
                        break;
                    case "_5": // _field_short_integer - 2byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<short>(struct_bytes, param_offset).ToString());
                        break;
                    case "_6": // _field_long_integer - 4byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<int>(struct_bytes, param_offset).ToString());
                        break;
                    case "_7": // _field_int64_integer - 8byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<long>(struct_bytes, param_offset).ToString());
                        break;
                    case "_8": // _field_angle - 4byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString());
                        break;
                    case "_9": // _field_tag - 4byte
                        TagWriter.WriteAttributeString("v", System.Text.Encoding.Default.GetString(struct_bytes.Skip((int)param_offset).Take(4).ToArray()));
                        break;
                    // i think for these guys, it would be easier if we left it up to the tool to read the names, im pretty sure theres been no recorded changes to flags or whatever
                    // infact we may be more likely to not beable to support updated tagstructs if we do write the flags/enum, opposed to the raw integer
                    case "_A": // _field_char_enum - 1byte
                        // string selected_item = currentParam.ChildNodes[struct_bytes[param_offset]].Attributes["n"].Value;
                        TagWriter.WriteAttributeString("v", struct_bytes[param_offset].ToString());
                        break;
                    case "_B": // _field_short_enum - 2byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<short>(struct_bytes, param_offset).ToString());
                        break;
                    case "_C": // _field_long_enum - 4byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<int>(struct_bytes, param_offset).ToString());
                        break;
                    case "_D": // _field_long_flags - 4byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<int>(struct_bytes, param_offset).ToString());
                        break;
                    case "_E": // _field_word_flags - 2byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<short>(struct_bytes, param_offset).ToString());
                        break;
                    case "_F": // _field_byte_flags - 1byte
                        TagWriter.WriteAttributeString("v", struct_bytes[param_offset].ToString());
                        break;
                    case "_10":{ // _field_point_2d - 4byte
                            string val1 = KindaSafe_SuperCast<short>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<short>(struct_bytes, param_offset + 2).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2);
                        }break;
                    case "_11":{ // _field_rectangle_2d - 4byte
                            string val1 = KindaSafe_SuperCast<short>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<short>(struct_bytes, param_offset + 2).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2);
                        }break;
                    case "_12": // _field_rgb_color - 4byte
                        // we'll leave it up the tag writer to exclude the last byte (which would be the first byte)
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<int>(struct_bytes, param_offset).ToString("X4"));
                        break;
                    case "_13": // _field_argb_color - 4byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<int>(struct_bytes, param_offset).ToString("X4"));
                        break;
                    case "_14": // _field_real - 4byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString());
                        break;
                    case "_15": // _field_real_fraction - 4byte
                        // im pretty sure we dont have to do anything specific here, tag writer will note range is between 0.0 - 1.0
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString());
                        break;
                    case "_16":{ // _field_real_point_2d - 8byte 
                            string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2);
                        }break;
                    case "_17":{ // _field_real_point_3d - 12byte
                            string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                            string val3 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 8).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2 + "," + val3);
                        }break;
                    case "_18":{ // _field_real_vector_2d - 8byte
                            string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2);
                        }break;
                    case "_19":{ // _field_real_vector_3d - 12byte
                            string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                            string val3 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 8).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2 + "," + val3);
                        }break;
                    case "_1A":{ // _field_real_quaternion - 16byte
                            string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                            string val3 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 8).ToString();
                            string val4 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 12).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2 + "," + val3 + "," + val4);
                        }break;
                    case "_1B":{ // _field_real_euler_angles_2d - 8byte
                            string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2);
                        }break;
                    case "_1C":{ // _field_real_euler_angles_3d - 12byte
                            string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                            string val3 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 8).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2 + "," + val3);
                        }break;
                    case "_1D":{ // _field_real_plane_2d - 12byte
                            string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                            string val3 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 8).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2 + "," + val3);
                        }break;
                    case "_1E":{ // _field_real_plane_3d - 16byte 
                            string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                            string val3 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 8).ToString();
                            string val4 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 12).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2 + "," + val3 + "," + val4);
                        }break;
                    case "_1F":{ // _field_real_rgb_color - 12byte
                            string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                            string val3 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 8).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2 + "," + val3);
                        }break;
                    case "_20":{ // _field_real_argb_color - 16byte
                            string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                            string val3 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 8).ToString();
                            string val4 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 12).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2 + "," + val3 + "," + val4);
                        }break;
                    case "_21": // _field_real_hsv_color - 4byte
                        // again here we leave it up to the tag writer tool to drop the alpha byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<int>(struct_bytes, param_offset).ToString("X4"));
                        break;
                    case "_22": // _field_real_ahsv_color - 4byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<int>(struct_bytes, param_offset).ToString("X4"));
                        break;
                    case "_23":{ // _field_short_bounds - 4byte
                        string val1 = KindaSafe_SuperCast<short>(struct_bytes, param_offset).ToString();
                        string val2 = KindaSafe_SuperCast<short>(struct_bytes, param_offset + 2).ToString();
                        TagWriter.WriteAttributeString("v", val1 + "," + val2);
                        }break;
                    case "_24":{ // _field_angle_bounds - 8byte
                            string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                            string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                            TagWriter.WriteAttributeString("v", val1 + "," + val2);
                        }break;
                    case "_25":{ // _field_real_bounds - 8byte
                        string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                        string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                        TagWriter.WriteAttributeString("v", val1 + "," + val2);
                        }break;
                    case "_26":{ // _field_real_fraction_bounds - 8byte
                        string val1 = KindaSafe_SuperCast<float>(struct_bytes, param_offset).ToString();
                        string val2 = KindaSafe_SuperCast<float>(struct_bytes, param_offset + 4).ToString();
                        TagWriter.WriteAttributeString("v", val1 + "," + val2);
                        }break;
                    //case "_27": // UNMAPPED //
                    //
                    //    break;
                    //case "_28": // UNMAPPED //
                    //
                    //    break;
                    // commented out because these values
                    //case "_29": // _field_long_block_flags - 4byte
                    //
                    //    break;
                    //case "_2A": // _field_word_block_flags - 4byte
                    //
                    //    break;
                    //case "_2B": // _field_byte_block_flags - 4byte
                    //
                    //    break;
                    //case "_2C": // _field_char_block_index - 1byte
                    //
                    //    break;
                    //case "_2D": // _field_char_block_index_custom - 1byte
                    //
                    //    break;
                    //case "_2E": // _field_short_block_index - 2byte
                    //
                    //    break;
                    //case "_2F": // _field_short_block_index_custom - 2byte
                    //
                    //    break;
                    //case "_30": // _field_long_block_index - 4byte
                    //
                    //    break;
                    //case "_31": // _field_long_block_index_custom - 4byte
                    //
                    //    break;
                    //case "_32": // UNMAPPED TYPE //
                    //
                    //    break;
                    //case "_33": // UNMAPPED TYPE //
                    //
                    //    break;
                    // do we even need these guys
                    case "_34": // _field_pad - X bytes
                    case "_35": // _field_skip - X bytes
                        int pad_length = Convert.ToInt32(currentParam.Attributes["Length"].Value);
                        TagWriter.WriteAttributeString("v", BitConverter.ToString(struct_bytes.Skip((int)param_offset).Take(pad_length).ToArray()).Replace("-", ""));
                        break;
                    // these two should be skipped?
                    //case "_36": // _field_explanation - 0byte
                    //
                    //    break;
                    //case "_37": // _field_custom - 0byte
                    //
                    //    break;
                    case "_38":{ // _field_struct - 0byte
                            string struct_guid = "_" + currentParam.Attributes["GUID"].Value;
                            XmlNode struct_node = reference_root.SelectSingleNode(struct_guid);
                            process_literal_struct(struct_node, ref tag_struct, relative_offset);
                        }break;
                    case "_39":{ // _field_array - 0byte
                            string struct_guid = "_" + currentParam.Attributes["GUID"].Value;
                            int array_count = Convert.ToInt32(currentParam.Attributes["Count"].Value);
                            XmlNode struct_node = reference_root.SelectSingleNode(struct_guid);
                            int referenced_array_size = Convert.ToInt32(struct_node.Attributes["Size"].Value);
                            for (int array_ind = 0; array_ind < array_count; array_ind++)
                            {
                                TagWriter.WriteStartElement("_" + make_my_param_name_xml_compatible.Replace(currentParam.Attributes["StructName1"].Value, "") + array_ind);
                                process_literal_struct(struct_node, ref tag_struct, relative_offset + (ulong)(referenced_array_size * array_ind));
                                TagWriter.WriteEndElement();
                            }
                        }break;
                    //case "_3A": // UNMAPPED TYPE //
                    //
                    //    break;
                    //case "_3B": // END OF STRUCT, TYPE DISCLUDED //
                    //
                    //    break;
                    case "_3C": // _field_byte_integer - 1byte
                        TagWriter.WriteAttributeString("v", struct_bytes[param_offset].ToString());
                        break;
                    case "_3D": // _field_word_integer - 2byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<short>(struct_bytes, param_offset).ToString());
                        break;
                    case "_3E": // _field_dword_integer - 4byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<int>(struct_bytes, param_offset).ToString());
                        break;
                    case "_3F": // _field_qword_integer - 8byte
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<long>(struct_bytes, param_offset).ToString());
                        break;
                    case "_40":{ // _field_tag_block - 20byte
                            // read the count
                            int tagblock_count = KindaSafe_SuperCast<int>(struct_bytes, param_offset + 16);
                            // find the struct that this is referring to
                            string struct_guid = currentParam.Attributes["GUID"].Value;
                            for (int struct_index = 0; struct_index < file_to_unpack.tag_structs.Length; struct_index++)
                            {
                                string this_guid = file_to_unpack.tag_structs[struct_index].GUID_1.ToString("X8")
                                                 + file_to_unpack.tag_structs[struct_index].GUID_2.ToString("X8");
                                if (this_guid == struct_guid)
                                {
                                    process_highlevel_struct(ref file_to_unpack.tag_structs[struct_index], tagblock_count);
                                    break;
                                }
                            }
                        }break;
                    // skip these guys for now, until we look further into it
                    case "_41": // _field_tag_reference - 28byte
                        // we can pretty much ignore the tag group and asset ID
                        // the global ID will tell us everything we need from the tag reference
                        TagWriter.WriteAttributeString("v", KindaSafe_SuperCast<int>(struct_bytes, param_offset + 8).ToString("X4"));
                        break;
                    case "_42": // _field_data - 24byte
                        pass_array_into_clipboard(struct_bytes.Skip((int)param_offset).Take(24).ToArray());
                        break;
                    case "_43": // _field_resource - 16byte
                        pass_array_into_clipboard(struct_bytes.Skip((int)param_offset).Take(16).ToArray());
                        break;
                    //case "_44": // _field_data_path
                    //
                    //    break;
                    //case "_45": // UNMAPPED TYPE //
                    //
                    //    break;
                    default:
                        // here we should breakpoint so we can figure out the lengths of unmapped types
                        // type 30 is used??
                        break;
                }

                // close off this parameter
                TagWriter.WriteEndElement();
            }
        }

    }
}
