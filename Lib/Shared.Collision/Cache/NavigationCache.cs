using System.Numerics;
using Serilog;
using Shared.Collision.Navigation;

namespace Shared.Collision.Cache;

/// <summary>Small sidecar cache for collision-derived navigation triangles.</summary>
public static class NavigationCache
{
    private const int FormatVersion = 1;
    private static readonly byte[] Magic = "PCNV"u8.ToArray();
    private static readonly ILogger Logger = Log.ForContext(typeof(NavigationCache));

    public static string GetCachePath(string cacheDir, string chunkName)
    {
        var dir = Path.Combine(cacheDir, "navigation");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{chunkName}.navcache");
    }

    public static void Save(IReadOnlyList<NavigationTriangle> triangles, string path)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write(FormatVersion);
        writer.Write(triangles.Count);
        foreach (var triangle in triangles)
        {
            WriteVector(writer, triangle.A);
            WriteVector(writer, triangle.B);
            WriteVector(writer, triangle.C);
            writer.Write(triangle.PhysicsMaterialId);
        }
    }

    public static bool TryLoad(string path, out NavigationTriangle[] triangles)
    {
        triangles = [];
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            using var reader = new BinaryReader(stream);
            if (!reader.ReadBytes(4).SequenceEqual(Magic) || reader.ReadInt32() != FormatVersion)
            {
                return false;
            }

            int count = reader.ReadInt32();
            if (count < 0 || count > 100_000_000)
            {
                return false;
            }

            var result = new NavigationTriangle[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = new NavigationTriangle(
                    ReadVector(reader),
                    ReadVector(reader),
                    ReadVector(reader),
                    reader.ReadUInt32());
            }

            triangles = result;
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to load navigation cache {Path}", path);
            return false;
        }
    }

    private static void WriteVector(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static Vector3 ReadVector(BinaryReader reader)
    {
        return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
}
