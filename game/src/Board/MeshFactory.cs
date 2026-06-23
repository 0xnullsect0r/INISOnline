using Godot;
using INISOnline.Theme;
using Inis.Core.Model;

namespace INISOnline.Board;

/// <summary>
/// Builds the low-poly 3D meshes for the 2.5D board: flat hexagonal tile prisms (textured with
/// the tile SVGs) and the standing clan / building pieces. Kept simple and procedural so the
/// look is consistent and needs no external mesh assets.
/// </summary>
public static class MeshFactory
{
    public const float HexRadius = 1.0f;
    public const float HexHeight = 0.22f;

    /// <summary>A flat hexagon prism (radial segments = 6) lying in the XZ plane.</summary>
    public static CylinderMesh HexTile() => new()
    {
        TopRadius = HexRadius,
        BottomRadius = HexRadius,
        Height = HexHeight,
        RadialSegments = 6,
        Rings = 1,
    };

    public static StandardMaterial3D TileMaterial(Texture2D? texture, Color tint)
    {
        var mat = new StandardMaterial3D
        {
            AlbedoColor = tint,
            Roughness = 0.85f,
            Metallic = 0.0f,
        };
        if (texture is not null) mat.AlbedoTexture = texture;
        return mat;
    }

    public static StandardMaterial3D PieceMaterial(Color color, float emission = 0f)
    {
        var mat = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.55f,
            Metallic = 0.1f,
        };
        if (emission > 0f)
        {
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = emission;
        }
        return mat;
    }

    /// <summary>A small standing clan figure (a tapered post).</summary>
    public static MeshInstance3D Clan(Color color)
    {
        var mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.12f, Height = 0.45f, RadialSegments = 8 };
        return Piece(mesh, color, yOffset: 0.45f / 2f);
    }

    /// <summary>A sanctuary: a small four-sided pyramid.</summary>
    public static MeshInstance3D Sanctuary()
    {
        var mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.28f, Height = 0.5f, RadialSegments = 4 };
        return Piece(mesh, Palette.Cream, yOffset: 0.25f);
    }

    /// <summary>A citadel: a short square tower.</summary>
    public static MeshInstance3D Citadel()
    {
        var mesh = new BoxMesh { Size = new Vector3(0.42f, 0.5f, 0.42f) };
        return Piece(mesh, Palette.Bronze, yOffset: 0.25f);
    }

    /// <summary>The capital: a taller gold tower.</summary>
    public static MeshInstance3D Capital()
    {
        var mesh = new BoxMesh { Size = new Vector3(0.5f, 0.8f, 0.5f) };
        return Piece(mesh, Palette.Gold, yOffset: 0.4f);
    }

    private static MeshInstance3D Piece(Mesh mesh, Color color, float yOffset)
    {
        var node = new MeshInstance3D { Mesh = mesh, MaterialOverride = PieceMaterial(color) };
        node.Position = new Vector3(0, yOffset, 0);
        return node;
    }

    /// <summary>A muted terrain tint used when a tile texture is unavailable.</summary>
    public static Color TerrainTint(TerrainType terrain) => terrain switch
    {
        TerrainType.Plains => Color.FromHtml("8Fae6b"),
        TerrainType.Forest => Color.FromHtml("4f7a4a"),
        TerrainType.Mountain => Color.FromHtml("9a948c"),
        TerrainType.Bog => Color.FromHtml("6f6a4d"),
        TerrainType.Coast => Color.FromHtml("6fa3b0"),
        _ => Palette.ParchmentDark,
    };
}
