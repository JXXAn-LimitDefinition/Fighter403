using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fighter403.UI
{
    [CreateAssetMenu(menuName = "Fighter403/玩家初始卡组", fileName = "PlayerInitialDeck")]
    public sealed class PlayerInitialDeck : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            [InspectorName("卡牌预制体")] public CardDefinition card;
            [Min(0), InspectorName("数量")] public int copies;
        }

        [SerializeField, InspectorName("初始卡牌"), Tooltip("每次新开地牢时按数量加入卡组并洗牌。数量为 0 的行跳过。")]
        private Entry[] entries = Array.Empty<Entry>();

        internal bool TryBuild(IReadOnlyList<CardDefinition> catalog, out List<CardDefinition> deck, out string error)
        {
            deck = new List<CardDefinition>();
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.copies <= 0) continue;
                if (entry.card == null || !catalog.Contains(entry.card))
                {
                    error = $"初始卡组第 {i + 1} 行需要选择已登记的卡牌。";
                    return false;
                }
                if (entry.card.IsTemporary)
                {
                    error = $"临时牌「{entry.card.DisplayName}」不能加入初始卡组。";
                    return false;
                }
                if (entry.card.Kind == CardKind.ThousandSlashes && (entry.copies > 1 || deck.Contains(entry.card)))
                {
                    error = $"初始卡组里的「{entry.card.DisplayName}」最多 1 张。";
                    return false;
                }

                for (int copy = 0; copy < entry.copies; copy++)
                    deck.Add(entry.card);
            }

            error = deck.Count == 0 ? "初始卡组至少需要 1 张卡牌。" : null;
            return error == null;
        }
    }
}
