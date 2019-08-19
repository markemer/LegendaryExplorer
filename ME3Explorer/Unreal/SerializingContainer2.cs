﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using SharpDX;
using StreamHelpers;

namespace ME3Explorer
{
    public class SerializingContainer2
    {
        public Stream ms;
        public bool IsLoading;
        public IMEPackage Pcc;
        public MEGame Game => Pcc.Game;

        public SerializingContainer2(Stream stream, IMEPackage pcc, bool isLoading = false)
        {
            ms = stream;
            IsLoading = isLoading;
            Pcc = pcc;
        }
    }

    public static class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref int val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadInt32();
            else
                sc.ms.WriteInt32(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref uint val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadUInt32();
            else
                sc.ms.WriteUInt32(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref short val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadInt16();
            else
                sc.ms.WriteInt16(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref ushort val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadUInt16();
            else
                sc.ms.WriteUInt16(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref long val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadInt64();
            else
                sc.ms.WriteInt64(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref ulong val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadUInt64();
            else
                sc.ms.WriteUInt64(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref byte val)
        {
            if (sc.IsLoading)
                val = (byte)sc.ms.ReadByte();
            else
                sc.ms.WriteByte(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref string val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadUnrealString();
            else
                sc.ms.WriteUnrealString(val, sc.Game);
        }

        public static void Serialize(this SerializingContainer2 sc, ref float val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadFloat();
            else
                sc.ms.WriteFloat(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref double val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadDouble();
            else
                sc.ms.WriteDouble(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref bool val)
        {
            if (sc.IsLoading)
                val = sc.ms.ReadBoolInt();
            else
                sc.ms.WriteBoolInt(val);
        }

        public static void Serialize(this SerializingContainer2 sc, ref byte[] buffer, int size)
        {
            if (sc.IsLoading)
            {
                buffer = sc.ms.ReadToBuffer(size);
            }
            else
            {
                sc.ms.WriteFromBuffer(buffer);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref Guid guid)
        {
            if (sc.IsLoading)
                guid = sc.ms.ReadGuid();
            else
                sc.ms.WriteGuid(guid);
        }

        public delegate void SerializeDelegate<T>(SerializingContainer2 sc, ref T item);

        public static void Serialize<T>(this SerializingContainer2 sc, ref T[] arr, SerializeDelegate<T> serialize)
        {
            int count = arr?.Length ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                arr = new T[count];
            }

            for (int i = 0; i < count; i++)
            {
                serialize(sc, ref arr[i]);
            }
        }
        public static void Serialize<TKey, TValue>(this SerializingContainer2 sc, ref OrderedMultiValueDictionary<TKey, TValue> dict, SerializeDelegate<TKey> serializeKey, SerializeDelegate<TValue> serializeValue)
        {
            int count = dict?.Count ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                dict = new OrderedMultiValueDictionary<TKey, TValue>(count);
                for (int i = 0; i < count; i++)
                {
                    TKey key = default;
                    serializeKey(sc, ref key);
                    TValue value = default;
                    serializeValue(sc, ref value);
                    dict.Add(key, value);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var key = dict[i].Key;
                    serializeKey(sc, ref key);
                    var value = dict[i].Value;
                    serializeValue(sc, ref value);
                }
            }

        }

        public static void Serialize(this SerializingContainer2 sc, ref NameReference name)
        {
            if (sc.IsLoading)
            {
                name = sc.ms.ReadNameReference(sc.Pcc);
            }
            else
            {
                sc.ms.WriteNameReference(name, sc.Pcc);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref Color color)
        {
            if (sc.IsLoading)
            {
                byte b = (byte)sc.ms.ReadByte();
                byte g = (byte)sc.ms.ReadByte();
                byte r = (byte)sc.ms.ReadByte();
                byte a = (byte)sc.ms.ReadByte();
                color = new Color(r, g, b, a);
            }
            else
            {
                sc.ms.WriteByte(color.B);
                sc.ms.WriteByte(color.G);
                sc.ms.WriteByte(color.R);
                sc.ms.WriteByte(color.A);
            }
        }
    }
}
