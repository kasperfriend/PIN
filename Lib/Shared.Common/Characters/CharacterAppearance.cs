using System.Collections.Generic;

namespace Shared.Common.Characters;

/// <summary>
/// How the character's appearance is presented on every surface that renders
/// it, so the character selection screen and the in-game character can never
/// diverge ("get what you select").
/// </summary>
public static class CharacterAppearance
{
    /// <summary>
    /// The head accessory meshes a character wears, in wear order: the hair mesh
    /// first, then the facial hair mesh, then any extra accessories (zeros and
    /// duplicates removed).
    ///
    /// In the original service's data the hair meshes ARE the leading head
    /// accessories — a real captured character carries <c>hair == head_accessories[0]</c>
    /// and <c>facial_hair == head_accessories[1]</c>. The game client takes the
    /// hair mesh from the accessory list it is sent in-game, while the selection
    /// screen reads the dedicated <c>hair</c>/<c>facial_hair</c> fields, so both
    /// surfaces have to be fed from this one ordering.
    /// </summary>
    public static IReadOnlyList<uint> HeadAccessoryMeshes(CharacterVisualsRecord visuals)
    {
        var meshes = new List<uint>();
        AddMesh(meshes, visuals.Hair);
        AddMesh(meshes, visuals.FacialHair);

        foreach (var accessory in visuals.HeadAccessories)
        {
            AddMesh(meshes, accessory);
        }

        return meshes;
    }

    private static void AddMesh(List<uint> meshes, uint mesh)
    {
        if (mesh != 0 && !meshes.Contains(mesh))
        {
            meshes.Add(mesh);
        }
    }
}
