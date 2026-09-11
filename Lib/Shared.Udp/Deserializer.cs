using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Udp;

public static class Deserializer
{
    public static unsafe byte[] ReadFixed(byte* source, int length)
    {
        return new Span<byte>(source, length).ToArray();
    }

    public static unsafe string ReadFixedString(byte* source, int length)
    {
        return Encoding.ASCII.GetString(source, length);
    }

    public static T ReadStruct<T>(ReadOnlyMemory<byte> data)
        where T : struct
    {
        var size = Unsafe.SizeOf<T>();
        return data.Length < size ? default : MemoryMarshal.Read<T>(data.Span[..size]);
    }

    public static T ReadPrimitive<T>(ref ReadOnlyMemory<byte> data)
        where T : class
    {
        return (T)ReadPrimitive(ref data, typeof(T));
    }

    public static T ReadClass<T>(ref ReadOnlyMemory<byte> data)
        where T : class
    {
        return (T)ReadClass(ref data, typeof(T));
    }

    public static T Read<T>(ref ReadOnlyMemory<byte> data)
    {
        var type = typeof(T);

        // The packet header and GSS dispatch code read a handful of small primitives (and one
        // byte-sized enum) per incoming packet. The general object-based path pays for that with
        // a boxed return value, an Enum.ToObject reflection hop for enums, and a type attribute
        // scan per read. The typeof comparisons below are compile-time constant once T is closed,
        // so a call site like packet.Read<ushort>() compiles to a BinaryPrimitives load and an
        // offset advance. The encodings match ReadPrimitive exactly: explicit little-endian
        // integers, a single byte for char, and the raw bit pattern for float/double/Half.
        // Anything else (and sbyte/bool, which ReadPrimitive never supported either) keeps
        // falling through to the general path, throwing where it threw before.
        if (type == typeof(byte))
        {
            var value = data.Span[0];
            data = data[1..];
            return Unsafe.As<byte, T>(ref value);
        }

        if (type == typeof(char))
        {
            // One wire byte, decoded with Encoding.ASCII's replacement rule like the general path.
            var value = (char)(data.Span[0] <= 0x7F ? data.Span[0] : '?');
            data = data[1..];
            return Unsafe.As<char, T>(ref value);
        }

        if (type == typeof(ushort))
        {
            var value = BinaryPrimitives.ReadUInt16LittleEndian(data[..2].Span);
            data = data[2..];
            return Unsafe.As<ushort, T>(ref value);
        }

        if (type == typeof(short))
        {
            var value = BinaryPrimitives.ReadInt16LittleEndian(data[..2].Span);
            data = data[2..];
            return Unsafe.As<short, T>(ref value);
        }

        if (type == typeof(Half))
        {
            var value = (Half)BinaryPrimitives.ReadUInt16LittleEndian(data[..2].Span);
            data = data[2..];
            return Unsafe.As<Half, T>(ref value);
        }

        if (type == typeof(uint))
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(data[..4].Span);
            data = data[4..];
            return Unsafe.As<uint, T>(ref value);
        }

        if (type == typeof(int))
        {
            var value = BinaryPrimitives.ReadInt32LittleEndian(data[..4].Span);
            data = data[4..];
            return Unsafe.As<int, T>(ref value);
        }

        if (type == typeof(float))
        {
            var value = BinaryPrimitives.ReadSingleLittleEndian(data[..4].Span);
            data = data[4..];
            return Unsafe.As<float, T>(ref value);
        }

        if (type == typeof(ulong))
        {
            var value = BinaryPrimitives.ReadUInt64LittleEndian(data[..8].Span);
            data = data[8..];
            return Unsafe.As<ulong, T>(ref value);
        }

        if (type == typeof(long))
        {
            var value = BinaryPrimitives.ReadInt64LittleEndian(data[..8].Span);
            data = data[8..];
            return Unsafe.As<long, T>(ref value);
        }

        if (type == typeof(double))
        {
            var value = BinaryPrimitives.ReadDoubleLittleEndian(data[..8].Span);
            data = data[8..];
            return Unsafe.As<double, T>(ref value);
        }

        if (type.IsEnum)
        {
            // An enum on the wire is just its underlying integer, and every enum's size is its
            // underlying type's size, so the value can be copied straight over the local. The
            // previous path boxed the underlying read and went through Enum.ToObject for it.
            var size = Unsafe.SizeOf<T>();
            var value = default(T);
            data[..size].Span.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref value), size));
            data = data[size..];
            return value;
        }

        return (T)Read(ref data, type);
    }

    public static object ReadPrimitive(ref ReadOnlyMemory<byte> data, Type type)
    {
        ReadOnlySpan<byte> span;

        if (typeof(byte) == type)
        {
            span = data[..1].Span;
            data = data[1..];
            return span[0];
        }

        if (typeof(char) == type)
        {
            span = data[..1].Span;
            data = data[1..];
            // Same mapping Encoding.ASCII.GetChars(span.ToArray())[0] performed (byte values above
            // ASCII are the replacement character), without the array copy and char[] per read.
            return span[0] <= 0x7F ? (char)span[0] : '?';
        }

        if (typeof(short) == type)
        {
            span = data[..2].Span;
            data = data[2..];
            return BinaryPrimitives.ReadInt16LittleEndian(span);
        }

        if (typeof(ushort) == type)
        {
            span = data[..2].Span;
            data = data[2..];
            return BinaryPrimitives.ReadUInt16LittleEndian(span);
        }

        if (typeof(int) == type)
        {
            span = data[..4].Span;
            data = data[4..];
            return BinaryPrimitives.ReadInt32LittleEndian(span);
        }

        if (typeof(uint) == type)
        {
            span = data[..4].Span;
            data = data[4..];
            return BinaryPrimitives.ReadUInt32LittleEndian(span);
        }

        if (typeof(long) == type)
        {
            span = data[..8].Span;
            data = data[8..];
            return BinaryPrimitives.ReadInt64LittleEndian(span);
        }

        if (typeof(ulong) == type)
        {
            span = data[..8].Span;
            data = data[8..];
            return BinaryPrimitives.ReadUInt64LittleEndian(span);
        }

        if (typeof(Half) == type)
        {
            span = data[..2].Span;
            data = data[2..];
            return BinaryPrimitives.ReadUInt16LittleEndian(span);
        }

        if (typeof(float) == type)
        {
            span = data[..4].Span;
            data = data[4..];
            return MemoryMarshal.Cast<byte, float>(span)[0];
        }

        if (typeof(double) == type)
        {
            span = data[..8].Span;
            data = data[8..];
            return MemoryMarshal.Cast<byte, double>(span)[0];
        }

        throw new Exception();
    }

    private static unsafe object Read(ref ReadOnlyMemory<byte> data, Type type, IEnumerable<Attribute> attributes = null)
    {
        // Wire primitives carry no serializer metadata of their own: LengthPrefixed, Length,
        // Padding and ExistsPrefix live on the *fields* being read and reach this method through
        // the explicit attributes parameter. With no field attributes in play (list elements,
        // the underlying type of an enum, the header reads), fetching the value type's own
        // attributes — and then scanning the result four times — was pure per-packet cost.
        if (attributes is null && (type.IsPrimitive || type == typeof(Half)))
        {
            return ReadPrimitive(ref data, type);
        }

        attributes = attributes?.ToList() ?? type.GetCustomAttributes();

        var prefixLength = attributes.FirstOrDefault(a => a is LengthPrefixedAttribute) as LengthPrefixedAttribute;
        var length = attributes.FirstOrDefault(a => a is LengthAttribute) as LengthAttribute;
        var padding = attributes.FirstOrDefault(a => a is PaddingAttribute) as PaddingAttribute;
        var exists = attributes.FirstOrDefault(a => a is ExistsPrefixAttribute) as ExistsPrefixAttribute;

        object ret = null;

        if (padding != null)
        {
            data = data[padding.Size..];
        }

        if (exists != null)
        {
            if (Read(ref data, exists.ExistsType) != exists.TrueValue)
            {
                return null;
            }

            data = data[Marshal.SizeOf(exists.ExistsType)..];
        }

        if (typeof(IEnumerable).IsAssignableFrom(type) && type.GenericTypeArguments is { Length: > 0 })
        {
            var l = 0;
            if (prefixLength != null)
            {
                l = (int)Convert.ChangeType(Read(ref data, prefixLength.LengthType), typeof(int));
                data = data[Marshal.SizeOf(prefixLength.LengthType)..];
            }
            else if (length != null)
            {
                l = length.Length;
            }
            else
            {
                throw new Exception();
            }

            var tempRet = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type.GenericTypeArguments));
            for (var i = 0; i < l; i++)
            {
                _ = tempRet.Add(Read(ref data, type.GenericTypeArguments[0]));
            }

            ret = tempRet;
        }
        else if (typeof(string) == type)
        {
            var l = 0;

            while (l < data.Length && data.Span[l] != 0x00)
            {
                l++;
            }

            l++; // null terminator

            ret = Encoding.ASCII.GetString(data[..(l - 1)].Span.ToArray());
            data = data[l..];
        }
        else if (type.IsClass)
        {
            ret = ReadClass(ref data, type);
        }
        else if (type.IsPrimitive || typeof(Half) == type)
        {
            ret = ReadPrimitive(ref data, type);
        }
        else if (type.IsValueType && type.BaseType == typeof(Enum))
        {
            ret = Enum.ToObject(type, Read(ref data, Enum.GetUnderlyingType(type)));
        }
        else if (type.IsValueType)
        {
            var size = Marshal.SizeOf(type);
            var memoryHandle = data[..size].Pin();

            ret = Marshal.PtrToStructure(new IntPtr(memoryHandle.Pointer), type);
            data = data[size..];

            memoryHandle.Dispose();
        }

        return ret;
    }

    private static object ReadClass(ref ReadOnlyMemory<byte> data, Type type)
    {
        var properties = from property in type.GetFields()
                         where Attribute.IsDefined(property, typeof(FieldAttribute))
                         orderby ((FieldAttribute)property
                                                  .GetCustomAttributes(typeof(FieldAttribute), false)
                                                  .Single()).Order
                         select property;

        var ret = Activator.CreateInstance(type);
        foreach (var property in properties)
        {
            var attrs = property.GetCustomAttributes();
            var value = Read(ref data, property.FieldType, attrs);

            property.SetValue(ret, value);
        }

        return ret;
    }
}