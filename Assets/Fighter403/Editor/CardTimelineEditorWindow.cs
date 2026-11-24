using System;
using System.Collections.Generic;
using System.Linq;
using Fighter403.UI;
using UnityEditor;
using UnityEngine;

namespace Fighter403.EditorTools
{
    public sealed class CardTimelineEditorWindow : EditorWindow
    {
        private const string CardsFolder = "Assets/Fighter403/Prefabs/Cards";
        private const int TimelineControlHint = 0x403060;

        private enum TimelineDrag
        {
            None,
            StartupEnd,
            ActiveEnd,
            TotalEnd
        }

        private CardDefinition selectedCard;
        private CardDefinition[] cards = Array.Empty<CardDefinition>();
        private string[] cardNames = Array.Empty<string>();
        private SerializedObject serializedCard;
        private Vector2 scroll;
        private int previewFrame;
        private bool previewPlaying;
        private double lastPreviewTime;
        private TimelineDrag timelineDrag;
        private int timelineControlId;
        private bool timelineDirty;
        private int dragStartupFrames;
        private int dragActiveFrames;
        private int dragRecoveryFrames;
        private SerializedProperty startupFramesProperty;
        private SerializedProperty activeFramesProperty;
        private SerializedProperty recoveryFramesProperty;

        [MenuItem("Fighter403/Card Timeline Editor")]
        public static void Open()
        {
            var window = GetWindow<CardTimelineEditorWindow>();
            window.titleContent = new GUIContent("卡牌时间轴");
            window.minSize = new Vector2(760, 640);
            window.RefreshCards();
            window.Show();
        }

        public static void Open(CardDefinition card)
        {
            Open();
            var window = GetWindow<CardTimelineEditorWindow>();
            window.SelectCard(card);
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += UpdatePreview;
            RefreshCards();
        }

        private void OnDisable()
        {
            CommitTimeline();
            if (GUIUtility.hotControl == timelineControlId)
                GUIUtility.hotControl = 0;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= UpdatePreview;
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeObject is CardDefinition card)
                SelectCard(card);
        }

        private void UpdatePreview()
        {
            if (!previewPlaying || selectedCard == null) return;
            double now = EditorApplication.timeSinceStartup;
            float frameDuration = 1f / 60f;
            if (now - lastPreviewTime >= frameDuration)
            {
                int total = Mathf.Max(1, selectedCard.TimelineTotalFrames);
                int steps = Mathf.Max(1, Mathf.FloorToInt((float)((now - lastPreviewTime) / frameDuration)));
                previewFrame = (previewFrame + steps) % total;
                lastPreviewTime = now;
                Repaint();
            }
        }

        private void RefreshCards()
        {
            var loaded = new List<CardDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CardsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
                if (card != null) loaded.Add(card);
            }

            cards = loaded.OrderBy(card => card.name, StringComparer.OrdinalIgnoreCase).ToArray();
            cardNames = cards.Select(card => card.name).ToArray();
            if (selectedCard == null && cards.Length > 0)
                SelectCard(cards[0]);
            else if (selectedCard != null)
                SelectCard(selectedCard);
        }

        private void SelectCard(CardDefinition card)
        {
            if (card == null) return;
            if (selectedCard != null && selectedCard != card)
                CommitTimeline();
            selectedCard = card;
            selectedCard.EnsureEditorData();
            serializedCard = new SerializedObject(selectedCard);
            startupFramesProperty = serializedCard.FindProperty("startupFrames");
            activeFramesProperty = serializedCard.FindProperty("activeFrames");
            recoveryFramesProperty = serializedCard.FindProperty("recoveryFrames");
            timelineDrag = TimelineDrag.None;
            timelineDirty = false;
            previewFrame = Mathf.Clamp(previewFrame, 0, Mathf.Max(0, selectedCard.TimelineTotalFrames - 1));
            EditorUtility.SetDirty(selectedCard);
            AssetDatabase.SaveAssets();
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            HandlePlaybackShortcut();
            DrawToolbar();
            if (selectedCard == null || serializedCard == null)
            {
                EditorGUILayout.HelpBox("卡牌目录中没有找到带 CardDefinition 的预制体。", MessageType.Info);
                return;
            }

            serializedCard.UpdateIfRequiredOrScript();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawCardProperties();
            DrawTimeline();
            DrawPreviewControls();
            DrawPreview();
            EditorGUILayout.EndScrollView();
            ApplyCardChanges();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("刷新卡牌", EditorStyles.toolbarButton, GUILayout.Width(72)))
                    RefreshCards();

                int current = Array.IndexOf(cards, selectedCard);
                if (current >= 0 && cards.Length > 0)
                {
                    int next = EditorGUILayout.Popup(current, cardNames, EditorStyles.toolbarPopup, GUILayout.Width(150));
                    if (next != current)
                    {
                        SelectCard(cards[next]);
                        return;
                    }
                }

                var picked = (CardDefinition)EditorGUILayout.ObjectField(
                    selectedCard, typeof(CardDefinition), false, GUILayout.MinWidth(200));
                if (picked != selectedCard)
                {
                    SelectCard(picked);
                    return;
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button(previewPlaying ? "暂停预览" : "播放预览", EditorStyles.toolbarButton, GUILayout.Width(84)))
                    TogglePreview();
                GUILayout.Label("空格：播放/暂停", EditorStyles.toolbarButton, GUILayout.Width(92));
            }
        }

        private void HandlePlaybackShortcut()
        {
            var current = Event.current;
            if (current.type != EventType.KeyDown || current.keyCode != KeyCode.Space ||
                selectedCard == null || GUIUtility.hotControl != 0 || EditorGUIUtility.editingTextField)
                return;

            TogglePreview();
            current.Use();
        }

        private void TogglePreview()
        {
            previewPlaying = !previewPlaying;
            lastPreviewTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void DrawCardProperties()
        {
            EditorGUILayout.LabelField("卡牌属性", EditorStyles.boldLabel);
            Property("title", "卡名文本");
            Property("energyCost", "能量费用");

            var kindProperty = Property("kind", "招式类型", false);
            int kind = kindProperty.intValue;
            bool whiteHole = kindProperty.enumNames[kind] == "InfiniteBlueWhiteHole";
            bool bigBang = kindProperty.enumNames[kind] == "BigBang";
            var shape = (CardHitShape)Property("hitShape", "判定形状", false).enumValueIndex;
            if (!bigBang)
            {
                using (new EditorGUI.DisabledScope(whiteHole)) Property("hitShape", "判定形状");
                Property("distance", "范围 / 半径");
            }
            if (shape is CardHitShape.Arc or CardHitShape.Triangle) Property("sweepDegrees", "判定张角");
            if (shape == CardHitShape.Landing) Property("impactRadius", "落点伤害半径");
            if (shape == CardHitShape.Dash) Property("dashHalfWidth", "冲刺判定半宽");

            if (kind == 1)
            {
                Property("damage", "冲刺伤害");
                EditorGUILayout.HelpBox("冲锋沿当前指定方向加速，沿途进行冲刺范围判定；不会锁定目标或瞬移。", MessageType.Info);
            }
            else if (kind == 3)
            {
                Property("block", "基础格挡");
                Property("guardTint", "格挡时角色颜色");
            }
            else if (whiteHole)
            {
                Property("pushDistance", "推出距离");
                Property("block", "护盾获得量");
            }
            else
            {
                Property("damage", bigBang ? "未触发斩杀时的全场伤害" : "基础伤害");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("命中反馈", EditorStyles.boldLabel);
            if (kind != 3)
            {
                Property("shakeAmplitude", "镜头震幅");
                Property("shakeSeconds", "镜头震动时长 (秒)");
                Property("hitStopScale", "顿帧倍率");
                SecondsProperty("hitStopMs", "顿帧时长 (秒)", "内部仍以毫秒保存，避免影响现有卡牌数据。");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("特效", EditorStyles.boldLabel);
            Property("slashStartFrame", "特效开始帧");
            Property("slashFront", "前层特效序列");
            Property("slashBack", "后层特效序列");
        }

        private void DrawTimeline()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("60 帧 / 秒时间轴", EditorStyles.boldLabel);

            GetTimelineValues(out int startup, out int active, out int recovery);

            EditorGUILayout.LabelField("帧数直接输入（拖拽异常时可用）", EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(timelineDrag != TimelineDrag.None))
            using (new EditorGUILayout.HorizontalScope())
            {
                int editedStartup = EditorGUILayout.IntField("前摇", startup, GUILayout.MinWidth(150));
                int editedActive = EditorGUILayout.IntField("有效", active, GUILayout.MinWidth(150));
                int editedRecovery = EditorGUILayout.IntField("后摇", recovery, GUILayout.MinWidth(150));

                if (timelineDrag == TimelineDrag.None &&
                    (editedStartup != startup || editedActive != active || editedRecovery != recovery))
                {
                    startup = Mathf.Max(0, editedStartup);
                    active = Mathf.Max(1, editedActive);
                    recovery = Mathf.Max(0, editedRecovery);
                    startupFramesProperty.intValue = startup;
                    activeFramesProperty.intValue = active;
                    recoveryFramesProperty.intValue = recovery;
                }
            }

            int total = startup + active + recovery;
            int maxFrame = Mathf.Max(60, total + 60);

            var summary = $"前摇 {startup} 帧 / {startup / 60f:0.000} 秒    " +
                $"有效 {active} 帧 / {active / 60f:0.000} 秒    " +
                $"后摇 {recovery} 帧 / {recovery / 60f:0.000} 秒    " +
                $"总计 {total} 帧 / {total / 60f:0.000} 秒";
            EditorGUILayout.HelpBox(summary, MessageType.None);

            Rect timeline = GUILayoutUtility.GetRect(0, 112, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(timeline, new Color(0.09f, 0.1f, 0.12f));
            Rect bar = new Rect(timeline.x + 12, timeline.y + 25, timeline.width - 24, 44);
            float scale = bar.width / maxFrame;
            float startupX = bar.x + startup * scale;
            float activeX = bar.x + (startup + active) * scale;
            float totalX = bar.x + total * scale;

            EditorGUI.DrawRect(new Rect(bar.x, bar.y, Mathf.Max(1, startupX - bar.x), bar.height), new Color(0.26f, 0.34f, 0.52f));
            EditorGUI.DrawRect(new Rect(startupX, bar.y, Mathf.Max(1, activeX - startupX), bar.height), new Color(0.78f, 0.42f, 0.16f));
            EditorGUI.DrawRect(new Rect(activeX, bar.y, Mathf.Max(1, totalX - activeX), bar.height), new Color(0.25f, 0.42f, 0.32f));
            GUI.Label(new Rect(bar.x + 4, bar.y + 13, Mathf.Max(1, startupX - bar.x - 8), 18), "前摇", EditorStyles.whiteMiniLabel);
            GUI.Label(new Rect(startupX + 4, bar.y + 13, Mathf.Max(1, activeX - startupX - 8), 18), "有效", EditorStyles.whiteMiniLabel);
            GUI.Label(new Rect(activeX + 4, bar.y + 13, Mathf.Max(1, totalX - activeX - 8), 18), "后摇", EditorStyles.whiteMiniLabel);

            for (int frame = 0; frame <= maxFrame; frame += 10)
            {
                float x = bar.x + frame * scale;
                EditorGUI.DrawRect(new Rect(x, bar.y + bar.height + 2, 1, 6), new Color(0.65f, 0.68f, 0.72f));
                GUI.Label(new Rect(x - 18, bar.y + bar.height + 10, 44, 30), $"{frame}f\n{frame / 60f:0.###}s", EditorStyles.centeredGreyMiniLabel);
            }

            float previewX = bar.x + Mathf.Clamp(previewFrame, 0, maxFrame) * scale;
            EditorGUI.DrawRect(new Rect(previewX, bar.y - 8, 2, bar.height + 16), new Color(1f, 0.88f, 0.34f));
            DrawHandle(startupX, bar.y, new Color(0.45f, 0.7f, 1f));
            DrawHandle(activeX, bar.y, new Color(1f, 0.66f, 0.28f));
            DrawHandle(totalX, bar.y, new Color(0.5f, 0.9f, 0.64f));

            HandleTimelineDrag(bar, scale, maxFrame, startup, active, recovery, startupX, activeX, totalX);
        }

        private void DrawHandle(float x, float y, Color color)
        {
            EditorGUI.DrawRect(new Rect(x - 2, y - 8, 4, 60), color);
            EditorGUI.DrawRect(new Rect(x - 6, y - 10, 12, 4), color);
        }

        private void HandleTimelineDrag(Rect bar, float scale, int maxFrame, int startup, int active, int recovery,
            float startupX, float activeX, float totalX)
        {
            // Use a stable hint. A newly generated ID on every event can make hotControl
            // stop matching during MouseDrag, which presents as an intermittently stuck handle.
            timelineControlId = GUIUtility.GetControlID(TimelineControlHint, FocusType.Passive, bar);
            var current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0)
            {
                TimelineDrag selectedHandle = PickTimelineHandle(
                    bar, current.mousePosition, startupX, activeX, totalX);
                if (selectedHandle != TimelineDrag.None)
                {
                    timelineDrag = selectedHandle;
                    dragStartupFrames = startup;
                    dragActiveFrames = active;
                    dragRecoveryFrames = recovery;
                    timelineDirty = false;
                    GUIUtility.hotControl = timelineControlId;
                    current.Use();
                }
            }

            // Once this window owns a drag, continue handling it even if Unity changes
            // the active hotControl while processing a repaint/layout event.
            if (timelineDrag != TimelineDrag.None)
            {
                if (current.type == EventType.MouseDrag)
                {
                    int frame = Mathf.Clamp(Mathf.RoundToInt((current.mousePosition.x - bar.x) / scale), 0, maxFrame);
                    int nextStartup = dragStartupFrames;
                    int nextActive = dragActiveFrames;
                    int nextRecovery = dragRecoveryFrames;
                    int activeEnd = nextStartup + nextActive;
                    int total = nextStartup + nextActive + nextRecovery;
                    switch (timelineDrag)
                    {
                        case TimelineDrag.StartupEnd:
                            nextStartup = Mathf.Clamp(frame, 0, total - nextActive);
                            break;
                        case TimelineDrag.ActiveEnd:
                            activeEnd = Mathf.Clamp(frame, nextStartup + 1, total);
                            nextActive = activeEnd - nextStartup;
                            break;
                        case TimelineDrag.TotalEnd:
                            total = Mathf.Max(frame, nextStartup + nextActive);
                            nextRecovery = total - nextStartup - nextActive;
                            break;
                    }

                    if (nextStartup != dragStartupFrames || nextActive != dragActiveFrames ||
                        nextRecovery != dragRecoveryFrames)
                    {
                        dragStartupFrames = nextStartup;
                        dragActiveFrames = nextActive;
                        dragRecoveryFrames = nextRecovery;
                        timelineDirty = true;
                        previewFrame = Mathf.Clamp(previewFrame, 0,
                            Mathf.Max(0, nextStartup + nextActive + nextRecovery - 1));
                        Repaint();
                    }
                    current.Use();
                }
                else if (current.type == EventType.MouseUp)
                {
                    CommitTimeline();
                    timelineDrag = TimelineDrag.None;
                    GUIUtility.hotControl = 0;
                    current.Use();
                    Repaint();
                }
            }

            if (timelineDrag == TimelineDrag.None)
            {
                AddHandleCursor(startupX, bar);
                AddHandleCursor(activeX, bar);
                AddHandleCursor(totalX, bar);
            }
        }

        private static TimelineDrag PickTimelineHandle(Rect bar, Vector2 mousePosition,
            float startupX, float activeX, float totalX)
        {
            if (!bar.Contains(mousePosition)) return TimelineDrag.None;

            const float hitRadius = 14f;
            float startupDistance = Mathf.Abs(mousePosition.x - startupX);
            float activeDistance = Mathf.Abs(mousePosition.x - activeX);
            float totalDistance = Mathf.Abs(mousePosition.x - totalX);
            bool nearStartup = startupDistance <= hitRadius;
            bool nearActive = activeDistance <= hitRadius;
            bool nearTotal = totalDistance <= hitRadius;

            // When the active and recovery boundaries overlap (zero recovery), split
            // the hit area vertically so both handles remain reachable.
            if (nearActive && nearTotal && Mathf.Abs(activeX - totalX) < 1f)
                return mousePosition.y <= bar.center.y ? TimelineDrag.ActiveEnd : TimelineDrag.TotalEnd;

            TimelineDrag result = TimelineDrag.None;
            float nearest = hitRadius + 1f;
            if (nearStartup && startupDistance < nearest)
            {
                result = TimelineDrag.StartupEnd;
                nearest = startupDistance;
            }
            if (nearActive && activeDistance < nearest)
            {
                result = TimelineDrag.ActiveEnd;
                nearest = activeDistance;
            }
            if (nearTotal && totalDistance < nearest)
                result = TimelineDrag.TotalEnd;
            return result;
        }

        private static void AddHandleCursor(float x, Rect bar) =>
            EditorGUIUtility.AddCursorRect(new Rect(x - 14f, bar.y - 10f, 28f, bar.height + 20f),
                MouseCursor.ResizeHorizontal);

        private void CommitTimeline()
        {
            if (!timelineDirty) return;
            timelineDirty = false;

            startupFramesProperty.intValue = Mathf.Max(0, dragStartupFrames);
            activeFramesProperty.intValue = Mathf.Max(1, dragActiveFrames);
            recoveryFramesProperty.intValue = Mathf.Max(0, dragRecoveryFrames);
            serializedCard.ApplyModifiedProperties();
            selectedCard.EnsureEditorData();
            EditorUtility.SetDirty(selectedCard);
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
        }

        private void GetTimelineValues(out int startup, out int active, out int recovery)
        {
            if (timelineDrag != TimelineDrag.None)
            {
                startup = dragStartupFrames;
                active = dragActiveFrames;
                recovery = dragRecoveryFrames;
                return;
            }

            startup = Mathf.Max(0, startupFramesProperty.intValue);
            active = Mathf.Max(1, activeFramesProperty.intValue);
            recovery = Mathf.Max(0, recoveryFramesProperty.intValue);
        }

        private void DrawPreviewControls()
        {
            GetTimelineValues(out int startup, out int active, out int recovery);
            int total = Mathf.Max(1, startup + active + recovery);
            previewFrame = EditorGUILayout.IntSlider("预览帧", previewFrame, 0, total - 1);
            EditorGUILayout.LabelField($"当前时间 {previewFrame} 帧 / {previewFrame / 60f:0.000} 秒", EditorStyles.miniLabel);
        }

        private void DrawPreview()
        {
            Rect preview = GUILayoutUtility.GetRect(0, 250, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(preview, new Color(0.055f, 0.06f, 0.075f));
            Vector2 center = preview.center + new Vector2(0, 16);
            float maxRange = selectedCard.EditorHitShape == CardHitShape.Landing
                ? selectedCard.EditorImpactRadius + 1f
                : Mathf.Max(2f, selectedCard.EditorDistance + selectedCard.EditorDashHalfWidth + 0.5f);
            var kind = serializedCard.FindProperty("kind");
            var openingSprite = GetSprite(selectedCard.EditorSlashFront, 0);
            if (kind.enumNames[kind.intValue] == "BigBang" && openingSprite != null)
            {
                var extent = openingSprite.bounds.extents;
                maxRange = Mathf.Max(maxRange, Mathf.Max(extent.x, extent.y));
            }
            float scale = Mathf.Min((preview.width - 60f) / (maxRange * 2f), (preview.height - 60f) / (maxRange * 2f));
            GetTimelineValues(out int startup, out int activeFrames, out _);
            int activeStart = selectedCard.EditorHitShape == CardHitShape.Dash
                ? Mathf.Max(1, startup)
                : startup;
            bool active = previewFrame >= activeStart && previewFrame < activeStart + activeFrames;

            Handles.BeginGUI();
            CardScenePreview.DrawGui(selectedCard, center, scale, active);
            Handles.EndGUI();

            int effectIndex = previewFrame - selectedCard.EditorSlashStartFrame;
            DrawEffectLayer(GetSprite(selectedCard.EditorSlashBack, effectIndex), center, scale);
            DrawEffectLayer(GetSprite(selectedCard.EditorSlashFront, effectIndex), center, scale);

            GUI.Label(new Rect(preview.x + 12, preview.y + 10, preview.width - 24, 20),
                $"{selectedCard.name}    {selectedCard.EditorHitShape}    {(active ? "有效判定中" : "非判定帧")}",
                EditorStyles.whiteLabel);
            GUI.Label(new Rect(preview.x + 12, preview.yMax - 28, preview.width - 24, 20),
                "Scene View 中选择卡牌预制体或场景实例可查看同一判定形状。", EditorStyles.miniLabel);
        }

        private static void DrawEffectLayer(Sprite sprite, Vector2 center, float worldScale)
        {
            if (sprite == null) return;
            Rect source = sprite.textureRect;
            Vector2 size = source.size * (worldScale / sprite.pixelsPerUnit);
            Vector2 pivot = new Vector2(sprite.pivot.x, source.height - sprite.pivot.y) * (worldScale / sprite.pixelsPerUnit);
            var uv = new Rect(source.x / sprite.texture.width, source.y / sprite.texture.height,
                source.width / sprite.texture.width, source.height / sprite.texture.height);
            GUI.DrawTextureWithTexCoords(new Rect(center - pivot, size), sprite.texture, uv, true);
        }

        private static Sprite GetSprite(Sprite[] sprites, int index)
        {
            return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
        }

        private SerializedProperty Property(string name, string label, bool draw = true)
        {
            var property = serializedCard.FindProperty(name);
            if (draw) EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            return property;
        }

        private void SecondsProperty(string name, string label, string tooltip = "")
        {
            var property = serializedCard.FindProperty(name);
            float seconds = Mathf.Max(0f, property.floatValue * 0.001f);
            EditorGUI.BeginChangeCheck();
            seconds = EditorGUILayout.DelayedFloatField(new GUIContent(label, tooltip), seconds);
            if (EditorGUI.EndChangeCheck())
                property.floatValue = Mathf.Max(0f, Mathf.Round(seconds * 1000f));
        }

        private void ApplyCardChanges()
        {
            if (!serializedCard.ApplyModifiedProperties()) return;
            selectedCard.EnsureEditorData();
            EditorUtility.SetDirty(selectedCard);
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
        }

    }

    internal static class CardScenePreview
    {
        public static void Draw(CardDefinition card, bool showLabel)
        {
            if (card == null) return;
            Vector3 center = card.transform.position;
            Vector3 direction = card.transform.right.normalized;
            Color previous = Handles.color;
            Handles.color = new Color(0.98f, 0.62f, 0.2f, 0.86f);
            DrawWorldShape(card.EditorHitShape, center, direction, card.EditorDistance, card.EditorSweepDegrees,
                card.EditorDashHalfWidth, card.EditorTriangleHalfWidth);
            if (card.EditorHitShape == CardHitShape.Landing)
                Handles.DrawWireDisc(center + direction * card.EditorDistance, Vector3.forward, card.EditorImpactRadius);
            if (showLabel)
                Handles.Label(center + direction * (card.EditorDistance + 0.2f),
                    $"{card.name}  {card.EditorHitShape}  {card.TimelineActiveStartFrame}-{card.TimelineActiveEndFrame}f");
            Handles.color = previous;
        }

        public static void DrawGui(CardDefinition card, Vector2 center, float scale, bool active)
        {
            Color previous = Handles.color;
            Handles.color = active ? new Color(1f, 0.58f, 0.16f, 0.95f) : new Color(0.45f, 0.52f, 0.62f, 0.45f);
            if (card.EditorHitShape == CardHitShape.Landing)
                Handles.DrawWireDisc(center, Vector3.forward, card.EditorImpactRadius * scale);
            else
                DrawWorldShape(card.EditorHitShape, center, Vector3.right, card.EditorDistance * scale,
                    card.EditorSweepDegrees, card.EditorDashHalfWidth * scale, card.EditorTriangleHalfWidth * scale);
            Handles.color = previous;
            Handles.color = new Color(0.8f, 0.84f, 0.9f, 0.5f);
            Handles.DrawLine(center - Vector2.right * 4f, center + Vector2.right * 4f);
            Handles.DrawLine(center - Vector2.up * 4f, center + Vector2.up * 4f);
            Handles.color = previous;
        }

        private static void DrawWorldShape(CardHitShape shape, Vector3 center, Vector3 direction,
            float distance, float sweepDegrees, float dashHalfWidth, float triangleHalfWidth)
        {
            direction.z = 0f;
            direction.Normalize();
            Vector3 side = Vector3.Cross(Vector3.forward, direction).normalized;
            switch (shape)
            {
                case CardHitShape.Arc:
                    Handles.DrawWireArc(center, Vector3.forward, Quaternion.Euler(0, 0, -sweepDegrees * 0.5f) * direction, sweepDegrees, distance);
                    Handles.DrawLine(center, center + Quaternion.Euler(0, 0, sweepDegrees * 0.5f) * direction * distance);
                    Handles.DrawLine(center, center + Quaternion.Euler(0, 0, -sweepDegrees * 0.5f) * direction * distance);
                    break;
                case CardHitShape.Triangle:
                    Vector3 tip = center + direction * distance;
                    Handles.DrawPolyLine(center, tip + side * triangleHalfWidth, tip - side * triangleHalfWidth, center);
                    break;
                case CardHitShape.Circle:
                case CardHitShape.Landing:
                case CardHitShape.Lock:
                    Handles.DrawWireDisc(center, Vector3.forward, distance);
                    break;
                case CardHitShape.Dash:
                    Vector3 end = center + direction * distance;
                    Handles.DrawWireDisc(center, Vector3.forward, dashHalfWidth);
                    Handles.DrawWireDisc(end, Vector3.forward, dashHalfWidth);
                    Handles.DrawLine(center + side * dashHalfWidth, end + side * dashHalfWidth);
                    Handles.DrawLine(center - side * dashHalfWidth, end - side * dashHalfWidth);
                    break;
                case CardHitShape.None:
                    Handles.DrawWireDisc(center, Vector3.forward, distance);
                    break;
            }
        }
    }
}
