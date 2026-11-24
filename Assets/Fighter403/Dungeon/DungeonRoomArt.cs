using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]
#endif

namespace Fighter403
{
    internal sealed class DungeonRoomArt
    {
        public const string ResourceFolder = "Fighter403Dungeon";
        public const int PixelsPerUnit = 32;
        public const int Cell = 48;
        public const int WallFaceDepth = 33;
        private const string MaterialResource = ResourceFolder + "/DungeonSprite";
        private static readonly string[] Themes = { "Cache", "Firewall", "Core" };
        private static readonly string[] Groups = { "Floor", "Floor fixtures", "Room landmark", "Equipment", "Walls and gates" };
        private static readonly string[] FloorTiles = { "Floor0", "Floor1", "Floor2", "Floor3" };
        private static readonly string[] FloorBlocks = { "FloorBlock0", "FloorBlock1", "FloorBlock2", "FloorBlock3" };

        private static readonly Dictionary<string, Sprite>[] ThemeSprites = new Dictionary<string, Sprite>[Themes.Length];
        private static readonly Dictionary<(Sprite, RectInt), Sprite> CroppedSprites = new();
        private static Dictionary<string, Sprite> landmarks;
        private static Sprite[] signal;
        private static Sprite[] chestFrames, merchantFrames, merchantIdleFrames;
        private static Material material;
        private readonly Dictionary<string, Sprite> sprites;
        private readonly int theme;
        private readonly int columns, rows, seed;
        private readonly DungeonRoomKind role;
        private readonly DungeonDoorState[] states;
        public Vector2 HalfSize { get; }
        public static IReadOnlyList<Sprite> Signal => signal;

        public readonly struct Piece
        {
            public readonly Sprite Sprite;
            public readonly Vector2 Center;
            public readonly int Turn, Order, GateSide;
            public readonly RectInt Footprint;
            public readonly int Durability;

            public Piece(Sprite sprite, Vector2 center, int turn, int order, int gateSide = -1, RectInt footprint = default,
                int durability = 0)
            {
                Sprite = sprite;
                Center = center;
                Turn = turn;
                Order = order;
                GateSide = gateSide;
                Footprint = footprint;
                Durability = durability;
            }
        }

        public DungeonRoomArt(int theme, DungeonRoomKind role, int columns, int rows, int seed, DungeonDoorState[] states)
        {
            this.theme = theme;
            sprites = ThemeSprites[theme] ??= Load(Themes[theme]);
            landmarks ??= Load("Landmarks");
            signal ??= Load("DoorSignal").OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToArray();
            material = material != null ? material : Resources.Load<Material>(MaterialResource);
            this.role = role;
            this.columns = columns;
            this.rows = rows;
            this.seed = seed;
            this.states = states;
            HalfSize = new Vector2(columns, rows) * (Cell / (2f * PixelsPerUnit));
        }

        public bool Ready => sprites.Count == 20 && landmarks.Count == 9 && signal.Length == 12
            && material != null && DungeonRoomDressing.Ready;

        public static void Reload()
        {
            Array.Clear(ThemeSprites, 0, ThemeSprites.Length);
            foreach (var sprite in CroppedSprites.Values)
                if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
            CroppedSprites.Clear();
            landmarks = null;
            DungeonRoomDressing.Reload();
            signal = null;
            chestFrames = merchantFrames = merchantIdleFrames = null;
            material = null;
        }

        private static Dictionary<string, Sprite> Load(string atlas) =>
            Resources.LoadAll<Sprite>($"{ResourceFolder}/{atlas}").ToDictionary(sprite => sprite.name, StringComparer.Ordinal);

        public IEnumerable<Piece> Layout()
        {
            int width = columns * Cell;
            int height = rows * Cell;
            Piece At(string name, int x, int y, int order, int turn = 0, int gateSide = -1)
            {
                Sprite sprite = sprites[name];
                Vector2 size = sprite.rect.size;
                if ((turn & 1) != 0)
                    size = new Vector2(size.y, size.x);
                var origin = new Vector2(x < 0 ? width + x - size.x : x, y < 0 ? height + y - size.y : y);
                return new Piece(sprite, origin + size * .5f, turn, order, gateSide);
            }

            int FloorVariant(int x, int y, int span)
            {
                uint choice = unchecked((uint)(x * 17 ^ y * 23 ^ x * y * 3 ^ seed)) % 23;
                bool edge = x == 0 || x >= columns - span || y == 0 || y >= rows - span;
                return edge && choice > 10 ? 2 + (int)(choice % 2) : choice % 3 == 0 ? 1 : 0;
            }

            // Center on the logical grid so the opaque sprite borders overlap.
            Piece FloorAt(string name, int x, int y, int span) =>
                new(sprites[name], new Vector2(x + span * .5f, y + span * .5f) * Cell, 0, -20);

            // One renderer covers four floor cells. Single tiles fill odd room edges.
            int blockColumns = columns & ~1, blockRows = rows & ~1;
            for (int y = 0; y < blockRows; y += 2)
            for (int x = 0; x < blockColumns; x += 2)
                yield return FloorAt(FloorBlocks[FloorVariant(x, y, 2)], x, y, 2);
            for (int y = 0; y < blockRows; y++)
            for (int x = blockColumns; x < columns; x++)
                yield return FloorAt(FloorTiles[FloorVariant(x, y, 1)], x, y, 1);
            for (int y = blockRows; y < rows; y++)
            for (int x = 0; x < columns; x++)
                yield return FloorAt(FloorTiles[FloorVariant(x, y, 1)], x, y, 1);

            var size = new Vector2(width, height);
            var center = size * .5f;
            if (role == DungeonRoomKind.Boss) yield return new Piece(sprites["Arena"], center, 0, -19);
            foreach (var piece in DungeonRoomDressing.Place(theme, role, seed, size)) yield return piece;
            yield return new Piece(landmarks[role.ToString()], center, 0, -18);
            yield return At("Corner", 0, 0, -16);
            yield return At("Corner", width - Cell, 0, -16, 1);
            yield return At("Corner", width - Cell, height - Cell, -16, 2);
            yield return At("Corner", 0, height - Cell, -16, 3);

            for (int side = 0; side < 4; side++)
            {
                bool vertical = (side & 1) != 0;
                int length = vertical ? rows : columns;
                int doorCell = length / 2 - 1;
                DungeonDoorState state = states[side];
                for (int cell = 1; cell < length - 1; cell++)
                {
                    int x = vertical ? (side == 1 ? width - Cell : 0) : cell * Cell;
                    int y = vertical ? cell * Cell : (side == 2 ? height - Cell : 0);
                    if (state != DungeonDoorState.Wall && cell == doorCell)
                    {
                        yield return At(state.ToString(), x, y, -16, side, side);
                        cell++;
                    }
                    else
                    {
                        bool doorway = cell == doorCell || cell == doorCell + 1;
                        yield return At(!doorway && cell % 5 == 2 ? "Wall1" : "Wall0", x, y, -16, side);
                    }
                }
            }
        }

        public GameObject Create(string name, Vector2 position, DungeonRoom room, Action onBreach,
            SpriteRenderer playerSprite = null)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            var groups = new Transform[Groups.Length];
            foreach (Piece piece in Layout())
            {
                int index = piece.Order + 20;
                Transform group = groups[index];
                if (group == null)
                {
                    group = new GameObject(Groups[index]).transform;
                    group.SetParent(root.transform, false);
                    groups[index] = group;
                }
                var part = new GameObject(piece.Sprite.name);
                part.transform.SetParent(group, false);
                Vector2 offset = piece.Center / PixelsPerUnit - HalfSize;
                part.transform.localPosition = new Vector2(offset.x, -offset.y);
                part.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * piece.Turn);
                var renderer = part.AddComponent<SpriteRenderer>();
                renderer.sprite = piece.Sprite;
                renderer.sharedMaterial = material;
                renderer.sortingOrder = piece.Order;
                if (room != null)
                {
                    AddSolidProp(piece, renderer, playerSprite, room);
                    if (playerSprite != null) AddSouthWallForeground(piece, renderer, playerSprite.sortingOrder + 1);
                }
                if (room != null && piece.Order == -18 && DungeonRoomActor.HasProximityInteraction(role))
                {
                    var frames = role is DungeonRoomKind.Vault or DungeonRoomKind.Shop ? InteractionFrames(role) : null;
                    part.AddComponent<DungeonRoomActor>().Initialize(renderer, room, frames,
                        role == DungeonRoomKind.Shop ? merchantIdleFrames : null);
                }
                if (piece.GateSide < 0) continue;
                int side = piece.GateSide;
                var gate = part.AddComponent<DungeonGate>();
                int? foregroundOrder = room != null && playerSprite != null && side == 2
                    ? playerSprite.sortingOrder + 1 : null;
                gate.Initialize(states[side], room, room?.Doors[side], renderer, sprites["Open"], sprites["Breached"],
                    signal, onBreach, foregroundOrder);
            }

            if (room != null) AddBoundary(root.transform);
            return root;
        }

        private static void AddSolidProp(Piece piece, SpriteRenderer renderer, SpriteRenderer playerSprite, DungeonRoom room)
        {
            // Pixel footprints describe ground contact within the source sprites.
            RectInt footprint = piece.Sprite.name switch
            {
                "Vault" => new RectInt(41, 60, 62, 23),
                "Shop" => new RectInt(35, 61, 74, 24),
                "Maintenance" => new RectInt(34, 60, 76, 25),
                "Hidden" => new RectInt(45, 62, 54, 21),
                _ => piece.Footprint
            };
            if (footprint.width == 0) return;

            var solid = renderer.gameObject.AddComponent<BoxCollider2D>();
            solid.size = (Vector2)footprint.size / PixelsPerUnit;
            solid.offset = LocalPixelPosition(piece.Sprite, footprint.center);
            if (playerSprite != null)
                renderer.gameObject.AddComponent<DungeonPropDepth>().Initialize(renderer, playerSprite, solid.offset);
            if (piece.Durability <= 0) return;

            // The room seed reproduces this center for each prop on return, even for identical sprites.
            Vector2 center = piece.Center;
            int durability = room.PropDurability.TryGetValue(center, out int saved) ? saved : piece.Durability;
            renderer.gameObject.AddComponent<DungeonBreakableProp>().Initialize(renderer, solid, durability,
                remaining => room.PropDurability[center] = remaining, piece.Durability);
        }

        private static Vector2 LocalPixelPosition(Sprite sprite, Vector2 pixel) =>
            new Vector2(pixel.x - sprite.pivot.x, sprite.rect.height - sprite.pivot.y - pixel.y) / PixelsPerUnit;

        private static void AddSouthWallForeground(Piece piece, SpriteRenderer renderer, int sortingOrder)
        {
            RectInt face = (piece.Sprite.name, piece.Turn) switch
            {
                ("Wall0" or "Wall1" or "Corner", 2) => new RectInt(0, 0, Cell, WallFaceDepth),
                ("Corner", 3) => new RectInt(0, 0, WallFaceDepth, Cell),
                _ => default
            };
            if (face.width > 0) AddCroppedRenderer("South wall face", renderer, face, sortingOrder);
        }

        internal static SpriteRenderer AddCroppedRenderer(string name, SpriteRenderer parent, RectInt pixels, int order)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = LocalPixelPosition(parent.sprite, pixels.center);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = parent.sharedMaterial;
            renderer.sortingLayerID = parent.sortingLayerID;
            renderer.sortingOrder = order;
            renderer.sprite = CropSprite(parent.sprite, pixels);
            return renderer;
        }

        internal static Sprite CropSprite(Sprite source, RectInt pixels)
        {
            var key = (source, pixels);
            if (CroppedSprites.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;
            Rect rect = source.rect;
            var crop = new Rect(rect.x + pixels.x, rect.yMax - pixels.yMax, pixels.width, pixels.height);
            sprite = Sprite.Create(source.texture, crop, Vector2.one * .5f, PixelsPerUnit,
                0, SpriteMeshType.FullRect, Vector4.zero, false);
            sprite.name = source.name + " crop";
            CroppedSprites[key] = sprite;
            return sprite;
        }

        private static Sprite[] InteractionFrames(DungeonRoomKind kind)
        {
            if (chestFrames == null || merchantFrames == null)
            {
                var all = Resources.LoadAll<Sprite>(ResourceFolder + "/RoomInteraction");
                chestFrames = all.Where(sprite => sprite.name.StartsWith("Chest", StringComparison.Ordinal))
                    .OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
                merchantFrames = all.Where(sprite => sprite.name.StartsWith("Merchant", StringComparison.Ordinal) &&
                      !sprite.name.StartsWith("MerchantIdle", StringComparison.Ordinal))
                    .OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
                merchantIdleFrames = all.Where(sprite => sprite.name.StartsWith("MerchantIdle", StringComparison.Ordinal))
                    .OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
            }
            return kind == DungeonRoomKind.Vault ? chestFrames : merchantFrames;
        }

        private void AddBoundary(Transform root)
        {
            const float depth = (float)WallFaceDepth / PixelsPerUnit;
            const float aperture = DungeonGate.Aperture;
            for (int side = 0; side < 4; side++)
            {
                bool vertical = (side & 1) != 0;
                var outward = (Vector2)DungeonGenerator.Directions[side];
                var tangent = new Vector2(outward.y, -outward.x);
                float extent = vertical ? HalfSize.y : HalfSize.x;
                float normal = vertical ? HalfSize.x : HalfSize.y;
                Vector2 line = outward * (normal - depth * .5f);
                if (states[side] == DungeonDoorState.Wall)
                {
                    AddWall(line, 2f * extent, vertical);
                    continue;
                }
                float midpoint = (extent + aperture * .5f) * .5f;
                AddWall(line - tangent * midpoint, extent - aperture * .5f, vertical);
                AddWall(line + tangent * midpoint, extent - aperture * .5f, vertical);
            }

            void AddWall(Vector2 center, float length, bool vertical)
            {
                var wall = new GameObject("Wall collider");
                wall.transform.SetParent(root, false);
                wall.transform.localPosition = center;
                wall.AddComponent<BoxCollider2D>().size = vertical ? new Vector2(depth, length) : new Vector2(length, depth);
            }
        }
    }

    internal sealed class DungeonPropDepth : MonoBehaviour
    {
        private SpriteRenderer prop, player;
        private Vector2 groundOffset;

        internal void Initialize(SpriteRenderer propSprite, SpriteRenderer playerSprite, Vector2 footprintCenter)
        {
            prop = propSprite;
            player = playerSprite;
            groundOffset = footprintCenter;
            prop.sortingLayerID = player.sortingLayerID;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (player == null || prop == null) return;
            float groundY = transform.TransformPoint(groundOffset).y;
            bool playerBehind = player.transform.position.y > groundY;
            int order = player.sortingOrder + (playerBehind ? 1 : -1);
            if (prop.sortingOrder != order) prop.sortingOrder = order;
        }
    }
}
