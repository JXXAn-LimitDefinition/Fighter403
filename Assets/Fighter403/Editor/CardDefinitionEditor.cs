using Fighter403.UI;
using UnityEditor;
using UnityEngine;

namespace Fighter403.EditorTools
{
    [CustomEditor(typeof(CardDefinition))]
    public sealed class CardDefinitionEditor : Editor
    {
        private void OnEnable()
        {
            var card = target as CardDefinition;
            if (card == null) return;
            card.EnsureEditorData();
            EditorUtility.SetDirty(card);
        }

        public override void OnInspectorGUI()
        {
            var card = (CardDefinition)target;
            card.EnsureEditorData();
            serializedObject.Update();

            var kindProperty = serializedObject.FindProperty("kind");
            int kind = kindProperty.intValue;
            bool dash = kind == 1;
            bool guard = kind == 3;
            bool sandevistan = kindProperty.enumNames[kind] == "Sandevistan";
            bool flurry = kindProperty.enumNames[kind] == "ThousandSlashes";
            bool blackHole = kindProperty.enumNames[kind] == "LimitedRedBlackHole";
            bool dualThread = kindProperty.enumNames[kind] == "DualThread";
            bool whiteHole = kindProperty.enumNames[kind] == "InfiniteBlueWhiteHole";
            bool bigBang = kindProperty.enumNames[kind] == "BigBang";
            var shape = (CardHitShape)serializedObject.FindProperty("hitShape").enumValueIndex;

            using (new EditorGUI.DisabledScope(true)) Field("kind", "招式类型");
            Field("title", "卡名文本", "卡面、牌堆和悬浮提示共用的名称来源。");

            Heading("属性");
            Field("energyCost", "能量费用", "出牌消耗的能量点数。填 0 可免费出牌。");
            Field("majorAttribute", "大属性");
            Field("minorAttribute", "小属性");
            Field("secondMinorAttribute", "第二小属性");
            Field("comboCount", "连击计数", "实际出牌一次时，各参与连击属性计入的牌数。");
            Field("countMinorAttributesForCombo", "小属性参与连击", "每个不同的小属性也视为主属性参与同属性倍率和千击万斩的判定。");
            if (sandevistan) Field("consumeUntilKill", "击杀前消耗");
            Heading("判定与数值");
            if (!sandevistan && !dualThread && !bigBang)
            {
                using (new EditorGUI.DisabledScope(flurry || blackHole || whiteHole))
                    Field("hitShape", "判定形状", "Scene View 和实战判定共用此形状。");
            }
            string distanceLabel = shape switch
            {
                CardHitShape.Dash => "冲刺距离",
                CardHitShape.Lock or CardHitShape.Landing => "锁定范围",
                CardHitShape.Triangle => "三角判定长度",
                _ => "判定半径"
            };
            if (!guard && !sandevistan && !flurry && !dualThread && !bigBang)
                Field("distance", distanceLabel, "场景单位。瞄准和实战共用此参数。");
            if (whiteHole) Field("pushDistance", "推出距离", "沿远离施法中心的方向推出敌人，单位为场景单位。");
            if (shape is CardHitShape.Arc or CardHitShape.Triangle) Field("sweepDegrees", "判定张角 (°)");
            if (shape == CardHitShape.Landing) Field("impactRadius", "落点伤害半径");
            if (kind == 0) Field("fractureBonus", "裂痕追加伤害");
            if (kind == 2 || flurry) Field("pulses", "伤害段数");
            if (!dash && !flurry && !whiteHole && !bigBang) Field("effectSeconds", blackHole ? "眩晕时长 (游戏秒)" :
                sandevistan || dualThread ? "增益时长 (现实秒)" : "附加效果时长 (游戏秒)");
            if (shape == CardHitShape.Dash) Field("dashHalfWidth", "冲刺判定半宽", "冲刺路径两侧各扩展此距离，首尾为圆角。");
            if (guard) Field("block", "基础格挡");
            else if (whiteHole) Field("block", "护盾获得量");
            else if (bigBang) Field("damage", "未触发斩杀时的全场伤害");
            else if (!sandevistan && !dualThread) Field("damage", "基础伤害");
            if (flurry) Field("finisherDamage", "终斩伤害");
            if (dash)
                EditorGUILayout.HelpBox("冲锋沿当前指定方向加速，沿途进行冲刺范围判定；不会锁定目标或瞬移。", MessageType.Info);

            Heading("招式时序 (60 帧/秒)");
            Field("startupFrames", "前摇帧数");
            Field("activeFrames", "有效判定帧数");
            Field("recoveryFrames", "后摇帧数");
            ShowTimelineSummary(card);

            if (!guard && !sandevistan && !dualThread)
            {
                Heading("命中反馈");
                Field("shakeAmplitude", "镜头震幅", "世界单位；填写 0 关闭震动。");
                Field("shakeSeconds", "镜头震动时长 (秒)", "按真实时间衰减，不受顿帧影响。");
                Field("hitStopScale", "顿帧倍率", "0 表示完全暂停，1 表示不减速。");
                SecondsField("hitStopMs", "顿帧时长 (秒)", "使用真实时间计时；同一招打中多人也只触发一次。0 表示关闭。");
            }

            Heading("招式表现");
            if (sandevistan) Field("sandevistanVfx", "Sandevistan 特效");
            if (guard) Field("guardTint", "格挡时角色颜色");
            Field("slashStartFrame", "特效开始帧");
            Field("slashFront", flurry ? "单次斩击特效序列" : "前层特效序列");
            if (flurry)
            {
                Field("blinkEffect", "瞬移特效序列");
                Field("finisherEffect", "终斩特效序列");
            }
            else Field("slashBack", "后层特效序列");

            EditorGUILayout.Space();
            if (GUILayout.Button("打开卡牌时间轴编辑器"))
                CardTimelineEditorWindow.Open(card);

            serializedObject.ApplyModifiedProperties();
            SceneView.RepaintAll();
        }

        private static void ShowTimelineSummary(CardDefinition card)
        {
            EditorGUILayout.HelpBox(
                $"前摇 {card.TimelineStartupFrames} 帧 ({card.TimelineStartupFrames / 60f:0.000} 秒)    " +
                $"有效 {card.TimelineActiveFrames} 帧 ({card.TimelineActiveFrames / 60f:0.000} 秒)    " +
                $"后摇 {card.TimelineRecoveryFrames} 帧 ({card.TimelineRecoveryFrames / 60f:0.000} 秒)\n" +
                $"总时长 {card.TimelineTotalFrames} 帧 ({card.TimelineTotalFrames / 60f:0.000} 秒)",
                MessageType.None);
        }

        private static void Heading(string title)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void Field(string name, string label, string tooltip = "") =>
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label, tooltip), true);

        private void SecondsField(string name, string label, string tooltip = "")
        {
            var property = serializedObject.FindProperty(name);
            float seconds = Mathf.Max(0f, property.floatValue * 0.001f);
            EditorGUI.BeginChangeCheck();
            seconds = EditorGUILayout.DelayedFloatField(new GUIContent(label, tooltip), seconds);
            if (EditorGUI.EndChangeCheck())
                property.floatValue = Mathf.Max(0f, Mathf.Round(seconds * 1000f));
        }

        private void OnSceneGUI()
        {
            CardScenePreview.Draw((CardDefinition)target, true);
        }
    }
}
