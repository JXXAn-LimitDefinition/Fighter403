using System;
using System.Globalization;
using Fighter403.Audio;
using Fighter403.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fighter403.UI
{
    internal sealed partial class Fighter403MainMenu
    {
        private const string IntroSeenKey = SettingsKey + "IntroSeen";
        private const int IntroCombatPage = 1;
        private const float ComboStepSeconds = .52f;
        private static readonly Vector2 IntroSize = new(936, 540);
        private static readonly Color IntroSurface = new Color32(17, 29, 38, 255);
        private static readonly string[] IntroTitles = { "探索目标", "走位与出牌", "局内成长" };

        private TMP_FontAsset introFont;
        private RectTransform introRoot, introFrame;
        private readonly RectTransform[] introPages = new RectTransform[IntroTitles.Length];
        private TextMeshProUGUI introTitle, introProgress, introNextLabel;
        private Button tutorialEntry, introSkip, introPrevious, introNext;
        private TextMeshProUGUI[] introMultipliers;
        private Image[] introComboIcons;
        private RawImage introComboSpark;
        private int introPage, introComboStep;
        private float introPageStarted;
        private bool introStartsRun;

        private void OpenIntro(bool fromStart)
        {
            if (openingRun || introRoot != null) return;
            introStartsRun = fromStart;
            GameSfx.Play(Sfx.MenuConfirm);
            if (sparkAnimation != null) StopCoroutine(sparkAnimation);
            focusSpark.gameObject.SetActive(false);
            content.gameObject.SetActive(false);
            hints.gameObject.SetActive(false);

            introRoot = Stretch(screen, "First Run Intro");
            Plate(introRoot, new Color32(8, 13, 19, 247), true);
            entryBlocker.transform.SetAsLastSibling();
            introFrame = Rect(introRoot, "Briefing", Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, IntroSize);
            ArrangeIntro(screen.rect.size);
            introTitle = IntroLabel(introFrame, "Intro Title", "", 0, 440, 738, 86, 54, Paper);
            introProgress = IntroLabel(introFrame, "Page Number", "", 776, 456, 160, 52, 27, Cyan,
                TextAlignmentOptions.MidlineRight);
            Plate(Place(introFrame, "Header Rule", 0, 430, IntroSize.x, 2), Line);
            Plate(Place(introFrame, "Footer Rule", 0, 82, IntroSize.x, 2), Line);

            for (int i = 0; i < introPages.Length; i++)
                introPages[i] = Stretch(introFrame, IntroTitles[i]);
            BuildIntroExploration(introPages[0]);
            BuildIntroCombat(introPages[IntroCombatPage]);
            BuildIntroGrowth(introPages[2]);

            introSkip = IntroButton("Skip Intro", fromStart ? "跳过介绍" : "返回主界面", 0,
                () => FinishIntro(completed: false));
            introPrevious = IntroButton("Previous Intro Page", "上一页", 545, () => ShowIntroPage(introPage - 1));
            introNext = IntroButton("Next Intro Page", "", 745, AdvanceIntro);
            introNextLabel = introNext.GetComponentInChildren<TextMeshProUGUI>();
            ShowIntroPage(0);
        }

        private void BuildIntroExploration(RectTransform panel)
        {
            IntroLabel(panel, "Exploration", "在地图上选择前进路线。\n击败每层首领，逐步深入内核。",
                0, 304, IntroSize.x, 108, 32, Paper);

            for (int i = 0; i < DungeonGenerator.FloorCount; i++)
            {
                float x = i * 326;
                Plate(Place(panel, "Floor", x, 146, 284, 120), IntroSurface);
                IntroLabel(panel, "Floor Number", $"第 {i + 1} 层", x + 21, 213, 236, 36, 21, Cyan);
                IntroLabel(panel, "Floor Theme", DungeonGenerator.ThemeLabels[i], x + 21, 166, 236, 44, 30, Paper);
                if (i + 1 < DungeonGenerator.FloorCount)
                    Plate(Place(panel, "Route", x + 284, 205, 42, 2), Line);
            }
        }

        private void BuildIntroCombat(RectTransform panel)
        {
            IntroRow(panel, "WASD", "实时移动，闪避敌人的攻击。", 341, 164, 390);
            IntroRow(panel, "鼠标左键", "单击敌人，锁定指定目标。\n再次点击同一敌人可取消。", 267, 164, 390);
            IntroRow(panel, "鼠标右键", "锁定屏幕内最近的敌人。\n再按一次，取消锁定。", 193, 164, 390);
            IntroRow(panel, "空格", "点击卡牌，按顺序加入队列。\n锁定后按空格，消耗能量出牌。", 119, 164, 390);

            var combo = Place(panel, "Combo Example", 568, 138, 368, 254);
            Plate(combo, IntroSurface);
            IntroLabel(combo, "Combo Title", "同属性连击", 20, 189, 328, 48, 30, Paper);
            IntroLabel(combo, "Combo Rule", "连续打出同主属性的卡牌，\n提高连击倍率。", 20, 17, 328, 72, 24, Muted);

            var attribute = Resources.Load<Sprite>("Fighter403/CardAttributes/Triangle");
            int slotCount = CombatRules.SameClassCardsForMaxMultiplier + 1;
            introMultipliers = new TextMeshProUGUI[slotCount];
            introComboIcons = new Image[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                float x = 20 + i * 84;
                var slot = Place(combo, "Combo Card", x, 95, 76, 90);
                Plate(slot, Ink);
                var icon = Plate(Place(slot, "Attack Attribute", 25, 48, 26, 26), Color.white);
                icon.sprite = attribute;
                icon.enabled = attribute != null;
                introComboIcons[i] = icon;
                string multiplier = CombatRules.NextMultiplier(i).ToString("0.##", CultureInfo.InvariantCulture);
                introMultipliers[i] = IntroLabel(slot, "Multiplier", "×" + multiplier, 0, 5, 76, 36, 23, Muted,
                    TextAlignmentOptions.Center);
            }
            introComboSpark = Texture(Place(combo, "Combo PixelFX Spark", 20, 124, 76, 76),
                focusSpark.texture as Texture2D);
            introComboSpark.enabled = false;
        }

        private void BuildIntroGrowth(RectTransform panel)
        {
            IntroRow(panel, "房间奖励", "从奖励中挑选新卡，补充你的牌组。", 328, 237, 660);
            IntroRow(panel, "商人", "用数据碎片购买道具，或移除牌组里的卡。", 248, 237, 660);
            IntroRow(panel, "数据注入器", "获得后立即生效，为本局提供强化。", 168, 237, 660);
        }

        private void IntroRow(RectTransform panel, string heading, string body, float y, float bodyX, float bodyWidth)
        {
            Plate(Place(panel, "Row Rule", 0, y + 5, 2, 62), Line);
            IntroLabel(panel, "Topic", heading, 16, y, bodyX - 28, 72, 27, Cyan);
            IntroLabel(panel, "Description", body, bodyX, y, bodyWidth, 72, 24, Paper);
        }

        private TextMeshProUGUI IntroLabel(RectTransform parent, string name, string text, float x, float y,
            float width, float height, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var label = Label(Place(parent, name, x, y, width, height), "", size, color, alignment);
            label.font = introFont;
            label.text = text;
            return label;
        }

        private Button IntroButton(string name, string text, float x, Action onClick)
        {
            var button = Button(introFrame, name, "", new Vector2(x, 4), new Vector2(191, 58), onClick);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.font = introFont;
            label.text = text;
            return button;
        }

        private void ArrangeIntro(Vector2 size)
        {
            float scale = Mathf.Min(size.x * .9f / IntroSize.x, size.y * .88f / IntroSize.y, 1.25f);
            introFrame.localScale = Vector3.one * scale;
        }

        private void ShowIntroPage(int index)
        {
            introPage = index;
            introPageStarted = Time.unscaledTime;
            introComboStep = -1;
            introComboSpark.enabled = false;
            for (int i = 0; i < introPages.Length; i++) introPages[i].gameObject.SetActive(i == index);
            introTitle.text = IntroTitles[index];
            introProgress.text = $"{index + 1} / {introPages.Length}";
            bool lastPage = index == introPages.Length - 1;
            introNextLabel.text = lastPage ? (introStartsRun ? "进入游戏" : "返回主界面") : "下一页";

            bool canGoBack = index > 0;
            bool showSkip = introStartsRun || !lastPage;
            introSkip.gameObject.SetActive(showSkip);
            introPrevious.gameObject.SetActive(canGoBack);
            introSkip.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnRight = canGoBack ? introPrevious : introNext };
            introPrevious.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = showSkip ? introSkip : null, selectOnRight = introNext };
            introNext.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = canGoBack ? introPrevious : introSkip };
            EventSystem.current?.SetSelectedGameObject(introNext.gameObject);
            if (index == IntroCombatPage) UpdateIntroCombo();
        }

        private void AdvanceIntro()
        {
            if (introPage == introPages.Length - 1)
            {
                FinishIntro(completed: true);
                return;
            }
            ShowIntroPage(introPage + 1);
        }

        private void UpdateIntro()
        {
            if (!introStartsRun && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                FinishIntro(completed: false);
                return;
            }
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == null)
                events.SetSelectedGameObject(introNext.gameObject);
            if (introPage == IntroCombatPage) UpdateIntroCombo();
        }

        private void UpdateIntroCombo()
        {
            float time = (Time.unscaledTime - introPageStarted) % (introMultipliers.Length * ComboStepSeconds + 1.25f);
            int step = Mathf.Min((int)(time / ComboStepSeconds), introMultipliers.Length - 1);
            if (step != introComboStep)
            {
                introComboStep = step;
                for (int i = 0; i < introMultipliers.Length; i++)
                {
                    introMultipliers[i].color = i == step ? Cyan : i < step ? Paper : Muted;
                    introComboIcons[i].color = i <= step ? Color.white : new Color(1, 1, 1, .3f);
                }
                introComboSpark.rectTransform.anchoredPosition = new Vector2(20 + step * 84, 124);
            }
            int frame = (int)((time - step * ComboStepSeconds) * FocusSparkFramesPerSecond);
            introComboSpark.enabled = introComboSpark.texture != null && frame < FocusSparkFrameCount;
            if (introComboSpark.enabled)
                SheetFrame(introComboSpark, frame, FocusSparkGridSize, FocusSparkGridSize);
        }

        private void FinishIntro(bool completed)
        {
            if (introStartsRun || completed)
                PlayerPrefs.SetInt(IntroSeenKey, 1);
            if (introStartsRun)
            {
                BeginEntry();
                return;
            }
            if (completed) PlayerPrefs.Save();
            CloseIntro();
            GameSfx.Play(Sfx.MenuBack);
            lastHomeChoice = tutorialEntry;
            ShowPage(0);
        }

        private void CloseIntro()
        {
            introRoot.gameObject.SetActive(false);
            Destroy(introRoot.gameObject);
            introRoot = null;
            content.gameObject.SetActive(true);
            hints.gameObject.SetActive(true);
        }
    }
}
