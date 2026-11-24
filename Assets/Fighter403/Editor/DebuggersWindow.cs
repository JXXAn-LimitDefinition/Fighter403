using System;
using System.Linq;
using Fighter403.Debuggers;
using UnityEditor;
using UnityEngine;

namespace Fighter403.EditorTools
{
    internal sealed class DebuggersWindow : EditorWindow
    {
        private const string AssetFolder = "Assets/Fighter403/Debuggers";
        private DebuggerDefinition[] definitions = Array.Empty<DebuggerDefinition>();
        [SerializeField] private DebuggerDefinition selected;
        private SerializedObject details;
        private string search = "";
        private Vector2 listScroll, detailsScroll;

        [MenuItem("Fighter403/数据注入器")]
        private static void Open()
        {
            var window = GetWindow<DebuggersWindow>("数据注入器");
            window.minSize = new Vector2(520f, 300f);
        }

        private void OnEnable() => Reload();
        private void OnProjectChange() => Reload();
        private void OnInspectorUpdate() => Repaint();

        private void Reload()
        {
            definitions = AssetDatabase.FindAssets("t:DebuggerDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DebuggerDefinition>)
                .Where(definition => definition != null)
                .OrderBy(Label, StringComparer.CurrentCulture)
                .ToArray();
            Select(definitions.Contains(selected) ? selected : definitions.FirstOrDefault());
            Repaint();
        }

        private void Select(DebuggerDefinition definition)
        {
            selected = definition;
            details = definition != null ? new SerializedObject(definition) : null;
            detailsScroll = Vector2.zero;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(206f));
                GUILayout.FlexibleSpace();
                if (EditorApplication.isPlaying)
                    GUILayout.Label($"本局已获得 {DebuggersManager.OwnedCount} 个", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("新建数据注入器", EditorStyles.toolbarButton)) CreateDefinition();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                DrawDetails();
            }
        }

        private void DrawList()
        {
            using var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(218f));
            using var scroll = new EditorGUILayout.ScrollViewScope(listScroll);
            listScroll = scroll.scrollPosition;
            foreach (var definition in definitions)
            {
                if (definition == null || Label(definition).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var rect = EditorGUILayout.GetControlRect(false, 34f);
                bool owned = EditorApplication.isPlaying && DebuggersManager.Owns(definition);
                bool isSelected = selected == definition;
                if (GUI.Toggle(rect, isSelected, GUIContent.none, EditorStyles.toolbarButton) && !isSelected)
                {
                    GUI.FocusControl(null);
                    Select(definition);
                }

                DrawIcon(new Rect(rect.x + 4f, rect.y + 3f, 28f, 28f), definition.Icon);
                var labelRect = new Rect(rect.x + 38f, rect.y, rect.width - (owned ? 80f : 44f), rect.height);
                GUI.Label(labelRect, new GUIContent(Label(definition), Label(definition)), EditorStyles.label);
                if (owned)
                    GUI.Label(new Rect(rect.xMax - 39f, rect.y, 37f, rect.height), "已获得", EditorStyles.miniLabel);
            }
        }

        private void DrawDetails()
        {
            using var scroll = new EditorGUILayout.ScrollViewScope(detailsScroll);
            detailsScroll = scroll.scrollPosition;
            if (selected == null || details == null)
            {
                EditorGUILayout.HelpBox("新建或选择一个数据注入器。", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                var preview = GUILayoutUtility.GetRect(80f, 80f, GUILayout.ExpandWidth(false));
                EditorGUI.DrawRect(preview, new Color32(25, 29, 39, 255));
                DrawIcon(preview, selected.Icon);
                GUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(Label(selected), EditorStyles.boldLabel);
                    if (selected.Effect == DebuggerEffect.CastEnergyRecovery)
                        EditorGUILayout.LabelField($"释放卡牌期间，费用回复速度提高 {selected.BonusPercent:0.##}%。", EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("在 Project 中定位", GUILayout.ExpandWidth(false))) EditorGUIUtility.PingObject(selected);
                }
            }

            EditorGUILayout.Space(8f);
            details.Update();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.PropertyField(details.FindProperty("displayName"), new GUIContent("名称"));
                EditorGUILayout.PropertyField(details.FindProperty("icon"), new GUIContent("独立图标"));
                var effect = details.FindProperty("effect");
                effect.enumValueIndex = EditorGUILayout.Popup("效果", effect.enumValueIndex, new[] { "无", "出牌期间加快费用回复" });
                if (effect.enumValueIndex == (int)DebuggerEffect.CastEnergyRecovery)
                    EditorGUILayout.PropertyField(details.FindProperty("bonusPercent"), new GUIContent("回复速度加成（%）"));
            }

            if (details.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(selected);

            bool sharedIcon = selected.Icon != null && definitions.Any(definition =>
                definition != null && definition != selected && definition.Icon == selected.Icon);
            if (!selected.IsConfigured)
                EditorGUILayout.HelpBox("请设置名称和图标。", MessageType.Warning);
            else if (sharedIcon)
                EditorGUILayout.HelpBox("已有其他数据注入器使用此图标，请分配独立图标。", MessageType.Warning);

            EditorGUILayout.Space(6f);
            bool playing = EditorApplication.isPlaying;
            bool owned = playing && DebuggersManager.Owns(selected);
            using (new EditorGUI.DisabledScope(!playing || owned || !selected.IsConfigured || sharedIcon))
            {
                string label = !playing ? "进入 Play Mode 后可测试发放" : owned ? "本局已获得" : "本局测试发放";
                if (GUILayout.Button(label, GUILayout.ExpandWidth(false))) DebuggersManager.TryAcquire(selected);
            }
        }

        private void CreateDefinition()
        {
            var path = EditorUtility.SaveFilePanelInProject("新建数据注入器", "NewDataInjector", "asset", "选择资产保存位置。", AssetFolder);
            if (string.IsNullOrEmpty(path)) return;

            var definition = CreateInstance<DebuggerDefinition>();
            var properties = new SerializedObject(definition);
            properties.FindProperty("displayName").stringValue = System.IO.Path.GetFileNameWithoutExtension(path);
            properties.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssetIfDirty(definition);
            search = "";
            Reload();
            Select(definition);
        }

        private static string Label(DebuggerDefinition definition) => string.IsNullOrEmpty(definition.DisplayName)
            ? definition.name : definition.DisplayName;

        private static void DrawIcon(Rect rect, Sprite sprite)
        {
            if (sprite == null) return;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(sprite));
            if (texture == null) return;

            Rect source = sprite.rect;
            float scale = Mathf.Min(rect.width / source.width, rect.height / source.height);
            Vector2 size = source.size * scale;
            var target = new Rect(rect.center - size * 0.5f, size);
            var uv = new Rect(source.x / texture.width, source.y / texture.height,
                source.width / texture.width, source.height / texture.height);
            GUI.DrawTextureWithTexCoords(target, texture, uv);
        }
    }
}
