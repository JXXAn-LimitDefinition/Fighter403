using Fighter403.UI;
using UnityEditor;

namespace Fighter403.EditorTools
{
    [InitializeOnLoad]
    internal static class CardPrefabMigration
    {
        private const string CardsFolder = "Assets/Fighter403/Prefabs/Cards";

        static CardPrefabMigration()
        {
            EditorApplication.delayCall += MigratePendingCards;
        }

        private static void MigratePendingCards()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool changed = false;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CardsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
                if (card == null) continue;

                var serialized = new SerializedObject(card);
                bool timelineReady = serialized.FindProperty("timelineConfigured").boolValue;
                bool shapeReady = serialized.FindProperty("shapeConfigured").boolValue;
                if (timelineReady && shapeReady) continue;

                card.EnsureEditorData();
                EditorUtility.SetDirty(card);
                changed = true;
            }

            if (changed) AssetDatabase.SaveAssets();
        }
    }
}
