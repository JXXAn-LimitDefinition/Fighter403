using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fighter403.Player;
using Fighter403.UI;
using UnityEngine;
using Random = System.Random;

namespace Fighter403
{
    internal enum DungeonRoomKind { Start, Combat, Elite, Vault, Shop, Maintenance, Event, Hidden, Boss }
    internal enum DungeonDoorState { Wall, Open, Locked, Concealed, Breached }

    internal sealed class DungeonPassage
    {
        public readonly DungeonRoom First;
        public readonly DungeonRoom Second;
        public bool Hidden => First.Kind == DungeonRoomKind.Hidden || Second.Kind == DungeonRoomKind.Hidden;
        public bool Breached { get; set; }

        public DungeonPassage(DungeonRoom first, DungeonRoom second)
        {
            First = first;
            Second = second;
        }

        public DungeonRoom Other(DungeonRoom room) => room == First ? Second : First;
    }

    internal sealed class DungeonRoom
    {
        public readonly Vector2Int Cell;
        public readonly DungeonPassage[] Doors = new DungeonPassage[4];
        public readonly int Distance;
        public DungeonRoomKind Kind { get; set; }
        public int ArtSeed { get; set; }
        public EnemyRole[] Enemies { get; set; } = Array.Empty<EnemyRole>();
        public int RewardSeed { get; set; }
        public CardKind[] OfferedCards { get; set; }
        public readonly HashSet<CardKind> Bought = new();
        public readonly Dictionary<Vector2, int> PropDurability = new();
        public int Event { get; set; }
        public bool Visited { get; set; }
        public bool Cleared { get; set; }
        public bool Claimed { get; set; }
        public bool TreasureOpened { get; set; }

        public DungeonRoom(Vector2Int cell, int distance, DungeonRoomKind kind = DungeonRoomKind.Combat)
        {
            Cell = cell;
            Distance = distance;
            Kind = kind;
        }
    }

    internal static class DungeonGenerator
    {
        public const int FloorCount = 3;
        private const int UnownedCardWeight = 3;
        public static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        public static readonly string[] ThemeLabels = { "外网缓存区", "防火墙矩阵", "内核" };
        public static readonly string[] RoomLabels =
            { "起点", "战斗", "精英", "宝库", "商店", "维护站", "事件", "隐藏房", "Boss" };

        public static int ParseSeed(string text)
        {
            text ??= "";
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                return number;
            uint hash = 2166136261u;
            foreach (char character in text.Trim())
                hash = unchecked((hash ^ character) * 16777619u);
            return unchecked((int)hash);
        }

        public static DungeonRoom[] Generate(int seed, int floor)
        {
            if (floor < 0 || floor >= FloorCount)
                throw new ArgumentOutOfRangeException(nameof(floor));

            int floorSeed = unchecked(seed * 397 ^ (floor + 1) * 7919);
            var random = new Random(floorSeed);
            int count = random.Next(9, 12);
            var rooms = new List<DungeonRoom> { new(Vector2Int.zero, 0, DungeonRoomKind.Start) };
            var occupied = new HashSet<Vector2Int> { Vector2Int.zero };
            int mainPathLength = random.Next(4, 6);
            for (int i = 0; i < mainPathLength; i++)
                Extend(rooms, occupied, rooms[^1], random, DungeonRoomKind.Combat);
            DungeonRoom boss = rooms[^1];
            boss.Kind = DungeonRoomKind.Boss;

            while (rooms.Count < count)
            {
                var candidates = rooms.Where(room => room.Distance < boss.Distance - 1 &&
                    AvailableSides(room, occupied).Any()).ToArray();
                if (candidates.Length == 0)
                    candidates = rooms.Where(room => room != boss && AvailableSides(room, occupied).Any()).ToArray();
                Extend(rooms, occupied, candidates[random.Next(candidates.Length)], random, DungeonRoomKind.Combat);
            }

            var branches = rooms.Where(room => room.Kind == DungeonRoomKind.Combat &&
                room.Doors.Count(door => door != null) == 1).ToList();
            MarkBranch(branches, rooms, random, DungeonRoomKind.Shop);
            MarkBranch(branches, rooms, random, DungeonRoomKind.Vault);
            MarkBranch(branches, rooms, random, DungeonRoomKind.Elite);
            if (count == 11)
            {
                var additional = floor > 0 && random.Next(3) == 0
                    ? DungeonRoomKind.Elite
                    : random.Next(2) == 0 ? DungeonRoomKind.Maintenance : DungeonRoomKind.Event;
                MarkBranch(branches, rooms, random, additional);
            }

            if (random.Next(4) != 0)
            {
                var hosts = rooms.Where(room => room.Kind == DungeonRoomKind.Combat &&
                    AvailableSides(room, occupied).Any()).ToArray();
                if (hosts.Length > 0)
                    Extend(rooms, occupied, hosts[random.Next(hosts.Length)], random, DungeonRoomKind.Hidden);
            }

            foreach (DungeonRoom room in rooms)
            {
                room.ArtSeed = random.Next();
                room.Enemies = Encounter(random, floor, room.Kind);
                // Preserve the three random draws formerly used by the fixed reward list.
                room.RewardSeed = random.Next() ^ random.Next() ^ random.Next();
                room.Event = random.Next(3);
            }
            return rooms.ToArray();
        }

        private static IEnumerable<int> AvailableSides(DungeonRoom room, HashSet<Vector2Int> occupied)
        {
            for (int side = 0; side < Directions.Length; side++)
                if (!occupied.Contains(room.Cell + Directions[side]))
                    yield return side;
        }

        private static void Extend(List<DungeonRoom> rooms, HashSet<Vector2Int> occupied, DungeonRoom from,
            Random random, DungeonRoomKind kind)
        {
            var options = AvailableSides(from, occupied).ToArray();
            int side = options[random.Next(options.Length)];
            var room = new DungeonRoom(from.Cell + Directions[side], from.Distance + 1, kind);
            occupied.Add(room.Cell);
            rooms.Add(room);
            var passage = new DungeonPassage(from, room);
            from.Doors[side] = room.Doors[(side + 2) % 4] = passage;
        }

        private static void MarkBranch(List<DungeonRoom> branches, List<DungeonRoom> rooms, Random random, DungeonRoomKind kind)
        {
            if (branches.Count == 0)
                branches.AddRange(rooms.Where(room => room.Kind == DungeonRoomKind.Combat));
            int index = random.Next(branches.Count);
            branches[index].Kind = kind;
            branches.RemoveAt(index);
        }

        private static EnemyRole[] Encounter(Random random, int floor, DungeonRoomKind kind)
        {
            if (kind == DungeonRoomKind.Boss)
                return floor switch { 0 => new[] { EnemyRole.Melee }, 2 => new[] { EnemyRole.Shield }, _ => Array.Empty<EnemyRole>() };
            if (kind == DungeonRoomKind.Elite)
            {
                if (floor == 0) return new[] { EnemyRole.Resonator, EnemyRole.Melee, EnemyRole.Melee };
                if (floor == 2) return new[] { EnemyRole.Protocol, EnemyRole.Melee, EnemyRole.Swarm };
                return random.Next(3) switch
                {
                    0 => new[] { EnemyRole.Mirror },
                    1 => new[] { EnemyRole.Cluster, EnemyRole.Cluster },
                    _ => new[] { EnemyRole.Splitter }
                };
            }
            if (kind != DungeonRoomKind.Combat)
                return Array.Empty<EnemyRole>();

            if (floor == 0)
                return Enumerable.Range(0, random.Next(1, 4))
                    .Select(_ => random.Next(4) == 0 ? EnemyRole.Swarm : EnemyRole.Melee).ToArray();
            if (floor == 1)
                return random.Next(3) switch
                {
                    0 => new[] { EnemyRole.Shield, EnemyRole.Swarm },
                    1 => new[] { EnemyRole.Shooter, EnemyRole.Melee },
                    _ => new[] { EnemyRole.Shooter, EnemyRole.Melee, EnemyRole.Melee }
                };
            return new[] { EnemyRole.Shooter, EnemyRole.Shield, EnemyRole.Swarm,
                random.Next(2) == 0 ? EnemyRole.Melee : EnemyRole.Swarm };
        }

        public static IReadOnlyList<CardKind> OfferCards(DungeonRoom room, IEnumerable<CardKind> configuredKinds,
            IEnumerable<CardKind> deckKinds, bool hasAcquiredThousandSlashes)
        {
            bool shop = room.Kind == DungeonRoomKind.Shop;
            bool backup = room.Kind == DungeonRoomKind.Event;
            if (room.OfferedCards != null && !hasAcquiredThousandSlashes) return room.OfferedCards;
            int removedIndex = room.OfferedCards == null ? -1 : Array.IndexOf(room.OfferedCards, CardKind.ThousandSlashes);
            if (room.OfferedCards != null && removedIndex < 0) return room.OfferedCards;

            var owned = deckKinds.ToHashSet();
            var pool = configuredKinds.Distinct().OrderBy(kind => kind)
                .Where(kind => kind != CardKind.BigBang)
                .Where(kind => !hasAcquiredThousandSlashes || kind != CardKind.ThousandSlashes)
                .Where(kind => !shop || !owned.Contains(kind) && !room.Bought.Contains(kind))
                .ToList();
            int Weight(CardKind kind) => shop || backup || owned.Contains(kind) ? 1 : UnownedCardWeight;
            var random = new Random(room.RewardSeed);

            if (room.OfferedCards == null)
                room.OfferedCards = RollCards(random, pool, backup ? 1 : 3, Weight);
            else
            {
                pool.RemoveAll(kind => room.OfferedCards.Contains(kind));
                if (pool.Count > 0)
                    room.OfferedCards[removedIndex] = RollCards(random, pool, 1, Weight)[0];
                else
                    room.OfferedCards = room.OfferedCards.Where(kind => kind != CardKind.ThousandSlashes).ToArray();
            }
            return room.OfferedCards;
        }

        private static CardKind[] RollCards(Random random, List<CardKind> pool, int count, Func<CardKind, int> weight)
        {
            var cards = new CardKind[Math.Min(count, pool.Count)];
            for (int i = 0; i < cards.Length; i++)
            {
                int roll = random.Next(pool.Sum(weight));
                int index = 0;
                for (; index < pool.Count - 1; index++)
                {
                    roll -= weight(pool[index]);
                    if (roll < 0) break;
                }
                cards[i] = pool[index];
                pool.RemoveAt(index);
            }
            return cards;
        }
    }
}
