using System;
using System.Collections;
using Fighter403.Audio;
using Framework.FrameworkAudio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fighter403.UI
{
    internal sealed class Fighter403MainMenu : MonoBehaviour
    {
        private const string AssetsPath = "Fighter403/MainMenu/";
        private const string SettingsKey = "Fighter403.MainMenu.";
        private static readonly Color Ink = new Color32(8, 13, 19, 255);
        private static readonly Color Paper = new Color32(227, 236, 235, 255);
        private static readonly Color Cyan = new Color32(146, 219, 216, 255);
        private static readonly Color Muted = new Color32(160, 178, 189, 255);
        private static readonly Color Line = new Color32(53, 75, 86, 255);

        private enum ButtonIcon { None = -1, Start, Settings, Credits, Back, Chevron }

        private RectTransform screen, backdrop, content, gate, irisFrame, hints;
        private UnityEngine.UI.RawImage firewall, iris, focusSpark, firewallParticles;
        private Fighter403MenuParticles ambientParticles;
        private Texture2D buttonIcons;
        private readonly Sprite[,] buttonPlates = new Sprite[3, 3];
        private readonly UnityEngine.UI.RawImage[] leaves = new UnityEngine.UI.RawImage[3];
        private UnityEngine.UI.RawImage idleSweep;
        private Sprite openingStencil;
        private RectTransform opening;
        private readonly RectTransform[] pages = new RectTransform[3];
        private readonly Selectable[] firstChoices = new Selectable[3];
        private readonly UnityEngine.UI.Button[] entries = new UnityEngine.UI.Button[3];
        private readonly Selectable[] settingsRows = new Selectable[5];
        private TMP_FontAsset font;
        private TextMeshProUGUI warning, controlsHint, fullScreenLabel;
        private Func<string> beginRun;
        private Action handOff;
        private Selectable lastHomeChoice;
        private GameObject lastFocus;
        private Vector2 layoutSize, gateCenter;
        private Coroutine sparkAnimation;
        private bool openingRun, restoredRun;
        private int page;
        private int lastParticleFrame = -1;
        private float lastFocusCue;

        internal static Fighter403MainMenu Open(TMP_FontAsset menuFont, Func<string> start, Action finish)
        {
            var gateArt = Resources.Load<Texture2D>(AssetsPath + "Firewall");
            var title = Resources.Load<Texture2D>(AssetsPath + "Wordmark");
            var pixelFont = Resources.Load<TMP_FontAsset>(AssetsPath + "MenuPixelFont");
            if (pixelFont != null) menuFont = pixelFont;
            if (gateArt == null || title == null || menuFont == null)
            {
                Debug.LogError("Fighter403 main menu is missing its gate, title, or font.");
                return null;
            }

            var root = new GameObject("Fighter403 Main Menu", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referenceResolution = new Vector2(1600, 1000);
            scaler.matchWidthOrHeight = 1;
            var menu = root.AddComponent<Fighter403MainMenu>();
            menu.font = menuFont;
            menu.beginRun = start;
            menu.handOff = finish;
            menu.Build(gateArt, title);
            return menu;
        }

        private void Build(Texture2D gateArt, Texture2D title)
        {
            screen = (RectTransform)transform;
            backdrop = Stretch(screen, "Backdrop");
            Plate(backdrop, Ink, true);
            gate = Rect(backdrop, "Firewall Gate", Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, Vector2.one);
            firewall = Texture(Stretch(gate, "Steel Frame"), gateArt);

            var wellTexture = Resources.Load<Texture2D>(AssetsPath + "GateOpening");
            var leafTexture = Resources.Load<Texture2D>(AssetsPath + "GateLeaves");
            opening = Rect(gate, "Gate Opening", new Vector2(330f / 576f, 1f - 224f / 450f),
                new Vector2(.5f, .5f), Vector2.zero, Vector2.one);
            var openingMask = Plate(opening, Color.white);
            if (wellTexture != null)
            {
                openingStencil = Sprite.Create(wellTexture, new Rect(0, 0, wellTexture.width, wellTexture.height),
                    new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                openingMask.sprite = openingStencil;
                var mask = opening.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
            }
            else openingMask.enabled = false;
            Texture(Stretch(opening, "Opening"), wellTexture);
            for (int i = 0; i < leaves.Length; i++)
            {
                leaves[i] = Texture(Stretch(opening, "Gate Leaf " + (i + 1)), leafTexture);
                leaves[i].uvRect = new Rect(i / 3f, 0, 1f / 3, 1);
            }
            opening.gameObject.SetActive(false);
            idleSweep = Texture(Rect(gate, "Idle Sweep", opening.anchorMin, new Vector2(.5f, .5f),
                Vector2.zero, Vector2.one), Resources.Load<Texture2D>(AssetsPath + "IdleSweep"));
            idleSweep.gameObject.SetActive(idleSweep.texture != null);
            firewallParticles = Texture(Rect(gate, "Firewall Particles", opening.anchorMin, new Vector2(.5f, .5f),
                Vector2.zero, Vector2.one), Resources.Load<Texture2D>(AssetsPath + "FirewallParticles"));
            firewallParticles.gameObject.SetActive(firewallParticles.texture != null);
            var particleTiles = Resources.Load<Texture2D>(AssetsPath + "AmbientParticles");
            if (particleTiles != null)
            {
                ambientParticles = Stretch(backdrop, "Fullscreen Particles").gameObject.AddComponent<Fighter403MenuParticles>();
                ambientParticles.Initialize(particleTiles);
            }
            LoadButtonArt();

            CreateFrame(backdrop);
            content = Rect(backdrop, "Pages", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(620, 450));
            for (int i = 0; i < pages.Length; i++) pages[i] = Stretch(content, "Page " + (i + 1));
            pages[0].name = "Main";
            pages[1].name = "Settings";
            pages[2].name = "Credits";

            Texture(Place(pages[0], "Fighter403", 0, 326, 550, 550f * title.height / title.width), title);
            Rule(pages[0], 308);
            entries[0] = Button(pages[0], "Start Game", "开始游戏", new Vector2(0, 212), new Vector2(328, 64),
                StartGame, ButtonIcon.Start);
            entries[1] = Button(pages[0], "Open Settings", "设置", new Vector2(0, 138), new Vector2(328, 64),
                () => OpenPage(1), ButtonIcon.Settings);
            entries[2] = Button(pages[0], "Open Credits", "制作人", new Vector2(0, 64), new Vector2(328, 64),
                () => OpenPage(2), ButtonIcon.Credits);
            SetVerticalNavigation(entries);
            warning = Label(Place(pages[0], "Start Error", 1, 7, 490, 45), "", 19, Cyan);
            firstChoices[0] = entries[0];
            BuildSettings(pages[1]);

            Header(pages[2], "制作人");
            Label(Place(pages[2], "Producer", 0, 203, 530, 75), "魏旻顾（季闲）", 48, Paper);
            firstChoices[2] = BackButton(pages[2], 132);

            hints = Place(backdrop, "Controls", 0, 0, 420, 30);
            controlsHint = Label(Stretch(hints, "Keys"), "", 18, Muted);
            focusSpark = Texture(Rect(backdrop, "Focus Spark", Vector2.zero, new Vector2(.5f,.5f),
                Vector2.zero, new Vector2(82, 82)), Resources.Load<Texture2D>(AssetsPath + "FocusSpark"));
            focusSpark.gameObject.SetActive(false);

            irisFrame = Stretch(screen, "PixelFX Iris");
            iris = Texture(Stretch(irisFrame, "Curtain"), Resources.Load<Texture2D>(AssetsPath + "Iris"));
            irisFrame.gameObject.SetActive(false);
            if (PlayerPrefs.HasKey(SettingsKey + "Fullscreen"))
                ApplyFullScreen(PlayerPrefs.GetInt(SettingsKey + "Fullscreen") != 0);
            ShowPage(0);
        }

        private void BuildSettings(RectTransform panel)
        {
            Header(panel, "设置");
            settingsRows[0] = SliderRow(panel, "总音量", "Master", .80f, 251, value => AudioListener.volume = value);
            settingsRows[1] = SliderRow(panel, "音乐音量", "Music", .70f, 191,
                value => AudioManager.Instance.SetMusicMixVolume(value));
            settingsRows[2] = SliderRow(panel, "音效音量", "Effects", .75f, 131,
                value => AudioManager.Instance.AudioSettings(EAudioType.Sfx, value));
            Divider(panel, 127);
            Label(Place(panel, "Fullscreen Label", 0, 75, 190, 52), "全屏", 24, Paper);
            var button = Button(panel, "Fullscreen", "", new Vector2(393, 81), new Vector2(97, 42), ToggleFullScreen);
            fullScreenLabel = button.GetComponentInChildren<TextMeshProUGUI>();
            settingsRows[3] = button;
            settingsRows[4] = BackButton(panel, 12);
            SetVerticalNavigation(settingsRows);
            firstChoices[1] = settingsRows[0];
        }

        private Slider SliderRow(RectTransform panel, string label, string key, float fallback, float y,
            Action<float> apply)
        {
            Divider(panel, y + 53);
            Label(Place(panel, key + " Label", 0, y, 160, 53), label, 24, Paper);
            var value = Label(Place(panel, key + " Value", 417, y, 73, 53), "", 24, Cyan,
                TextAlignmentOptions.MidlineRight);
            var rect = Place(panel, key + " Slider", 164, y + 5, 242, 44);
            var slider = rect.gameObject.AddComponent<Slider>();
            Plate(rect, Color.clear, true);
            var track = Rect(rect, "Track", new Vector2(.5f, .5f), new Vector2(.5f,.5f), Vector2.zero,
                new Vector2(232, 4));
            Plate(track, Line);
            var fill = Stretch(track, "Fill");
            Plate(fill, Cyan);
            slider.fillRect = fill;

            var handleArea = Rect(rect, "Handle Travel", new Vector2(0, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(-10, 23));
            handleArea.anchorMax = new Vector2(1, .5f);
            var handle = Rect(handleArea, "Handle", new Vector2(0, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(12, 0));
            slider.targetGraphic = Plate(handle, Paper, true);
            slider.handleRect = handle;
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.wholeNumbers = true;
            float initial = ReadVolume(key, fallback);
            slider.SetValueWithoutNotify(Mathf.Round(initial * 100));
            value.text = slider.value.ToString("0") + "%";
            apply(initial);
            slider.onValueChanged.AddListener(number =>
            {
                float normalized = number / 100f;
                value.text = number.ToString("0") + "%";
                apply(normalized);
                PlayerPrefs.SetFloat(SettingsKey + key, normalized);
                if (key != "Music") GameSfx.Play(Sfx.MenuFocus, minimumInterval: .085f);
            });
            return slider;
        }

        private static float ReadVolume(string key, float fallback) =>
            Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsKey + key, fallback));

        private void ToggleFullScreen()
        {
            bool target = !Screen.fullScreen;
            ApplyFullScreen(target);
            PlayerPrefs.SetInt(SettingsKey + "Fullscreen", target ? 1 : 0);
            PlayerPrefs.Save();
            GameSfx.Play(Sfx.MenuConfirm);
        }

        private static void ApplyFullScreen(bool enabled)
        {
            Screen.fullScreenMode = enabled ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        private void Header(RectTransform panel, string title)
        {
            Label(Place(panel, "Heading", 0, 336, 550, 98), title, 72, Paper);
            Rule(panel, 322);
        }

        private void Rule(RectTransform panel, float y)
        {
            Plate(Place(panel, "Rule", 0, y, 430, 1), Line);
            Plate(Place(panel, "Rule Accent", 0, y - 1, 42, 3), Cyan);
        }

        private static void Divider(RectTransform panel, float y) =>
            Plate(Place(panel, "Setting Divider", 0, y, 490, 1), Line);

        private UnityEngine.UI.Button BackButton(RectTransform panel, float y) =>
            Button(panel, "Return", "返回", new Vector2(0, y), new Vector2(168, 48), Back, ButtonIcon.Back);

        private void Back()
        {
            if (page == 0 || openingRun) return;
            PlayerPrefs.Save();
            GameSfx.Play(Sfx.MenuBack);
            ShowPage(0);
        }

        private void OpenPage(int target)
        {
            lastHomeChoice = entries[target];
            GameSfx.Play(Sfx.MenuConfirm);
            ShowPage(target);
        }

        private void ShowPage(int target)
        {
            page = target;
            for (int i = 0; i < pages.Length; i++) pages[i].gameObject.SetActive(i == target);
            firewall.color = target == 0 ? Color.white : new Color(.49f, .53f, .56f);
            firewallParticles.color = new Color(1, 1, 1, target == 0 ? 1f : .32f);
            idleSweep.color = firewallParticles.color;
            if (ambientParticles != null)
                ambientParticles.color = new Color(1, 1, 1, target == 0 ? 1f : .34f);
            controlsHint.text = target == 0 ? "↑ ↓  选择     Enter  确认" : "Esc  返回";
            if (sparkAnimation != null) StopCoroutine(sparkAnimation);
            focusSpark.gameObject.SetActive(false);
            var initial = target == 0 && lastHomeChoice != null ? lastHomeChoice : firstChoices[target];
            lastFocus = initial.gameObject;
            EventSystem.current?.SetSelectedGameObject(lastFocus);
        }

        private void Update()
        {
            if (openingRun || restoredRun) return;
            if (page == 1) fullScreenLabel.text = Screen.fullScreen ? "开启" : "关闭";
            if (page != 0 && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                Back();
                return;
            }

            var events = EventSystem.current;
            var selected = events?.currentSelectedGameObject;
            if (selected == null)
            {
                if (lastFocus != null && lastFocus.activeInHierarchy) events?.SetSelectedGameObject(lastFocus);
                return;
            }
            if (selected == lastFocus) return;
            lastFocus = selected;
            if (selected.transform.IsChildOf(pages[page])) CueFocus(selected.transform as RectTransform);
        }

        private void LateUpdate()
        {
            if (screen.rect.size != layoutSize) Arrange(screen.rect.size);
            if (restoredRun) return;
            float time = Time.unscaledTime;
            if (ambientParticles != null) ambientParticles.Advance(time);
            idleSweep.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -time * 26f);
            int frame = (int)(time * 15f) % 72;
            if (frame == lastParticleFrame) return;
            lastParticleFrame = frame;
            SheetFrame(firewallParticles, frame, 9, 8);
        }

        private void Arrange(Vector2 size)
        {
            if (size.x <= 1 || size.y <= 1) return;
            layoutSize = size;
            bool narrow = size.x < size.y * 1.05f;
            float left = size.x * (narrow ? .08f : .071f);
            float width = narrow ? Mathf.Max(470, size.x * 1.29f) : Mathf.Min(size.x * .81f, size.y * 1.31f);
            gateCenter = new Vector2(size.x * (narrow ? .50f : .72f), size.y * (narrow ? .70f : .50f));
            float gateHeight = width * 450f / 576f;
            gate.sizeDelta = new Vector2(width, gateHeight);
            gate.anchoredPosition = gateCenter - new Vector2(width * (330f / 576f - .5f),
                gateHeight * (.5f - 224f / 450f));
            opening.sizeDelta = Vector2.one * (width * 220f / 576f);
            idleSweep.rectTransform.sizeDelta = Vector2.one * (width * 340f / 576f);
            firewallParticles.rectTransform.sizeDelta = Vector2.one * (width * 400f / 576f);
            float scale = Mathf.Min(1.15f, (size.x - left * 2) / (narrow ? 600 : 740));
            if (narrow) scale = Mathf.Min(scale, size.y * .44f / 450);
            content.localScale = Vector3.one * scale;
            content.anchoredPosition = new Vector2(left, narrow ? size.y * .12f : size.y * .50f - 225 * scale);
            hints.anchoredPosition = new Vector2(left, size.y * .055f);
        }

        private void CueFocus(RectTransform item)
        {
            if (openingRun || item == null || Time.unscaledTime - lastFocusCue < .085f) return;
            lastFocusCue = Time.unscaledTime;
            GameSfx.Play(Sfx.MenuFocus);
            if (page != 0 || focusSpark.texture == null) return;
            if (sparkAnimation != null) StopCoroutine(sparkAnimation);
            Vector3 edge = item.TransformPoint(new Vector3(item.rect.xMax - 33, item.rect.center.y));
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, edge);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(backdrop, screenPoint, null, out var local);
            focusSpark.rectTransform.anchoredPosition = local - backdrop.rect.min;
            sparkAnimation = StartCoroutine(PlaySpark());
        }

        private IEnumerator PlaySpark()
        {
            focusSpark.gameObject.SetActive(true);
            float started = Time.unscaledTime;
            for (int frame = 0; frame < 16; frame = (int)((Time.unscaledTime - started) * 43f))
            {
                SheetFrame(focusSpark, frame, 4, 4);
                yield return null;
            }
            focusSpark.gameObject.SetActive(false);
            sparkAnimation = null;
        }

        private void StartGame()
        {
            if (openingRun) return;
            openingRun = true;
            warning.text = "";
            PlayerPrefs.Save();
            GameSfx.Play(Sfx.MenuStart);
            EventSystem.current?.SetSelectedGameObject(null);
            if (sparkAnimation != null) StopCoroutine(sparkAnimation);
            focusSpark.gameObject.SetActive(false);
            StartCoroutine(PlayEntry());
        }

        private IEnumerator PlayEntry()
        {
            opening.gameObject.SetActive(true);
            bool useIris = iris.texture != null;
            if (useIris) irisFrame.gameObject.SetActive(true);
            var fallback = irisFrame.GetComponent<UnityEngine.UI.Image>();
            if (!useIris)
            {
                if (fallback == null) fallback = Plate(irisFrame, Color.clear);
                fallback.color = Color.clear;
                iris.gameObject.SetActive(false);
                irisFrame.gameObject.SetActive(true);
            }

            const float framesPerSecond = 46f;
            float start = Time.unscaledTime;
            Vector2[] directions = { new(.27f, .16f), new(0, -.31f), new(-.27f, .16f) };
            string failure = null;
            while (true)
            {
                int frame = Mathf.Min(53, (int)((Time.unscaledTime - start) * framesPerSecond));
                if (useIris) SheetFrame(iris, frame, 7, 10);
                else
                {
                    float amount = frame < 30 ? frame / 30f : (54 - frame) / 24f;
                    fallback.color = new Color(Ink.r, Ink.g, Ink.b, amount);
                }
                float slide = Mathf.SmoothStep(0, 1, (frame - 3) / 25f);
                for (int i = 0; i < leaves.Length; i++)
                    leaves[i].rectTransform.anchoredPosition = directions[i] * (opening.rect.width * slide);

                if (!restoredRun && failure == null && frame >= 30)
                {
                    failure = beginRun();
                    if (failure == null)
                    {
                        restoredRun = true;
                        backdrop.gameObject.SetActive(false);
                    }
                }

                if (frame == 53) break;
                yield return null;
            }

            if (restoredRun)
            {
                handOff();
                Destroy(gameObject);
                yield break;
            }

            for (int i = 0; i < leaves.Length; i++) leaves[i].rectTransform.anchoredPosition = Vector2.zero;
            opening.gameObject.SetActive(false);
            irisFrame.gameObject.SetActive(false);
            openingRun = false;
            warning.text = failure ?? "暂时无法开始游戏，请重试。";
            lastHomeChoice = entries[0];
            ShowPage(0);
        }

        private void OnDestroy()
        {
            if (openingStencil != null) Destroy(openingStencil);
            foreach (Sprite plate in buttonPlates)
                if (plate != null) Destroy(plate);
        }

        private static void SheetFrame(UnityEngine.UI.RawImage graphic, int frame, int columns, int rows)
        {
            float width = 1f / columns;
            float height = 1f / rows;
            graphic.uvRect = new Rect(frame % columns * width, 1f - (frame / columns + 1) * height, width, height);
        }

        private void LoadButtonArt()
        {
            buttonIcons = Resources.Load<Texture2D>(AssetsPath + "ButtonIcons");
            var atlas = Resources.Load<Texture2D>(AssetsPath + "ButtonPlates");
            if (atlas == null) return;
            const int rowHeight = 32;
            const int columnWidth = 164;
            for (int row = 0; row < 3; row++)
            {
                int width = row == 2 ? 64 : columnWidth;
                var borders = row == 2 ? new Vector4(7, 5, 9, 6) : new Vector4(32, 5, 26, 6);
                for (int state = 0; state < 3; state++)
                {
                    var tile = new Rect(state * columnWidth, (2 - row) * rowHeight, width, rowHeight);
                    buttonPlates[row, state] = Sprite.Create(atlas, tile, new Vector2(.5f, .5f), 100, 0,
                        SpriteMeshType.FullRect, borders);
                }
            }
        }

        private UnityEngine.UI.Button Button(RectTransform parent, string name, string label, Vector2 position,
            Vector2 size, Action click, ButtonIcon icon = ButtonIcon.None)
        {
            var rect = Place(parent, name, position.x, position.y, size.x, size.y);
            var face = Plate(rect, Color.white, true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = face;
            int row = icon == ButtonIcon.None ? 2 : icon == ButtonIcon.Start ? 1 : 0;
            face.sprite = buttonPlates[row, 0];
            if (face.sprite != null)
            {
                face.type = UnityEngine.UI.Image.Type.Sliced;
                face.pixelsPerUnitMultiplier = .5f;
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = buttonPlates[row, 1],
                    selectedSprite = buttonPlates[row, 1],
                    pressedSprite = buttonPlates[row, 2],
                };
            }
            else
            {
                var colors = button.colors;
                colors.normalColor = Line;
                colors.highlightedColor = new Color32(49, 79, 91, 255);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color32(36, 61, 73, 255);
                button.colors = colors;
            }
            button.onClick.AddListener(() => { if (!openingRun) click(); });

            if (icon == ButtonIcon.None)
                Label(Stretch(rect, "Label"), label, 24, Paper, TextAlignmentOptions.Center);
            else
            {
                Icon(rect, icon, 35, size.y * .5f, 20, Cyan);
                Label(Place(rect, "Label", 69, 0, size.x - 102, size.y), label,
                    icon == ButtonIcon.Back ? 24 : 30, Paper);
                if (size.x > 200) Icon(rect, ButtonIcon.Chevron, size.x - 31, size.y * .5f, 16, Muted);
            }

            var events = rect.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => CueFocus(rect));
            events.triggers.Add(entry);
            return button;
        }

        private void Icon(RectTransform parent, ButtonIcon icon, float x, float y, float size, Color tint)
        {
            if (buttonIcons == null) return;
            var rect = Rect(parent, icon.ToString(), Vector2.zero, new Vector2(.5f, .5f),
                new Vector2(x, y), Vector2.one * size);
            var graphic = Texture(rect, buttonIcons);
            graphic.color = tint;
            graphic.uvRect = new Rect((int)icon / 5f, 0, 1f / 5f, 1);
        }

        private static void SetVerticalNavigation(Selectable[] choices)
        {
            for (int i = 0; i < choices.Length; i++)
            {
                choices[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = choices[(i + choices.Length - 1) % choices.Length],
                    selectOnDown = choices[(i + 1) % choices.Length],
                };
            }
        }

        private static void CreateFrame(RectTransform parent)
        {
            var frame = Stretch(parent, "Frame");
            frame.offsetMin = new Vector2(42, 42);
            frame.offsetMax = new Vector2(-42, -42);
            var tone = new Color32(82, 112, 122, 88);
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2(i % 2, i / 2);
                Plate(Rect(frame, "Horizontal Corner", corner, corner, Vector2.zero, new Vector2(28, 1)), tone);
                Plate(Rect(frame, "Vertical Corner", corner, corner, Vector2.zero, new Vector2(1, 28)), tone);
            }
        }

        private TextMeshProUGUI Label(RectTransform rect, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.text = text;
            return label;
        }

        private static UnityEngine.UI.Image Plate(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static UnityEngine.UI.RawImage Texture(RectTransform rect, Texture2D texture)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Place(RectTransform parent, string name, float x, float y, float w, float h) =>
            Rect(parent, name, Vector2.zero, Vector2.zero, new Vector2(x, y), new Vector2(w, h));

        private static RectTransform Stretch(RectTransform parent, string name)
        {
            var rect = Rect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            rect.anchorMax = Vector2.one;
            return rect;
        }

        private static RectTransform Rect(RectTransform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}

