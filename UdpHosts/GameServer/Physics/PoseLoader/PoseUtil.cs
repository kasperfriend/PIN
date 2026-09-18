#nullable enable

using System;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace GameServer.Physics.PoseLoader;

public class PoseUtil
{
    public static Vector3 ParseVector3(string input)
    {
        var cleaned = input.Trim('<', '>', ' ');
        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 3)
        {
            throw new FormatException($"Invalid Vector3 format: {input}");
        }

        return new Vector3(
            ParseComponent(parts[0], input),
            ParseComponent(parts[1], input),
            ParseComponent(parts[2], input));
    }

    /// <summary>
    ///     Parses one component of a vector, or throws for the whole vector naming the component that
    ///     is not a number.
    /// </summary>
    private static float ParseComponent(string part, string wholeVector) =>
        TryParseNumber(part, out var value)
            ? value
            : throw new FormatException($"Invalid Vector3 component '{part}' in: {wholeVector}");

    /// <summary>
    ///     Reads one number as the archive writes them, with a single allowance its own data
    ///     requires: a few values carry a trailing unit letter, so the shipped pose 00189610 has an
    ///     origin of <c>&lt;2.1t 0 0&gt;</c> - and every load of that file threw, so the row fell back
    ///     to a generic body shape (and, before the loader learned to remember failures, paid a file
    ///     read, a parse and a logged exception per entity per tick for it). A trailing run of
    ///     letters is dropped and the number in front of it is read; anything that is still not a
    ///     number is refused, exactly as it was before.
    /// </summary>
    private static bool TryParseNumber(string? part, out float value)
    {
        value = 0f;

        if (string.IsNullOrEmpty(part))
        {
            return false;
        }

        if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        int end = part.Length;
        while (end > 0 && char.IsLetter(part[end - 1]))
        {
            end--;
        }

        return end > 0 && end != part.Length &&
               float.TryParse(part[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public static Quaternion ParseRotation(string input)
    {
        var vectors = input.Split(['>'], StringSplitOptions.RemoveEmptyEntries)
                           .Select(v => ParseVector3(v + ">")) // Add back '>' so it parses correctly
                           .ToArray();

        if (vectors.Length != 3)
        {
            throw new FormatException($"Invalid Matrix3x3 format: {input}");
        }

        var matrix = new Matrix4x4(
            vectors[0][0],
            vectors[0][1],
            vectors[0][2],
            0,
            vectors[1][0],
            vectors[1][1],
            vectors[1][2],
            0,
            vectors[2][0],
            vectors[2][1],
            vectors[2][2],
            0,
            0,
            0,
            0,
            0);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(matrix));
    }

    public static float? TryParseFloat(string? input)
    {
        return TryParseNumber(input?.Trim('"'), out var result) ? result : null;
    }

    public static int? TryParseInt(string? input)
    {
        if (int.TryParse(input?.Trim('"'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return null;
    }
}