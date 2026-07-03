using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using INISOnline.Theme;
using Inis.Core.Data;
using Inis.Core.Model;

namespace INISOnline.Board;

/// <summary>
/// The 2.5D (RISK-style) board: a 3D scene viewed from a tilted, orbitable camera, rendered into
/// a SubViewport so the 2D HUD overlays on top. Territories are flat textured hex tiles; clans and
/// buildings are low-poly meshes standing on them. Tiles are pickable (raycast) so the HUD can use
/// a clicked territory as a card target. See docs/design.md.
/// </summary>
public partial class BoardView : SubViewportContainer
{
    [Signal] public delegate void TerritoryPickedEventHandler(string instanceId);

    private readonly GameData _data = GameData.Default;
    private SubViewport _viewport = null!;
    private Node3D _world = null!;
    private Node3D _yaw = null!;
    private Node3D _pitch = null!;
    private Camera3D _camera = null!;

    private readonly Dictionary<string, Node3D> _tiles = new();
    private readonly Dictionary<string, StandardMaterial3D> _tileMaterials = new();
    private Node3D _pieces = null!;
    private string? _selected;

    // Camera state.
    private float _distance = 13f;
    private float _yawDeg;
    private float _pitchDeg = -55f;
    private Vector2 _pressPos;
    private bool _dragged;

    public override void _Ready()
    {
        Stretch = true;
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        _viewport = new SubViewport
        {
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            HandleInputLocally = false,
            Msaa3D = Viewport.Msaa.Msaa4X,
        };
        AddChild(_viewport);

        _world = new Node3D();
        _viewport.AddChild(_world);

        SetupEnvironment();
        SetupCamera();

        _pieces = new Node3D();
        _world.AddChild(_pieces);
    }

    private void SetupEnvironment()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Palette.Slate,
            AmbientLightColor = new Color(0.6f, 0.6f, 0.65f),
            AmbientLightEnergy = 0.6f,
        };
        var worldEnv = new WorldEnvironment { Environment = env };
        _world.AddChild(worldEnv);

        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55, -40, 0),
            LightEnergy = 1.1f,
            ShadowEnabled = true,
        };
        _world.AddChild(sun);
    }

    private void SetupCamera()
    {
        _yaw = new Node3D();
        _world.AddChild(_yaw);
        _pitch = new Node3D();
        _yaw.AddChild(_pitch);
        _camera = new Camera3D { Position = new Vector3(0, 0, _distance), Fov = 50 };
        _pitch.AddChild(_camera);
        ApplyCamera();
    }

    private void ApplyCamera()
    {
        _yaw.RotationDegrees = new Vector3(0, _yawDeg, 0);
        _pitch.RotationDegrees = new Vector3(_pitchDeg, 0, 0);
        _camera.Position = new Vector3(0, 0, _distance);
    }

    // --------------------------------------------------------------- board build

    /// <summary>Builds tiles (including any explored mid-game), then refreshes pieces + highlights.</summary>
    public void Sync(GameState state)
    {
        if (_tiles.Count == 0) BuildTiles(state);
        else AddNewTiles(state);
        RefreshPieces(state);
    }

    private void BuildTiles(GameState state)
    {
        var ids = new List<string>(state.Territories.Keys);
        ids.Sort(StringComparer.Ordinal);
        var n = Math.Max(ids.Count, 1);
        var ringRadius = n <= 2 ? 1.6f : 1.1f * n / Mathf.Pi + 1.4f;

        for (var i = 0; i < ids.Count; i++)
        {
            var territory = state.Territories[ids[i]];
            var angle = Mathf.Tau * i / n;
            var pos = new Vector3(Mathf.Cos(angle) * ringRadius, 0, Mathf.Sin(angle) * ringRadius);
            _tiles[territory.InstanceId] = BuildTile(territory, pos);
        }
    }

    /// <summary>Territories discovered mid-game (Exploration, Tailtu's Land) join the board.</summary>
    private void AddNewTiles(GameState state)
    {
        foreach (var territory in state.Territories.Values)
        {
            if (_tiles.ContainsKey(territory.InstanceId)) continue;
            var def = _data.Territory(territory.DefinitionId);
            var index = _tiles.Count;
            // Golden-angle spiral outside the initial ring; islands sit even further out at sea.
            var angle = index * 2.39996f;
            var radius = (def.Island ? 3.4f : 2.2f) + 1.1f * index / Mathf.Pi;
            var pos = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            var tile = BuildTile(territory, pos);
            _tiles[territory.InstanceId] = tile;
            tile.Scale = new Vector3(0.05f, 0.05f, 0.05f);
            var tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(tile, "scale", Vector3.One, 0.45f);
        }
    }

    private Node3D BuildTile(TerritoryState territory, Vector3 pos)
    {
        var def = _data.Territory(territory.DefinitionId);
        var root = new Node3D { Position = pos };
        // Pointy hex tops read better when rotated half a step.
        root.RotationDegrees = new Vector3(0, 30, 0);

        var texture = LoadTileTexture(def);
        var material = MeshFactory.TileMaterial(texture, texture is null ? MeshFactory.TerrainTint(def.Terrain) : Colors.White);
        _tileMaterials[territory.InstanceId] = material;

        var mesh = new MeshInstance3D { Mesh = MeshFactory.HexTile(), MaterialOverride = material };
        root.AddChild(mesh);

        // Pickable body carrying the territory id.
        var body = new StaticBody3D();
        var shape = new CollisionShape3D { Shape = new CylinderShape3D { Radius = MeshFactory.HexRadius, Height = MeshFactory.HexHeight } };
        body.AddChild(shape);
        body.SetMeta("territory", territory.InstanceId);
        root.AddChild(body);

        _world.AddChild(root);
        return root;
    }

    private Texture2D? LoadTileTexture(TerritoryDefinition def)
    {
        if (def.Art is null) return null;
        var path = $"res://art/{def.Art}";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    private readonly Dictionary<string, string> _tileSignatures = new();

    private void RefreshPieces(GameState state)
    {
        foreach (var child in _pieces.GetChildren()) child.QueueFree();

        foreach (var (id, territory) in state.Territories)
        {
            if (!_tiles.TryGetValue(id, out var tile)) continue;
            var basePos = tile.Position;

            // Animate pieces only on tiles whose contents actually changed since last sync.
            var signature = TileSignature(territory);
            var changed = _tileSignatures.TryGetValue(id, out var prev) && prev != signature;
            _tileSignatures[id] = signature;

            // Buildings sit at the back of the tile; clans cluster at the front.
            var slot = 0;
            for (var s = 0; s < territory.Sanctuaries; s++) PlaceBuilding(MeshFactory.Sanctuary(), basePos, slot++, changed);
            if (territory.HasCapital) PlaceBuilding(MeshFactory.Capital(), basePos, slot++, changed);
            for (var c = 0; c < territory.Citadels; c++) PlaceBuilding(MeshFactory.Citadel(), basePos, slot++, changed);
            if (territory.HasHarbour) PlaceHarbour(basePos);

            var clanIndex = 0;
            foreach (var (color, count) in territory.Clans)
                for (var k = 0; k < count; k++)
                    PlaceClan(Palette.Clan(color), basePos, clanIndex++, changed);

            HighlightTile(id, id == _selected);
        }
    }

    private static string TileSignature(TerritoryState t) =>
        $"{t.Sanctuaries}:{t.Citadels}:{t.HasCapital}:{t.HasHarbour}:" +
        string.Join(",", t.Clans.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

    private void PlaceClan(Color color, Vector3 tilePos, int index, bool animate = false)
    {
        var node = MeshFactory.Clan(color);
        var offset = ClusterOffset(index, 0.30f, forward: 0.35f);
        node.Position = new Vector3(tilePos.X + offset.X, MeshFactory.HexHeight / 2f + node.Position.Y, tilePos.Z + offset.Y);
        _pieces.AddChild(node);
        if (animate) PopIn(node);
    }

    private void PlaceBuilding(MeshInstance3D node, Vector3 tilePos, int slot, bool animate = false)
    {
        var offset = ClusterOffset(slot, 0.34f, forward: -0.4f);
        node.Position = new Vector3(tilePos.X + offset.X, MeshFactory.HexHeight / 2f + node.Position.Y, tilePos.Z + offset.Y);
        _pieces.AddChild(node);
        if (animate) PopIn(node);
    }

    private void PlaceHarbour(Vector3 tilePos)
    {
        // A small azure pier disc at the tile's seaward edge marks a Harbour.
        var mesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.22f, Height = 0.06f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.35f, 0.65f, 0.8f),
                EmissionEnabled = true,
                Emission = new Color(0.2f, 0.45f, 0.6f),
                EmissionEnergyMultiplier = 0.3f,
            },
            Position = new Vector3(tilePos.X + 0.72f, MeshFactory.HexHeight / 2f, tilePos.Z + 0.55f),
        };
        _pieces.AddChild(mesh);
    }

    /// <summary>A quick scale-in so changed tiles visibly gain/lose their pieces.</summary>
    private void PopIn(Node3D node)
    {
        var target = node.Scale;
        node.Scale = target * 0.4f;
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(node, "scale", target, 0.22f);
    }

    private static Vector2 ClusterOffset(int index, float step, float forward)
    {
        var col = index % 3;
        var rowIdx = index / 3;
        return new Vector2((col - 1) * step, forward + rowIdx * step * 0.8f);
    }

    private void HighlightTile(string id, bool on)
    {
        if (!_tileMaterials.TryGetValue(id, out var mat)) return;
        mat.EmissionEnabled = on;
        if (on)
        {
            mat.Emission = Palette.GoldBright;
            mat.EmissionEnergyMultiplier = 0.5f;
        }
    }

    public void SetSelected(string? instanceId)
    {
        if (_selected is { } prev) HighlightTile(prev, false);
        _selected = instanceId;
        if (instanceId is not null) HighlightTile(instanceId, true);
    }

    // ------------------------------------------------------------------- input

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed:
                Zoom(-1f); AcceptEvent(); break;
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed:
                Zoom(1f); AcceptEvent(); break;
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left:
                if (mb.Pressed) { _pressPos = mb.Position; _dragged = false; }
                else if (!_dragged) PickAt(mb.Position);
                AcceptEvent();
                break;
            case InputEventMouseMotion motion:
                HandleMotion(motion); break;
        }
    }

    private void HandleMotion(InputEventMouseMotion motion)
    {
        if ((motion.ButtonMask & MouseButtonMask.Left) != 0)
        {
            if (motion.Position.DistanceTo(_pressPos) > 6) _dragged = true;
            _yawDeg -= motion.Relative.X * 0.4f;
            _pitchDeg = Mathf.Clamp(_pitchDeg - motion.Relative.Y * 0.3f, -80f, -20f);
            ApplyCamera();
            AcceptEvent();
        }
        else if ((motion.ButtonMask & MouseButtonMask.Right) != 0)
        {
            // Pan the pivot in the board plane, relative to the current yaw.
            var yaw = Mathf.DegToRad(_yawDeg);
            var right = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw));
            var fwd = new Vector3(-Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
            _yaw.Position += (right * -motion.Relative.X + fwd * -motion.Relative.Y) * 0.01f * _distance;
            AcceptEvent();
        }
    }

    private void Zoom(float steps)
    {
        _distance = Mathf.Clamp(_distance + steps * 1.2f, 5f, 28f);
        ApplyCamera();
    }

    private void PickAt(Vector2 localPos)
    {
        if (_camera is null) return;
        var from = _camera.ProjectRayOrigin(localPos);
        var to = from + _camera.ProjectRayNormal(localPos) * 1000f;
        var space = _camera.GetWorld3D().DirectSpaceState;
        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));
        if (hit.Count == 0 || !hit.TryGetValue("collider", out var collider)) return;
        if (collider.As<Node>() is { } node && node.HasMeta("territory"))
            EmitSignal(SignalName.TerritoryPicked, node.GetMeta("territory").AsString());
    }
}
