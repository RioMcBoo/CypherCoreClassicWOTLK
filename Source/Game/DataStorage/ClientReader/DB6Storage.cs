// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Framework.Dynamic;
using Framework.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace Game.DataStorage
{
    public interface IDB2Storage
    {
        bool HasRecord(int id);

        void WriteRecord(int id, Locale locale, ByteBuffer buffer);

        void EraseRecord(int id);

        string GetName();
    }

    [Serializable]
    public class DB6Storage<TKey, TRecord> : Dictionary<TKey, TRecord>, IDB2Storage where TRecord : new() where TKey: struct
    {
        WDCHeader _header;
        string _tableName = typeof(TRecord).Name;

        TKey ConvertKey(dynamic id)
        {
            Type type = typeof(TKey);

            if (type.IsEnum)
                type = type.GetEnumUnderlyingType();

            TypeCode typeCode = Type.GetTypeCode(type);

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Single: Cypher.Assert(id >= float.MinValue && id <= float.MaxValue, $"DB6Storage's Key value will lost data!"); break;
                case TypeCode.Int32: Cypher.Assert(id >= int.MinValue && id <= int.MaxValue, $"DB6Storage's Key value will lost data!"); break;
                case TypeCode.UInt32: Cypher.Assert(id >= uint.MinValue && id <= uint.MaxValue, $"DB6Storage's Key value will lost data!"); break;
                case TypeCode.Int16: Cypher.Assert(id >= short.MinValue && id <= short.MaxValue, $"DB6Storage's Key value will lost data!"); break;
                case TypeCode.UInt16: Cypher.Assert(id >= ushort.MinValue && id <= ushort.MaxValue, $"DB6Storage's Key value will lost data!"); break;
                case TypeCode.Byte: Cypher.Assert(id >= byte.MinValue && id <= byte.MaxValue, $"DB6Storage's Key value will lost data!"); break;
                case TypeCode.SByte: Cypher.Assert(id >= sbyte.MinValue && id <= sbyte.MaxValue, $"DB6Storage's Key value will lost data!"); break;
                default:
                    throw new Exception($"Unhandled DB6Storage's TKey type: {type}");
            }

            return (TKey)id;
        }

        int ConvertKey(TKey key)
        {
            return Convert.ToInt32(key);
        }

        bool LoadData(string fullFileName)
        {
            if (!File.Exists(fullFileName))
            {
                Log.outError(LogFilter.ServerLoading, $"File {fullFileName} not found.");
                return false;
            }

            DBReader reader = new();
            using (var stream = new FileStream(fullFileName, FileMode.Open))
            {
                if (!reader.Load(stream))
                {
                    Log.outError(LogFilter.ServerLoading, $"Error loading {fullFileName}.");
                    return false;
                }
            }

            _header = reader.Header;           

            foreach (var b in reader.Records)
                Add(ConvertKey(b.Key), b.Value.As<TRecord>());

            return true;
        }

        void LoadHotfixData(BitSet availableDb2Locales, HotfixStatements preparedStatement, HotfixStatements preparedStatementLocale)
        {
            LoadFromDB(false, preparedStatement);
            LoadFromDB(true, preparedStatement);

            if (preparedStatementLocale == 0)
                return;

            for (Locale locale = 0; locale < Locale.Total; ++locale)
            {
                if (!availableDb2Locales[(int)locale])
                    continue;

                LoadStringsFromDB(false, locale, preparedStatementLocale);
                LoadStringsFromDB(true, locale, preparedStatementLocale);
            }
        }        

        void LoadFromDB(bool custom, HotfixStatements preparedStatement)
        {
            // Even though this query is executed only once, prepared statement is used to send data from mysql server in binary format
            PreparedStatement stmt = HotfixDatabase.GetPreparedStatement(preparedStatement);
            stmt.SetBool(0, !custom);
            SQLResult result = DB.Hotfix.Query(stmt);
            if (result.IsEmpty())
                return;

            do
            {
                var obj = new TRecord();

                int dbIndex = 0;
                var fields = typeof(TRecord).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var f in fields)
                {
                    Type type = f.FieldType;

                    if (type.IsArray)
                    {
                        Type arrayElementType = type.GetElementType();
                        if (arrayElementType.IsEnum)
                            arrayElementType = arrayElementType.GetEnumUnderlyingType();

                        Array array = (Array)f.GetValue(obj);
                        switch (Type.GetTypeCode(arrayElementType))
                        {
                            case TypeCode.SByte:
                                f.SetValue(obj, ReadArray<sbyte>(result, dbIndex, array.Length));
                                break;
                            case TypeCode.Byte:
                                f.SetValue(obj, ReadArray<byte>(result, dbIndex, array.Length));
                                break;
                            case TypeCode.Int16:
                                f.SetValue(obj, ReadArray<short>(result, dbIndex, array.Length));
                                break;
                            case TypeCode.UInt16:
                                f.SetValue(obj, ReadArray<ushort>(result, dbIndex, array.Length));
                                break;
                            case TypeCode.Int32:
                                f.SetValue(obj, ReadArray<int>(result, dbIndex, array.Length));
                                break;
                            case TypeCode.UInt32:
                                f.SetValue(obj, ReadArray<uint>(result, dbIndex, array.Length));
                                break;
                            case TypeCode.Int64:
                                f.SetValue(obj, ReadArray<long>(result, dbIndex, array.Length));
                                break;
                            case TypeCode.UInt64:
                                f.SetValue(obj, ReadArray<ulong>(result, dbIndex, array.Length));
                                break;
                            case TypeCode.Single:
                                f.SetValue(obj, ReadArray<float>(result, dbIndex, array.Length));
                                break;
                            case TypeCode.String:
                                f.SetValue(obj, ReadArray<string>(result, dbIndex, array.Length));
                                break;
                            case TypeCode.Object:
                                if (arrayElementType == typeof(Vector3))
                                {
                                    float[] values = ReadArray<float>(result, dbIndex, array.Length * 3);

                                    Vector3[] vectors = new Vector3[array.Length];
                                    for (var i = 0; i < array.Length; ++i)
                                        vectors[i] = new Vector3(values[(i * 3)..(3 + (i * 3))]);

                                    f.SetValue(obj, vectors);

                                    dbIndex += array.Length * 3;
                                }
                                continue;
                            default:
                                Log.outError(LogFilter.ServerLoading, $"Not implemented DB2 Record's field array type: {arrayElementType.Name}. Cannot be read!");
                                break;
                        }

                        dbIndex += array.Length;
                    }
                    else
                    {
                        if (type.IsEnum)
                            type = type.GetEnumUnderlyingType();

                        switch (Type.GetTypeCode(type))
                        {
                            case TypeCode.SByte:
                                f.SetValue(obj, result.Read<sbyte>(dbIndex++));
                                break;
                            case TypeCode.Byte:
                                f.SetValue(obj, result.Read<byte>(dbIndex++));
                                break;
                            case TypeCode.Int16:
                                f.SetValue(obj, result.Read<short>(dbIndex++));
                                break;
                            case TypeCode.UInt16:
                                f.SetValue(obj, result.Read<ushort>(dbIndex++));
                                break;
                            case TypeCode.Int32:
                                f.SetValue(obj, result.Read<int>(dbIndex++));
                                break;
                            case TypeCode.UInt32:
                                f.SetValue(obj, result.Read<uint>(dbIndex++));
                                break;
                            case TypeCode.Int64:
                                f.SetValue(obj, result.Read<long>(dbIndex++));
                                break;
                            case TypeCode.UInt64:
                                f.SetValue(obj, result.Read<ulong>(dbIndex++));
                                break;
                            case TypeCode.Single:
                                f.SetValue(obj, result.Read<float>(dbIndex++));
                                break;
                            case TypeCode.String:
                                string str = result.Read<string>(dbIndex++);
                                f.SetValue(obj, str);
                                break;
                            case TypeCode.Object:
                                if (type == typeof(LocalizedString))
                                {
                                    LocalizedString locString = new();
                                    locString[Global.WorldMgr.GetDefaultDbcLocale()] = result.Read<string>(dbIndex++);

                                    f.SetValue(obj, locString);
                                }
                                else if (type == typeof(Vector2))
                                {
                                    f.SetValue(obj, new Vector2(ReadArray<float>(result, dbIndex, 2)));
                                    dbIndex += 2;
                                }
                                else if (type == typeof(Vector3))
                                {
                                    f.SetValue(obj, new Vector3(ReadArray<float>(result, dbIndex, 3)));
                                    dbIndex += 3;
                                }
                                else if (type == typeof(FlagArray128))
                                {
                                    f.SetValue(obj, new FlagArray128(ReadArray<uint>(result, dbIndex, 4)));
                                    dbIndex += 4;
                                }
                                break;
                            default:
                                Log.outError(LogFilter.ServerLoading, $"Not implemented DB2 Record's field type: {type.Name}. Cannot be read!");
                                break;
                        }
                    }
                }

                var id = fields[_header.IdIndex == -1 ? 0 : _header.IdIndex].GetValue(obj);                
                base[ConvertKey(id)] = obj;
            }
            while (result.NextRow());
        }

        void LoadStringsFromDB(bool custom, Locale locale, HotfixStatements preparedStatement)
        {
            PreparedStatement stmt = HotfixDatabase.GetPreparedStatement(preparedStatement);
            stmt.SetBool(0, !custom);
            stmt.SetString(1, locale.ToString());
            SQLResult result = DB.Hotfix.Query(stmt);
            if (result.IsEmpty())
                return;

            do
            {
                int index = 0;
                var obj = this.LookupByKey(ConvertKey(result.Read<int>(index++)));
                if (obj == null)
                    continue;

                foreach (var f in typeof(TRecord).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.FieldType != typeof(LocalizedString))
                        continue;

                    LocalizedString locString = (LocalizedString)f.GetValue(obj);
                    locString[locale] = result.Read<string>(index++);
                }
            } while (result.NextRow());
        }

        TValue[] ReadArray<TValue>(SQLResult result, int dbIndex, int arrayLength)
        {
            TValue[] values = new TValue[arrayLength];
            for (int i = 0; i < arrayLength; ++i)
                values[i] = result.Read<TValue>(dbIndex + i);

            return values;
        }

        public bool HasRecord(int id)
        {
            return ContainsKey(ConvertKey(id));
        }

        public bool HasRecord(TKey id)
        {
            return ContainsKey(id);
        }

        public void WriteRecord(int id, Locale locale, ByteBuffer buffer)
        {
            WriteRecord(ConvertKey(id), locale, buffer);
        }

        public void WriteRecord(TKey id, Locale locale, ByteBuffer buffer)
        {
            TRecord entry = this.LookupByKey(id);

            foreach (var fieldInfo in entry.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (fieldInfo.Name == "Id" && _header.HasIndexTable())
                    continue;

                var type = fieldInfo.FieldType;
                if (type.IsArray)
                {
                    WriteArrayValues(entry, fieldInfo, buffer);
                    continue;
                }

                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Boolean:
                        buffer.WriteUInt8((byte)((bool)fieldInfo.GetValue(entry) ? 1 : 0));
                        break;
                    case TypeCode.SByte:
                        buffer.WriteInt8((sbyte)fieldInfo.GetValue(entry));
                        break;
                    case TypeCode.Byte:
                        buffer.WriteUInt8((byte)fieldInfo.GetValue(entry));
                        break;
                    case TypeCode.Int16:
                        buffer.WriteInt16((short)fieldInfo.GetValue(entry));
                        break;
                    case TypeCode.UInt16:
                        buffer.WriteUInt16((ushort)fieldInfo.GetValue(entry));
                        break;
                    case TypeCode.Int32:
                        buffer.WriteInt32((int)fieldInfo.GetValue(entry));
                        break;
                    case TypeCode.UInt32:
                        buffer.WriteUInt32((uint)fieldInfo.GetValue(entry));
                        break;
                    case TypeCode.Int64:
                        buffer.WriteInt64((long)fieldInfo.GetValue(entry));
                        break;
                    case TypeCode.UInt64:
                        buffer.WriteUInt64((ulong)fieldInfo.GetValue(entry));
                        break;
                    case TypeCode.Single:
                        buffer.WriteFloat((float)fieldInfo.GetValue(entry));
                        break;
                    case TypeCode.String:
                        buffer.WriteCString((string)fieldInfo.GetValue(entry));
                        break;
                    case TypeCode.Object:
                        switch (type.Name)
                        {
                            case "LocalizedString":
                                LocalizedString locStr = (LocalizedString)fieldInfo.GetValue(entry);
                                if (!locStr.HasString(locale))
                                {
                                    locale = 0;
                                    if (!locStr.HasString(locale))
                                    {
                                        buffer.WriteUInt8(0);
                                        break;
                                    }
                                }

                                string str = locStr[locale];
                                buffer.WriteCString(str);
                                break;
                            case "Vector2":
                                Vector2 vector2 = (Vector2)fieldInfo.GetValue(entry);
                                buffer.WriteVector2(vector2);
                                break;
                            case "Vector3":
                                Vector3 vector3 = (Vector3)fieldInfo.GetValue(entry);
                                buffer.WriteVector3(vector3);
                                break;
                            case "FlagArray128":
                                FlagArray128 flagArray128 = (FlagArray128)fieldInfo.GetValue(entry);
                                buffer.WriteUInt32(flagArray128[0]);
                                buffer.WriteUInt32(flagArray128[1]);
                                buffer.WriteUInt32(flagArray128[2]);
                                buffer.WriteUInt32(flagArray128[3]);
                                break;
                            default:
                                throw new Exception($"Unhandled Custom type: {type.Name}");
                        }
                        break;
                }
            }
        }

        void WriteArrayValues(object entry, FieldInfo fieldInfo, ByteBuffer buffer)
        {
            var type = fieldInfo.FieldType.GetElementType();
            var array = (Array)fieldInfo.GetValue(entry);
            for (var i = 0; i < array.Length; ++i)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Boolean:
                        buffer.WriteUInt8((byte)((bool)array.GetValue(i) ? 1 : 0));
                        break;
                    case TypeCode.SByte:
                        buffer.WriteInt8((sbyte)array.GetValue(i));
                        break;
                    case TypeCode.Byte:
                        buffer.WriteUInt8((byte)array.GetValue(i));
                        break;
                    case TypeCode.Int16:
                        buffer.WriteInt16((short)array.GetValue(i));
                        break;
                    case TypeCode.UInt16:
                        buffer.WriteUInt16((ushort)array.GetValue(i));
                        break;
                    case TypeCode.Int32:
                        buffer.WriteInt32((int)array.GetValue(i));
                        break;
                    case TypeCode.UInt32:
                        buffer.WriteUInt32((uint)array.GetValue(i));
                        break;
                    case TypeCode.Int64:
                        buffer.WriteInt64((long)array.GetValue(i));
                        break;
                    case TypeCode.UInt64:
                        buffer.WriteUInt64((ulong)array.GetValue(i));
                        break;
                    case TypeCode.Single:
                        buffer.WriteFloat((float)array.GetValue(i));
                        break;
                    case TypeCode.String:
                        var str = (string)array.GetValue(i);
                        buffer.WriteCString(str);
                        break;
                }
            }
        }

        public void EraseRecord(int id)
        {
            Remove(ConvertKey(id));
        }

        public void EraseRecord(TKey id)
        {
            Remove(id);
        }

        public uint GetTableHash() { return _header.TableHash; }

        public int GetNumRows() { return ConvertKey(Keys.Max()) + 1; }

        public string GetName()
        {
            return _tableName;
        }

        public void ReadDB2(DB2LoadData data, string fileName, HotfixStatements preparedStatement, HotfixStatements preparedStatementLocale = HotfixStatements.None)
        {
            if (!LoadData($"{data.path}/{fileName}"))
            {
                Log.outError(LogFilter.ServerLoading, "Error loading DB2 files");
                Environment.Exit(1);
                return;
            }

            LoadHotfixData(data.availableLocales, preparedStatement, preparedStatementLocale);

            Global.DB2Mgr.AddDB2(GetTableHash(), this);
            data.counter++;
        }        
    }

    public class DB2LoadData
    {
        public string path = string.Empty;
        public BitSet availableLocales;
        public int counter;
    }
}
