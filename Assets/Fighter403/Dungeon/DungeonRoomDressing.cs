using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace Fighter403
{
    internal static class DungeonRoomDressing
    {
        private const int PropCount = 50;
        private const int Floor = 0, Detail = 1, Equipment = 2;
        private const int Service = 0, Research = 1, Security = 2, Power = 3, Freight = 4;
        private static readonly int[] FloorFamilies = { Freight, Security, Power };
        private static Prop[] catalog;
        private static Prop[] smallBreakables;
        private static Prop crate, capacitor, supplyBox;

        [Serializable]
        private sealed class LayoutData
        {
            public Prop[] pieces;
        }

        [Serializable]
        private sealed class Prop
        {
            public string name;
            public int family, kind;
            public int fx, fy, fw, fh;
            [NonSerialized] public Sprite Sprite;
            [NonSerialized] public int Durability;
            public RectInt Footprint => new(fx, fy, fw, fh);
            public bool IsSmallBox => name is "SupplyBox" or "ToolCase";
        }

        private enum Pattern { Line, Stack, Corner, Pair, Marker }

        private readonly struct Cluster
        {
            public readonly Vector2 Anchor;
            public readonly int Family;
            public readonly Pattern Pattern;
            public Cluster(Vector2 anchor, int family, Pattern pattern)
            {
                Anchor = anchor;
                Family = family;
                Pattern = pattern;
            }
        }

        private readonly struct Slot
        {
            public readonly Vector2 Offset;
            public readonly int Kind;
            public Slot(float along, float inward, int kind)
            {
                Offset = new Vector2(along, inward);
                Kind = kind;
            }
        }

        private static readonly Slot[] LineSlots =
        {
            new(-31, -22, Equipment), new(31, -22, Equipment),
            new(-60, 19, Detail), new(0, 20, Floor), new(57, 18, Detail),
        };
        private static readonly Slot[] StackSlots =
        {
            new(0, -30, Equipment), new(0, 22, Equipment),
            new(-51, 9, Detail), new(49, 0, Detail), new(0, 57, Floor),
        };
        private static readonly Slot[] CornerSlots =
        {
            new(-28, -26, Equipment), new(31, -7, Equipment),
            new(-42, 35, Detail), new(11, 28, Floor), new(59, 30, Detail),
        };
        private static readonly Slot[] PairSlots =
        {
            new(0, -13, Equipment), new(-34, 20, Detail), new(21, 23, Floor),
        };
        private static readonly Slot[] MarkerSlots =
        {
            new(-22, 0, Floor), new(20, 8, Detail),
        };
        private static readonly Vector2[] SlotAdjustments =
        {
            Vector2.zero, new(-8, 0), new(8, 0), new(0, -8), new(0, 8),
        };
        private static readonly Vector2[] SmallPropSlots =
        {
            new(-.79f, -.63f), new(.79f, .63f), new(.79f, -.63f), new(-.79f, .63f),
            new(-.57f, -.63f), new(.57f, .63f), new(.57f, -.63f), new(-.57f, .63f),
            new(-.36f, -.63f), new(.36f, .63f), new(.36f, -.63f), new(-.36f, .63f),
            new(-.29f, -.63f), new(.29f, .63f), new(.29f, -.63f), new(-.29f, .63f),
            new(-.79f, -.48f), new(.79f, .48f), new(.79f, -.48f), new(-.79f, .48f),
        };

        internal static bool Ready
        {
            get
            {
                if (catalog != null) return true;
                string path = DungeonRoomArt.ResourceFolder;
                var source = Resources.Load<TextAsset>($"{path}/RoomPropLayout");
                if (source == null) return false;
                var data = JsonUtility.FromJson<LayoutData>(source.text);
                if (data?.pieces?.Length != PropCount) return false;
                var atlas = Resources.LoadAll<Sprite>($"{path}/RoomProps")
                    .ToDictionary(sprite => sprite.name, StringComparer.Ordinal);
                if (atlas.Count != PropCount) return false;
                foreach (var prop in data.pieces)
                    if (!atlas.TryGetValue(prop.name, out prop.Sprite)) return false;
                crate = Array.Find(data.pieces, prop => prop.name == "CrateStack");
                capacitor = Array.Find(data.pieces, prop => prop.name == "CapacitorTower");
                if (crate == null || capacitor == null) return false;
                crate.Durability = 10;
                capacitor.Durability = 18;
                var smallAtlas = Resources.LoadAll<Sprite>($"{path}/SmallBreakables")
                    .ToDictionary(sprite => sprite.name, StringComparer.Ordinal);
                supplyBox = SmallProp("SupplyBox", Freight, 6, new RectInt(5, 19, 22, 9));
                smallBreakables = new[]
                {
                    supplyBox,
                    SmallProp("GlassBottle", Research, 3, new RectInt(11, 22, 10, 6)),
                    SmallProp("PartsCanister", Service, 8, new RectInt(7, 20, 18, 8)),
                    SmallProp("SampleVial", Research, 3, new RectInt(9, 23, 14, 5)),
                    SmallProp("BatteryCell", Power, 7, new RectInt(5, 20, 22, 8)),
                    SmallProp("CeramicJar", Service, 4, new RectInt(9, 22, 14, 6)),
                    SmallProp("ToolCase", Freight, 8, new RectInt(4, 21, 24, 7)),
                    SmallProp("DataCartridge", Security, 5, new RectInt(6, 20, 20, 8)),
                    SmallProp("SignalGauge", Power, 6, new RectInt(7, 22, 18, 6)),
                };
                foreach (var prop in smallBreakables)
                    if (!smallAtlas.TryGetValue(prop.name, out prop.Sprite)) return false;
                catalog = data.pieces;
                return true;
            }
        }

        private static Prop SmallProp(string name, int family, int durability, RectInt footprint) => new()
        {
            name = name, family = family, kind = Equipment, Durability = durability,
            fx = footprint.x, fy = footprint.y, fw = footprint.width, fh = footprint.height,
        };

        internal static void Reload() => catalog = null;

        internal static IEnumerable<DungeonRoomArt.Piece> Place(int theme, DungeonRoomKind role, int seed, Vector2 size)
        {
            if (!Ready) yield break;
            int preferredFamily = FloorFamilies[theme];
            var random = new Random(unchecked(seed * 397 ^ theme * 7919 ^ (int)role * 104729));
            var roomCenter = size * .5f;
            var used = new List<Rect>(17)
            {
                Centered(roomCenter, role == DungeonRoomKind.Boss ? new Vector2(448f, 304f) : new Vector2(192f, 148f)),
            };
            var encounterSpawns = role is DungeonRoomKind.Combat or DungeonRoomKind.Elite
                ? EncounterClearances(roomCenter) : Array.Empty<Rect>();
            var choices = catalog.Where(prop => prop.Durability == 0).ToList();
            int breakablesLeft = role switch
            {
                DungeonRoomKind.Boss or DungeonRoomKind.Shop => 0,
                DungeonRoomKind.Elite or DungeonRoomKind.Hidden => 2,
                _ => 1,
            };
            Prop breakable = role == DungeonRoomKind.Start || ((seed ^ theme) & 1) == 0 ? crate : capacitor;
            bool placedBoxGroup = false;
            foreach (var cluster in Compose(role, preferredFamily, random))
            {
                var slots = cluster.Pattern switch
                {
                    Pattern.Line => LineSlots,
                    Pattern.Stack => StackSlots,
                    Pattern.Corner => CornerSlots,
                    Pattern.Pair => PairSlots,
                    _ => MarkerSlots,
                };
                // Only floor pieces rotate. Open fronts and highlights stay consistent on props that stand up.
                int floorTurn = cluster.Pattern == Pattern.Stack ? 1 : 0;
                var anchor = roomCenter + new Vector2(cluster.Anchor.x * roomCenter.x, cluster.Anchor.y * roomCenter.y);
                int mirrorX = cluster.Anchor.x > 0 ? -1 : 1;
                int mirrorY = cluster.Anchor.y > 0 ? -1 : 1;
                bool placedEquipment = cluster.Pattern == Pattern.Marker;
                bool placedBreakable = false;
                foreach (var slot in slots)
                {
                    if (slot.Kind != Equipment && !placedEquipment) break;
                    int turn = slot.Kind == Floor ? floorTurn : 0;
                    var target = anchor + new Vector2(slot.Offset.x * mirrorX, slot.Offset.y * mirrorY);
                    if (slot.Kind == Equipment && breakablesLeft > 0 && !placedBreakable)
                    {
                        if (breakable == crate && TryPlaceBoxGroup(crate, supplyBox, 2, target, size, roomCenter,
                                used, encounterSpawns, out var boxCenters))
                        {
                            yield return BreakablePiece(crate, boxCenters[0]);
                            yield return BreakablePiece(supplyBox, boxCenters[1]);
                            placedBoxGroup = placedEquipment = placedBreakable = true;
                            breakablesLeft--;
                            breakable = capacitor;
                            continue;
                        }
                        if (breakable == capacitor && TryPlace(capacitor, target, 0, size, roomCenter, used,
                                encounterSpawns, out var capacitorCenter))
                        {
                            yield return BreakablePiece(capacitor, capacitorCenter);
                            placedEquipment = placedBreakable = true;
                            breakablesLeft--;
                            breakable = crate;
                            continue;
                        }
                    }
                    var available = choices.Where(prop => prop.kind == slot.Kind).ToList();
                    while (available.Count > 0)
                    {
                        int index = Pick(available, cluster.Family, preferredFamily, random);
                        var prop = available[index];
                        if (!TryPlace(prop, target, turn, size, roomCenter, used, encounterSpawns, out var center))
                        {
                            available.RemoveAt(index);
                            continue;
                        }
                        placedEquipment |= slot.Kind == Equipment;
                        choices.Remove(prop);
                        yield return new DungeonRoomArt.Piece(prop.Sprite, center, turn, slot.Kind == Equipment ? -17 : -19,
                            footprint: prop.Footprint);
                        break;
                    }
                }
            }

            int smallPropsLeft = role switch
            {
                DungeonRoomKind.Boss => 0,
                DungeonRoomKind.Elite or DungeonRoomKind.Hidden => 4,
                DungeonRoomKind.Start or DungeonRoomKind.Shop => 2,
                _ => 3,
            };
            if (smallPropsLeft == 0) yield break;

            int smallPropFamily = role switch
            {
                DungeonRoomKind.Start or DungeonRoomKind.Maintenance => Service,
                DungeonRoomKind.Shop or DungeonRoomKind.Vault => Freight,
                DungeonRoomKind.Event or DungeonRoomKind.Hidden => Research,
                DungeonRoomKind.Elite => Security,
                _ => preferredFamily,
            };
            var availableSmallProps = new List<Prop>(smallBreakables);
            if (placedBoxGroup) availableSmallProps.RemoveAll(prop => prop.IsSmallBox);
            int boxCount = role is DungeonRoomKind.Vault or DungeonRoomKind.Combat or DungeonRoomKind.Elite or DungeonRoomKind.Hidden
                ? 3 : 2;
            int firstSlot = random.Next(SmallPropSlots.Length);
            int propIndex = Pick(availableSmallProps, smallPropFamily, preferredFamily, random);
            for (int i = 0; i < SmallPropSlots.Length && smallPropsLeft > 0; i++)
            {
                Vector2 slot = SmallPropSlots[(firstSlot + i) % SmallPropSlots.Length];
                Vector2 target = roomCenter + Vector2.Scale(roomCenter, slot);
                var prop = availableSmallProps[propIndex];
                if (prop.IsSmallBox)
                {
                    if (!TryPlaceBoxGroup(prop, prop, boxCount, target, size, roomCenter, used, encounterSpawns, out var centers))
                        continue;
                    foreach (var center in centers) yield return BreakablePiece(prop, center);
                    availableSmallProps.RemoveAll(choice => choice.IsSmallBox);
                }
                else
                {
                    if (!TryPlace(prop, target, 0, size, roomCenter, used, encounterSpawns, out var center)) continue;
                    yield return BreakablePiece(prop, center);
                    availableSmallProps.RemoveAt(propIndex);
                }
                smallPropsLeft--;
                if (smallPropsLeft > 0) propIndex = Pick(availableSmallProps, smallPropFamily, preferredFamily, random);
            }
        }

        private static DungeonRoomArt.Piece BreakablePiece(Prop prop, Vector2 center) =>
            new(prop.Sprite, center, 0, -17, footprint: prop.Footprint, durability: prop.Durability);

        private static IEnumerable<Cluster> Compose(DungeonRoomKind role, int preferredFamily, Random random)
        {
            int sideX = random.Next(2) == 0 ? -1 : 1;
            int sideY = random.Next(2) == 0 ? -1 : 1;
            int variation = random.Next(3);
            Cluster At(float x, float y, int family, Pattern pattern) => new(new Vector2(x * sideX, y * sideY), family, pattern);
            Cluster Along(int family, Pattern pattern) => At(.58f, .48f, family, pattern);
            Cluster Side(int family, Pattern pattern) => At(.67f, .43f, family, pattern);
            Cluster Opposite(int family, Pattern pattern) => At(-.56f, -.47f, family, pattern);
            Cluster Adjacent(int family, Pattern pattern) => At(.61f, -.49f, family, pattern);

            switch (role)
            {
                case DungeonRoomKind.Start:
                    yield return variation == 0 ? Side(Service, Pattern.Stack) : Along(Service, Pattern.Line);
                    yield return variation == 2 ? Adjacent(preferredFamily, Pattern.Marker) : Opposite(preferredFamily, Pattern.Marker);
                    break;
                case DungeonRoomKind.Combat:
                    yield return variation == 0 ? Side(preferredFamily, Pattern.Pair) : Along(preferredFamily, Pattern.Corner);
                    yield return variation == 1 ? Adjacent(Security, Pattern.Marker) : Opposite(Security, Pattern.Marker);
                    break;
                case DungeonRoomKind.Elite:
                    yield return variation == 0 ? Side(Security, Pattern.Stack) : Along(Security, Pattern.Corner);
                    yield return variation == 2 ? Adjacent(Power, Pattern.Pair) : Opposite(Power, Pattern.Pair);
                    break;
                case DungeonRoomKind.Vault:
                    yield return variation == 1 ? Side(Freight, Pattern.Stack) : Along(Freight, Pattern.Line);
                    yield return variation == 2 ? At(-.62f, .49f, Security, Pattern.Pair) : Opposite(Security, Pattern.Marker);
                    break;
                case DungeonRoomKind.Shop:
                    yield return variation == 0 ? Side(Freight, Pattern.Stack) : Along(Freight, Pattern.Line);
                    yield return variation == 1 ? At(-.60f, .49f, Research, Pattern.Pair) : Opposite(Research, Pattern.Pair);
                    break;
                case DungeonRoomKind.Maintenance:
                    yield return variation == 0 ? Side(Service, Pattern.Stack) : Along(Service, Pattern.Corner);
                    yield return variation == 2 ? Adjacent(Power, Pattern.Marker) : Opposite(Power, Pattern.Pair);
                    break;
                case DungeonRoomKind.Event:
                    yield return variation == 0 ? Side(Research, Pattern.Stack) : Along(Research, Pattern.Corner);
                    yield return variation == 2 ? Adjacent(Power, Pattern.Marker) : Opposite(Power, Pattern.Marker);
                    break;
                case DungeonRoomKind.Hidden:
                    yield return variation == 1 ? Side(Freight, Pattern.Stack) : Along(Freight, Pattern.Corner);
                    yield return variation == 0 ? Adjacent(Service, Pattern.Pair) : Opposite(Research, Pattern.Marker);
                    break;
                case DungeonRoomKind.Boss:
                    yield return At(.74f, .64f, preferredFamily, variation == 1 ? Pattern.Stack : Pattern.Line);
                    yield return At(-.66f, -.64f, Security, Pattern.Corner);
                    yield return variation == 2 ? At(.69f, -.67f, Power, Pattern.Pair) : At(-.67f, .66f, Power, Pattern.Pair);
                    break;
            }
        }

        private static int Pick(List<Prop> choices, int family, int preferredFamily, Random random)
        {
            int Weight(Prop prop) => prop.family == family ? 24 : prop.family == preferredFamily ? 2 : 1;
            int ticket = random.Next(choices.Sum(Weight));
            for (int i = 0; i < choices.Count; i++)
            {
                ticket -= Weight(choices[i]);
                if (ticket < 0) return i;
            }
            return choices.Count - 1;
        }

        private static bool TryPlace(Prop prop, Vector2 target, int turn, Vector2 size, Vector2 roomCenter,
            List<Rect> used, Rect[] encounterSpawns, out Vector2 center)
        {
            Vector2 dimensions = prop.Sprite.rect.size;
            if ((turn & 1) != 0) dimensions = new Vector2(dimensions.y, dimensions.x);
            foreach (var adjustment in SlotAdjustments)
            {
                Vector2 point = target + adjustment;
                // Pixel-aligned position with a half-pixel offset for odd-sized crops.
                point = new Vector2(Mathf.Round(point.x - dimensions.x * .5f), Mathf.Round(point.y - dimensions.y * .5f))
                    + dimensions * .5f;
                var bounds = Centered(point, dimensions);
                if (!CanPlace(prop, bounds, size, roomCenter, used, encounterSpawns)) continue;
                center = point;
                used.Add(Centered(point, dimensions + Vector2.one * 8f));
                return true;
            }
            center = default;
            return false;
        }

        private static bool TryPlaceBoxGroup(Prop first, Prop companion, int desiredCount, Vector2 target, Vector2 size,
            Vector2 roomCenter, List<Rect> used, Rect[] encounterSpawns, out Vector2[] centers)
        {
            // The whole row must fit before it claims any space from the room.
            for (int count = desiredCount; count >= 2; count--)
            {
                float width = first.Sprite.rect.width + (count - 1) * companion.Sprite.rect.width;
                var row = new Rect[count];
                foreach (var adjustment in SlotAdjustments)
                {
                    float left = Mathf.Round(target.x + adjustment.x - width * .5f);
                    bool fits = true;
                    for (int i = 0; i < count; i++)
                    {
                        var prop = i == 0 ? first : companion;
                        Vector2 dimensions = prop.Sprite.rect.size;
                        float top = Mathf.Round(target.y + adjustment.y - dimensions.y * .5f + (i % 2) * 3f);
                        row[i] = new Rect(new Vector2(left, top), dimensions);
                        if (!CanPlace(prop, row[i], size, roomCenter, used, encounterSpawns))
                        {
                            fits = false;
                            break;
                        }
                        left += dimensions.x;
                    }
                    if (!fits) continue;

                    var firstFoot = FootprintAt(first, row[0]);
                    var lastFoot = FootprintAt(companion, row[count - 1]);
                    Vector2 stance = (firstFoot.center + lastFoot.center) * .5f;
                    // Leave an inward-facing stance about 1.37 world units from the row for a wide slash.
                    stance.y = Mathf.MoveTowards(stance.y, roomCenter.y, 44f);
                    var approach = Centered(stance, Vector2.one * 36f);
                    if (used.Exists(area => area.Overlaps(approach))) continue;

                    centers = new Vector2[count];
                    for (int i = 0; i < count; i++)
                    {
                        centers[i] = row[i].center;
                        used.Add(Centered(centers[i], row[i].size + Vector2.one * 8f));
                    }
                    used.Add(approach);
                    return true;
                }
            }
            centers = Array.Empty<Vector2>();
            return false;
        }

        private static bool CanPlace(Prop prop, Rect bounds, Vector2 size, Vector2 roomCenter, List<Rect> used,
            Rect[] encounterSpawns)
        {
            if (bounds.xMin < 39 || bounds.yMin < 39 || bounds.xMax > size.x - 39 || bounds.yMax > size.y - 39) return false;
            if (used.Exists(area => area.Overlaps(bounds))) return false;
            if (prop.kind != Equipment) return true;

            var footprint = FootprintAt(prop, bounds);
            // Reserve both axes around the center for the four exits, including concealed ones.
            if (footprint.xMin < roomCenter.x + 53 && footprint.xMax > roomCenter.x - 53) return false;
            if (footprint.yMin < roomCenter.y + 53 && footprint.yMax > roomCenter.y - 53) return false;
            return !Array.Exists(encounterSpawns, area => area.Overlaps(footprint));
        }

        private static Rect FootprintAt(Prop prop, Rect bounds) =>
            new(bounds.position + (Vector2)prop.Footprint.position, prop.Footprint.size);

        private static Rect[] EncounterClearances(Vector2 center)
        {
            const float ppu = DungeonRoomArt.PixelsPerUnit;
            const float clearance = 26f;
            // Encounters spawn at 1.9 or 2.45 units along the entrance axis and up to 2.7 units laterally.
            float near = 1.9f * ppu - clearance;
            float far = 2.45f * ppu + clearance;
            float span = 2.7f * ppu + clearance;
            return new[]
            {
                Rect.MinMaxRect(center.x - span, center.y + near, center.x + span, center.y + far),
                Rect.MinMaxRect(center.x - span, center.y - far, center.x + span, center.y - near),
                Rect.MinMaxRect(center.x + near, center.y - span, center.x + far, center.y + span),
                Rect.MinMaxRect(center.x - far, center.y - span, center.x - near, center.y + span),
            };
        }

        private static Rect Centered(Vector2 center, Vector2 size) => new(center - size * .5f, size);
    }
}
