using System.Numerics;
using Shared.Collision.Tagfile;
using Shared.Collision.Tagfile.Models;

namespace Shared.Collision.Navigation;

/// <summary>
///     Extracts the mesh surfaces that the collision loader feeds to Bepu while retaining their
///     source material ids. This is deliberately separate from the physics-shape builder: physics
///     needs a fast opaque shape, while navigation needs walkable normals, triangle adjacency and
///     material costs.
/// </summary>
public static class NavigationGeometryExtractor
{
    public static IReadOnlyList<NavigationTriangle> Extract(
        TagfileAsset asset,
        BaseTagfileObject root,
        IReadOnlyList<uint> physicsMaterialIds)
    {
        var result = new List<NavigationTriangle>();
        Visit(asset, root, Matrix4x4.Identity, physicsMaterialIds, result);
        return result;
    }

    private static void Visit(
        TagfileAsset asset,
        BaseTagfileObject? obj,
        Matrix4x4 parent,
        IReadOnlyList<uint> physicsMaterialIds,
        List<NavigationTriangle> result)
    {
        if (obj == null)
        {
            return;
        }

        switch (obj)
        {
            case HkRootLevelContainerObject root:
                foreach (var variant in root.NamedVariants)
                {
                    Visit(asset, Get(asset, variant.Variant), parent, physicsMaterialIds, result);
                }

                break;

            case HkpRigidBody rigidBody:
                Visit(asset, Get(asset, rigidBody.Collidable.Shape),
                    parent * RigidBodyTransform(rigidBody.Motion.Transform),
                    physicsMaterialIds, result);
                break;

            case HkpListShapeObject list:
                foreach (var child in list.ChildInfo)
                {
                    Visit(asset, Get(asset, child.Shape), parent, physicsMaterialIds, result);
                }

                break;

            case HkpMoppBvTreeShapeObject mopp:
                Visit(asset, Get(asset, mopp.Child), parent, physicsMaterialIds, result);
                break;

            case HkpConvexTranslateShapeObject translated:
                Visit(asset, Get(asset, translated.ChildShape),
                    parent * Matrix4x4.CreateTranslation(ToVector3(translated.Translation)),
                    physicsMaterialIds, result);
                break;

            case HkpTransformShapeObject transformed:
                Visit(asset, Get(asset, transformed.ChildShape),
                    parent * TransformShapeMatrix(transformed.Rotation, transformed.Transform),
                    physicsMaterialIds, result);
                break;

            case HkpConvexTransformShapeObject convexTransformed:
                Visit(asset, Get(asset, convexTransformed.ChildShape),
                    parent * TransformMatrix(convexTransformed.Transform),
                    physicsMaterialIds, result);
                break;

            case HkpStorageExtendedMeshShape storage:
                ExtractStorageMesh(asset, storage, parent, physicsMaterialIds, result);
                foreach (var shapePart in storage.ShapesSubparts)
                {
                    foreach (var child in shapePart.ChildShapes)
                    {
                        Visit(asset, Get(asset, child), parent, physicsMaterialIds, result);
                    }
                }

                break;

            case HkpExtendedMeshShapeObject extended:
                ExtractExtendedMesh(asset, extended, parent, physicsMaterialIds, result);
                foreach (var shapePart in extended.ShapesSubparts)
                {
                    foreach (var child in shapePart.ChildShapes)
                    {
                        Visit(asset, Get(asset, child), parent, physicsMaterialIds, result);
                    }
                }

                break;

            case HkpSimpleMeshShape simple:
                ExtractSimpleMesh(simple, parent, physicsMaterialIds, result);
                break;
        }
    }

    private static void ExtractStorageMesh(
        TagfileAsset asset,
        HkpStorageExtendedMeshShape mesh,
        Matrix4x4 parent,
        IReadOnlyList<uint> physicsMaterialIds,
        List<NavigationTriangle> result)
    {
        for (int partIndex = 0; partIndex < mesh.TrianglesSubparts.Length; partIndex++)
        {
            var part = mesh.TrianglesSubparts[partIndex];
            if (partIndex >= mesh.Meshstorage.Length ||
                Get(asset, mesh.Meshstorage[partIndex]) is not HkpStorageExtendedMeshShapeMeshSubpartStorage storage)
            {
                continue;
            }

            var vertices = new Vector3[storage.Vertices.Length];
            for (int i = 0; i < storage.Vertices.Length; i++)
            {
                vertices[i] = ToVector3(storage.Vertices[i]);
            }

            var indices = storage.Indices16.Length > 0
                ? storage.Indices16
                : storage.Indices32.Length > 0
                    ? storage.Indices32
                    : storage.Indices8;
            int stride = (int)(part.IndexStriding > 0 ? part.IndexStriding : 4);
            int triangleCount = indices.Length / stride;
            var local = MeshTransform(part.Transform) * parent;

            for (int triangle = 0; triangle < triangleCount; triangle++)
            {
                int offset = triangle * stride;
                if (offset + 2 >= indices.Length ||
                    indices[offset] >= vertices.Length ||
                    indices[offset + 1] >= vertices.Length ||
                    indices[offset + 2] >= vertices.Length)
                {
                    continue;
                }

                uint materialIndex = triangle < storage.MaterialIndices.Length
                    ? storage.MaterialIndices[triangle]
                    : (uint)partIndex;
                result.Add(new NavigationTriangle(
                    Vector3.Transform(vertices[indices[offset + 2]], local),
                    Vector3.Transform(vertices[indices[offset + 1]], local),
                    Vector3.Transform(vertices[indices[offset]], local),
                    ResolveMaterial(materialIndex, partIndex, physicsMaterialIds)));
            }
        }
    }

    private static void ExtractExtendedMesh(
        TagfileAsset asset,
        HkpExtendedMeshShapeObject mesh,
        Matrix4x4 parent,
        IReadOnlyList<uint> physicsMaterialIds,
        List<NavigationTriangle> result)
    {
        for (int partIndex = 0; partIndex < mesh.TrianglesSubparts.Length; partIndex++)
        {
            var part = mesh.TrianglesSubparts[partIndex];
            int blockIndex = (ushort)(part.UserData & 0xFFFF);
            if (blockIndex >= asset.VertBlocks.Length || blockIndex >= asset.IndiceBlocks.Length)
            {
                continue;
            }

            var vertices = asset.VertBlocks[blockIndex].Verts;
            var triangles = asset.IndiceBlocks[blockIndex].Indices;
            var local = MeshTransform(part.Transform) * parent;
            uint materialId = ResolveMaterial((uint)partIndex, partIndex, physicsMaterialIds);

            foreach (var indices in triangles)
            {
                if (indices.Length < 3 ||
                    indices[0] >= vertices.Length ||
                    indices[1] >= vertices.Length ||
                    indices[2] >= vertices.Length)
                {
                    continue;
                }

                result.Add(new NavigationTriangle(
                    Vector3.Transform(vertices[indices[2]], local),
                    Vector3.Transform(vertices[indices[1]], local),
                    Vector3.Transform(vertices[indices[0]], local),
                    materialId));
            }
        }
    }

    private static void ExtractSimpleMesh(
        HkpSimpleMeshShape mesh,
        Matrix4x4 parent,
        IReadOnlyList<uint> physicsMaterialIds,
        List<NavigationTriangle> result)
    {
        var vertices = mesh.Vertices.Select(ToVector3).ToArray();
        for (int i = 0; i < mesh.Triangles.Length; i++)
        {
            var triangle = mesh.Triangles[i];
            if (triangle.A >= vertices.Length || triangle.B >= vertices.Length || triangle.C >= vertices.Length)
            {
                continue;
            }

            uint materialIndex = i < mesh.MaterialIndices.Length ? mesh.MaterialIndices[i] : 0;
            result.Add(new NavigationTriangle(
                Vector3.Transform(vertices[triangle.C], parent),
                Vector3.Transform(vertices[triangle.B], parent),
                Vector3.Transform(vertices[triangle.A], parent),
                ResolveMaterial(materialIndex, 0, physicsMaterialIds)));
        }
    }

    private static BaseTagfileObject? Get(TagfileAsset asset, string name)
    {
        return string.IsNullOrEmpty(name) ? null : asset.TagfileObjects.GetValueOrDefault(name);
    }

    private static Vector3 ToVector3(Vector4 value) => new(value.X, value.Y, value.Z);

    private static uint ResolveMaterial(uint materialIndex, int fallbackIndex, IReadOnlyList<uint> ids)
    {
        if (materialIndex < ids.Count)
        {
            return ids[(int)materialIndex];
        }

        if (ids.Contains(materialIndex))
        {
            return materialIndex;
        }

        return fallbackIndex < ids.Count ? ids[fallbackIndex] : ids.Count > 0 ? ids[0] : 0;
    }

    private static Matrix4x4 MeshTransform(Vector4[] transform)
    {
        if (transform.Length < 3)
        {
            return Matrix4x4.Identity;
        }

        var rotation = new Quaternion(transform[1].X, transform[1].Y, transform[1].Z, transform[1].W);
        if (rotation.LengthSquared() < 0.000001f)
        {
            rotation = Quaternion.Identity;
        }
        else
        {
            rotation = Quaternion.Normalize(rotation);
        }

        var scale = ToVector3(transform[2]);
        if (scale.LengthSquared() < 0.000001f)
        {
            scale = Vector3.One;
        }

        var position = ToVector3(transform[0]);
        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
    }

    private static Matrix4x4 RigidBodyTransform(Vector4[] transform)
    {
        if (transform.Length < 4)
        {
            return Matrix4x4.Identity;
        }

        var matrix = new Matrix4x4(
            transform[0].X, transform[0].Y, transform[0].Z, 0f,
            transform[1].X, transform[1].Y, transform[1].Z, 0f,
            transform[2].X, transform[2].Y, transform[2].Z, 0f,
            0f, 0f, 0f, 1f);
        var rotation = Quaternion.CreateFromRotationMatrix(matrix);
        return Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(ToVector3(transform[3]));
    }

    private static Matrix4x4 TransformShapeMatrix(Vector4 rotation, Vector4[] transform)
    {
        var quaternion = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
        if (quaternion.LengthSquared() < 0.000001f)
        {
            quaternion = Quaternion.Identity;
        }
        else
        {
            quaternion = Quaternion.Normalize(quaternion);
        }

        var position = transform.Length >= 4 ? ToVector3(transform[3]) : Vector3.Zero;
        return Matrix4x4.CreateFromQuaternion(quaternion) * Matrix4x4.CreateTranslation(position);
    }

    private static Matrix4x4 TransformMatrix(Vector4[] transform)
    {
        if (transform.Length < 4)
        {
            return Matrix4x4.Identity;
        }

        return new Matrix4x4(
            transform[0].X, transform[0].Y, transform[0].Z, 0f,
            transform[1].X, transform[1].Y, transform[1].Z, 0f,
            transform[2].X, transform[2].Y, transform[2].Z, 0f,
            transform[3].X, transform[3].Y, transform[3].Z, 1f);
    }
}
