/*using System.Collections.Generic;
using System.Data.Common;
using System;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Security.Cryptography;

namespace Game.DataStorage.ClientReader.Info
{
    class WDC4Info
    {
        //                           WDC4
        // This section only applies to versions DF(10.1.0.48480) … 10.2.5.52393.
        // In WDC4 'encrypted_status' struct got added after 'common_data', this structure holds the IDs of encrypted records,
        // likely to give a better idea of what IDs are available/unavailable while loading the DB2 (provided you have keys available).

        // WDC4 also has the reappearance of flag 0x02 in Collectable* sparse tables, which if set, moves offset_map_id_list
        // to before relationship_map and in relationship_entry uses record IDs instead of record index.
        enum headerFlags : ushort
        {
            /// <summary>
            /// Offset map records -- these records have null-terminated strings inlined, and
            /// since they are variable-length, they are pointed to by an array of 6-byte offset+size pairs.
            /// </summary>
            Has_offset_map = 0x01,

            /// <summary>
            /// This may be 'secondary keys' and is unrelated to WDC1+ relationships (not present in any WDC3 files, but is present in WDC4+, see note there)
            /// </summary>
            Has_relationship_data = 0x02,

            Has_nonInline_IDs = 0x04,

            /// <summary>
            /// // WDC1+ (not present in any WDC3 files, possibly outdated?)
            /// </summary>
            Is_bitpacked = 0x10,
        }

        //Structure
        struct wdc4_db2_header
        {
            public uint magic;                  // 'WDC4'
            public uint record_count;           // this is for all sections combined now
            public uint field_count;
            public uint record_size;
            public uint string_table_size;      // this is for all sections combined now

            // Table Hashes are the name without .db2 or path, e.g. "ZoneStory", slapped into SStrHash (table_name, false, 0) (i.e. implicitly uppercased).
            public uint table_hash;

            //It is not currently known how layout hashes are generated.

            //Layout hashes change when:
            //  Columns get renamed
            //  Columns get moved
            //  Columns change types (i.e. int -> uint)
            //  Column going from noninline -> inline vice versa
            //  Column status changes(inline/noninline, relation/no relation)
            //  Indexes are added/removed(i.e.a primary or unique index)

            //Layout hashes DO NOT change when:
            //  Number of elements in array columns change
            //  Based on the above information one can speculate that layouthashes are a hash of an SQL query
            public uint layout_hash;
            public uint min_id;
            public uint max_id;
            public uint locale;                 // as seen in TextWowEnum
            public headerFlags flags;                  // possible values are listed in Known Flag Meanings
            public ushort id_index;               // this is the index of the field containing ID values; this is ignored if flags & 0x04 != 0
            public uint total_field_count;      // from WDC1 onwards, this value seems to always be the same as the 'field_count' value
            public uint bitpacked_data_offset;  // relative position in record where bitpacked data begins; not important for parsing the file
            public uint lookup_column_count;
            public uint field_storage_info_size;
            public uint common_data_size;
            public uint pallet_data_size;
            public uint section_count;          // new to WDC2, this is number of sections of data
        };
        static wdc4_db2_header header;

        // a section = records + string block + id list + copy table + offset map + offset map id list + relationship map
        struct wdc4_section_header
        {
            public ulong tact_key_hash;          // TactKeyLookup hash
            public uint file_offset;            // absolute position to the beginning of the section
            public uint record_count;           // 'record_count' for the section
            public uint string_table_size;      // 'string_table_size' for the section
            public uint offset_records_end;     // Offset to the spot where the records end in a file with an offset map structure;
            public uint id_list_size;           // Size of the list of ids present in the section
            public uint relationship_data_size; // Size of the relationship data in the section
            public uint offset_map_id_count;    // Count of ids present in the offset map in the section
            public uint copy_table_count;       // Count of the number of deduplication entries (you can multiply by 8 to mimic the old 'copy_table_size' field)
        };
        static wdc4_section_header[] section_headers = new wdc4_section_header[header.section_count];

        struct field_structure
        {
            public short size;                   // size in bits as calculated by: byteSize = (32 - size) / 8; this value can be negative to indicate field sizes larger than 32-bits
            public ushort position;              // position of the field within the record, relative to the start of the record
        };
        static field_structure[] fields = new field_structure[header.total_field_count];

        enum field_compression : uint
        {
            // None -- usually the field is a 8-, 16-, 32-, or 64-bit integer in the record data. But can contain 96-bit value representing 3 floats as well
            field_compression_none,
            // Bitpacked -- the field is a bitpacked integer in the record data.  It
            // is field_size_bits long and starts at field_offset_bits.
            // A bitpacked value occupies
            //   (field_size_bits + (field_offset_bits & 7) + 7) / 8
            // bytes starting at byte
            //   field_offset_bits / 8
            // in the record data.  These bytes should be read as a little-endian value,
            // then the value is shifted to the right by (field_offset_bits & 7) and
            // masked with ((1ull << field_size_bits) - 1).
            field_compression_bitpacked,
            // Common data -- the field is assumed to be a default value, and exceptions
            // from that default value are stored in the corresponding section in
            // common_data as pairs of { uint32_t record_id; uint32_t value; }.
            field_compression_common_data,
            // Bitpacked indexed -- the field has a bitpacked index in the record data.
            // This index is used as an index into the corresponding section in
            // pallet_data.  The pallet_data section is an array of uint32_t, so the index
            // should be multiplied by 4 to obtain a byte offset.
            field_compression_bitpacked_indexed,
            // Bitpacked indexed array -- the field has a bitpacked index in the record
            // data.  This index is used as an index into the corresponding section in
            // pallet_data.  The pallet_data section is an array of uint32_t[array_count],
            //
            field_compression_bitpacked_indexed_array,
            // Same as field_compression_bitpacked
            field_compression_bitpacked_signed,
        };

        struct field_bitpacked_data
        {
            public uint bitpacking_offset_bits; // not useful for most purposes; formula they use to calculate is bitpacking_offset_bits = field_offset_bits - (header.bitpacked_data_offset * 8)
            public uint bitpacking_size_bits; // not useful for most purposes
            public uint flags; // known values - 0x01: sign-extend (signed)
        }

        struct field_common_data
        {
            public uint default_value;
            public uint unk_or_unused2;
            public uint unk_or_unused3;
        }

        struct field_bitpacked_indexed_data
        {
            public uint bitpacking_offset_bits; // not useful for most purposes; formula they use to calculate is bitpacking_offset_bits = field_offset_bits - (header.bitpacked_data_offset * 8)
            public uint bitpacking_size_bits; // not useful for most purposes
            public uint unk_or_unused3;
        }

        struct field_bitpacked_indexed_array_data
        {
            public uint bitpacking_offset_bits; // not useful for most purposes; formula they use to calculate is bitpacking_offset_bits = field_offset_bits - (header.bitpacked_data_offset * 8)
            public uint bitpacking_size_bits; // not useful for most purposes
            public uint array_count;
        }

        struct field_unknown_data
        {
            public uint unk_or_unused1;
            public uint unk_or_unused2;
            public uint unk_or_unused3;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct field_data
        {
            public static int size = 4 + 4 + 4;

            //switch (storage_type)
            //case field_compression.field_compression_bitpacked:
            //case field_compression.field_compression_bitpacked_signed:
            [FieldOffset(0)]
            public field_bitpacked_data bitpacked;

            //case field_compression.field_compression_common_data:
            [FieldOffset(0)]
            public field_common_data common;

            //case field_compression.field_compression_bitpacked_indexed:
            [FieldOffset(0)]
            public field_bitpacked_indexed_data bitpacked_indexed;

            //case field_compression.field_compression_bitpacked_indexed_array:
            [FieldOffset(0)]
            public field_bitpacked_indexed_array_data bitpacked_indexed_array;

            //default:
            [FieldOffset(0)]
            public field_unknown_data unknown;
        }

        struct field_storage_info
        {
            public static int size = 2 + 2 + 4 + 4 + field_data.size;

            public ushort field_offset_bits;
            public ushort field_size_bits; // very important for reading bitpacked fields; size is the sum of all array pieces in bits - for example, uint32[3] will appear here as '96'
                                           // additional_data_size is the size in bytes of the corresponding section in
                                           // common_data or pallet_data.  These sections are in the same order as the
                                           // field_info, so to find the offset, add up the additional_data_size of any
                                           // previous fields which are stored in the same block (common_data or
                                           // pallet_data).
            public uint additional_data_size;
            public field_compression storage_type;

            public field_data data;
        };

        // Holds encryption status of specific ID in a section where tact_key_hash is not 0.
        struct encrypted_status
        {
            public int encrypted_id_count;
            public int[] encrypted_id;

            public encrypted_status(int count)
            {
                encrypted_id_count = count;
                encrypted_id = new int[encrypted_id_count];
            }
        };

        static field_storage_info[] field_info = new field_storage_info[header.field_storage_info_size / field_storage_info.size];
        static char[] pallet_data = new char[header.pallet_data_size];
        static char[] common_data = new char[header.common_data_size];

        // encrypted_status structure is only used on sections where tact_key_hash is not 0
        static encrypted_status encrypted_records;
        //for (int i = 0; i < header.section_count; ++i)
        //{
        //    if (section_headers[i].tact_key_hash == 0)
        //        continue;

        //    encrypted_status encrypted_records;
        //}

        struct record_data
        {
            public char[] data;

            public record_data()
            {
                data = new char[header.record_size];
            }
        };

        struct copy_table_entry
        {
            public uint id_of_new_row;
            public uint id_of_copied_row;
        };

        struct offset_map_entry
        {
            uint offset;
            ushort size;
        };

        struct relationship_entry
        {
            // This is the id of the foreign key for the record, e.g. SpellID in SpellX* tables.
            uint foreign_id;
            // This is the index of the record in record_data.  Note that this is *not* the record's own ID *unless* flag 0x02 is set.
            uint record_index;
        };

        struct relationship_mapping
        {
            uint num_entries;
            uint min_id;
            uint max_id;
            relationship_entry[] entries = new[num_entries];
        };

        struct section
        {
            //--------------------
            //if ((header.flags & 0x01) == 0) //Normal records            
            public record_data[] records = new record_data[section_headers.record_count];
            public char[] string_data = new char[section_headers.string_table_size];
            //else            
            // Offset map records -- these records have null-terminated strings inlined, and
            // since they are variable-length, they are pointed to by an array of 6-byte offset+size pairs.
            char variable_record_data[section_headers.offset_records_end - section_headers.file_offset];
            //----------------------            
            public uint[] id_list = new uint[section_headers.id_list_size / 4];
            //---------------------- 
            //if (section_headers.copy_table_count > 0)            
            public copy_table_entry[] copy_table = new copy_table_entry[section_headers.copy_table_count];
            //---------------------- 
            public offset_map_entry[] offset_map = new offset_map_entry[section_headers.offset_map_id_count];
            //---------------------- 
            //if (section_headers.relationship_data_size > 0)
            relationship_mapping relationship_map; // In some tables, this relationship mapping replaced columns that were used only as a lookup, such as the SpellID in SpellX* tables.
            //----------------------
            // Note, if flag 0x02 is set offset_map_id_list will appear before relationship_map instead.
            uint offset_map_id_list[section_headers.offset_map_id_count];
        }
        static section data_sections[header.section_count];
    }
}
*/
