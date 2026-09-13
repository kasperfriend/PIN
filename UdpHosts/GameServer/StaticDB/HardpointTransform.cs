using System;
using System.Collections;
using System.Numerics;

namespace GameServer.StaticDB;

/// <summary>
///     Translation of a <c>dbvisualrecords::Hardpoints.Transform</c> cell. The column is a
///     <c>HalfMatrix4x3</c> (12 half-floats, 4 columns of 3 rows); FauFau's C# type for that
///     cell is not a PIN record, so the loader stores the raw value and this helper reads the
///     fourth column as the muzzle offset <c>TurretWeapon.MuzzleHardpoint</c> is relative to.
/// </summary>
public static class HardpointTransform
{
    /// <summary>
    ///     World-local translation of <paramref name="transform" />, or <see cref="Vector3.Zero" />
    ///     when the cell is missing or not 12 numbers. The type name is 4x3, so the translation is
    ///     the last column (indices 9, 10, 11).
    /// </summary>
    public static Vector3 Translation(object transform)
    {
        if (transform == null)
        {
            return Vector3.Zero;
        }

        var values = ReadNumbers(transform);
        if (values is { Length: >= 12 })
        {
            return new Vector3(values[9], values[10], values[11]);
        }

        if (TryReadXyz(transform, out var xyz))
        {
            return xyz;
        }

        if (values is { Length: >= 3 })
        {
            return new Vector3(values[0], values[1], values[2]);
        }

        return Vector3.Zero;
    }

    /// <summary>Twelve-float identity of a <c>HalfMatrix4x3</c> with the given translation column.</summary>
    public static float[] MatrixWithTranslation(float x, float y, float z)
    {
        return
        [
            1f, 0f, 0f,
            0f, 1f, 0f,
            0f, 0f, 1f,
            x, y, z,
        ];
    }

    private static bool TryReadXyz(object transform, out Vector3 xyz)
    {
        xyz = Vector3.Zero;
        var type = transform.GetType();
        object x = ReadMember(transform, type, "x") ?? ReadMember(transform, type, "X");
        object y = ReadMember(transform, type, "y") ?? ReadMember(transform, type, "Y");
        object z = ReadMember(transform, type, "z") ?? ReadMember(transform, type, "Z");
        if (x == null || y == null || z == null)
        {
            return false;
        }

        xyz = new Vector3(ToFloat(x), ToFloat(y), ToFloat(z));
        return true;
    }

    private static object ReadMember(object target, Type type, string name)
    {
        var field = type.GetField(name);
        if (field != null)
        {
            return field.GetValue(target);
        }

        var property = type.GetProperty(name);
        return property?.GetValue(target);
    }

    private static float[] ReadNumbers(object transform)
    {
        if (transform is float[] floats)
        {
            return floats;
        }

        if (transform is not IEnumerable enumerable)
        {
            return null;
        }

        var list = new System.Collections.Generic.List<float>(12);
        foreach (var item in enumerable)
        {
            if (item == null)
            {
                continue;
            }

            list.Add(ToFloat(item));
        }

        return list.Count == 0 ? null : list.ToArray();
    }

    private static float ToFloat(object value)
    {
        return value switch
        {
            float f => f,
            double d => (float)d,
            Half h => (float)h,
            IConvertible convertible => Convert.ToSingle(convertible),
            _ => 0f,
        };
    }
}
