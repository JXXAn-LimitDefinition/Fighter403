using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Framework.FrameworkAudio;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
internal static class Fighter403MainMenuVerification
{
    private const string Trigger = "Temp/Fighter403MainMenu.verify";
    private const string Report = "Temp/Fighter403MainMenu.result";
    private const string CapturePath = "Temp/Fighter403MainMenuAmbientCapture";
    private const string EntryCapturePath = "Temp/Fighter403MainMenuEntryCapture";
    private const string SimulatedFailure = "暂时无法开始游戏，请重试。";
    private const int CaptureFrames = 64;
    private const string Prefix = "Fighter403.MainMenu.";
    private const string Started = "Fighter403AmbientVerifyStarted";
    private static double next;
    private static double menuDeadline;
    private static double entryDeadline;
    private static double finishCaptureAt;
    private static int step;
    private static int captured;
    private static int entryLoadAttempts;
    private static float oldEffects;
    private static Rect firstParticleFrame;
    private static Vector3[] firstAmbientVertices;
    private static bool hadEffects;
    private static bool changedEffects;
    private static bool coveredAtLoad;
    private static bool roomReadyUnderCover;

    static Fighter403MainMenuVerification()
    {
        EditorApplication.update += Tick;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        File.AppendAllText(Report, "OK " + message + Environment.NewLine);
    }

    private static Button Button(string name) => GameObject.Find(name)?.GetComponent<Button>();
    private static Graphic Ambient() => GameObject.Find("Fullscreen Particles")?.GetComponent<Graphic>();
    private static RawImage Shutter() => GameObject.Find("PixelFX Shutter")?.GetComponent<RawImage>();

    private static GameObject RaycastAt(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        if (hits.Count == 0)
            throw new Exception($"No UI raycast hit at {point} for {button.name} on a {Screen.width}x{Screen.height} viewport");
        return hits[0].gameObject;
    }

    private static Vector3[] ParticleVertices(Graphic layer)
    {
        var renderer = layer.GetComponent<CanvasRenderer>();
        if (renderer == null) throw new Exception("Ambient particle CanvasRenderer is missing");
        var mesh = renderer.GetMesh();
        if (mesh == null) throw new Exception("Ambient particle mesh is missing");
        return mesh.vertices;
    }

    private static void Click(string name)
    {
        Button button = Button(name);
        if (button == null) throw new Exception("Missing button: " + name);
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
            ExecuteEvents.pointerClickHandler);
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.timeSinceStartup < next) return;
        if (!SessionState.GetBool(Started, false))
        {
            if (!File.Exists(Trigger) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fighter403/MainMenu/MenuPixelFont.asset") == null) return;
            if (SceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity") return;
            File.WriteAllText(Report, "");
            step = captured = 0;
            SessionState.SetBool(Started, true);
            next = EditorApplication.timeSinceStartup + 2.5;
            menuDeadline = next + 12.0;
            EditorApplication.isPlaying = true;
            return;
        }
        if (!EditorApplication.isPlaying)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) SessionState.EraseBool(Started);
            return;
        }
        if (File.Exists(Trigger)) File.Delete(Trigger);
        EditorApplication.QueuePlayerLoopUpdate();

        try
        {
            if (step == 0)
            {
                var menu = GameObject.Find("Fighter403 Main Menu");
                var button = Button("Start Game");
                if (menuDeadline == 0) menuDeadline = EditorApplication.timeSinceStartup + 12;
                bool ready = menu != null && button != null && Time.timeScale == 0 &&
                    EventSystem.current?.currentSelectedGameObject == button.gameObject;
                if (!ready && EditorApplication.timeSinceStartup < menuDeadline) return;
                Check(menu != null && button != null, "Startup menu and start button");
                Check(ready, "Gameplay is paused and Start is selected");
                var entryAtlas = Resources.Load<Texture2D>("Fighter403/MainMenu/DungeonEntry");
                Check(Resources.Load<AudioClip>("Fighter403/Sfx/MenuStart") != null && entryAtlas != null &&
                    entryAtlas.width == 1920 && entryAtlas.height == 1440 && entryAtlas.filterMode == FilterMode.Point,
                    "Menu audio and point-sampled transition atlas imported");
                var titleArt = GameObject.Find("Fighter403").GetComponent<RawImage>();
                var pixelFont = Resources.Load<TMP_FontAsset>("Fighter403/MainMenu/MenuPixelFont");
                Check(titleArt.texture.filterMode == FilterMode.Point && titleArt.texture.height == 18 &&
                    pixelFont.atlasTexture.filterMode == FilterMode.Point && button.GetComponentInChildren<TextMeshProUGUI>().font == pixelFont &&
                    pixelFont.HasCharacters("\u5F00\u59CB\u6E38\u620F"), "Point-sampled title and pixel glyphs");
                Check(button.transition == Selectable.Transition.SpriteSwap &&
                    button.spriteState.highlightedSprite != button.spriteState.pressedSprite, "Distinct button states");
                var hit = RaycastAt(button);
                Check(hit == button.gameObject, $"Styled button receives pointer raycasts (hit {hit.name})");
                var particles = GameObject.Find("Firewall Particles").GetComponent<RawImage>();
                Check(particles.texture != null && particles.texture.width == 1728, "Particle atlas imported");
                firstParticleFrame = particles.uvRect;
                var ambient = Ambient();
                Check(ambient != null && ambient.mainTexture.width == 64 && ambient.mainTexture.height == 16 &&
                    ambient.mainTexture.filterMode == FilterMode.Point, "Point-sampled ambient particle tiles imported");
                Canvas.ForceUpdateCanvases();
                var bounds = ambient.rectTransform.rect;
                var backdrop = (RectTransform)ambient.transform.parent;
                var gate = backdrop.Find("Firewall Gate");
                var pages = backdrop.Find("Pages");
                Check(Vector2.Distance(bounds.size, backdrop.rect.size) < .01f &&
                    ambient.rectTransform.anchorMin == Vector2.zero && ambient.rectTransform.anchorMax == Vector2.one &&
                    gate.GetSiblingIndex() < ambient.transform.GetSiblingIndex() &&
                    ambient.transform.GetSiblingIndex() < pages.GetSiblingIndex() && !ambient.raycastTarget,
                    "Ambient layer covers the screen behind interactive menu content");
                firstAmbientVertices = ParticleVertices(ambient);
                var quadrants = new int[4];
                for (int i = 0; i + 3 < firstAmbientVertices.Length; i += 4)
                {
                    Vector2 center = (firstAmbientVertices[i] + firstAmbientVertices[i + 2]) * .5f;
                    if (bounds.Contains(center))
                        quadrants[(center.x >= bounds.center.x ? 1 : 0) + (center.y >= bounds.center.y ? 2 : 0)]++;
                }
                Check(quadrants.All(count => count >= 10), "Visible particle geometry reaches all four quadrants");
                Directory.CreateDirectory(CapturePath);
                step++;
                next = EditorApplication.timeSinceStartup + .07;
                return;
            }
            if (step == 1)
            {
                if (captured < CaptureFrames)
                {
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(CapturePath, $"frame-{captured:D3}.png")));
                    captured++;
                    next = EditorApplication.timeSinceStartup + 1.0 / 16.0;
                    return;
                }
                var particles = GameObject.Find("Firewall Particles").GetComponent<RawImage>();
                Check(particles.uvRect != firstParticleFrame, "Particle animation continues at time scale zero");
                Check(Time.timeScale == 0 && !ParticleVertices(Ambient()).SequenceEqual(firstAmbientVertices),
                    "Fullscreen particle mesh moves at time scale zero");
                var start = Button("Start Game");
                ExecuteEvents.Execute(start.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down },
                    ExecuteEvents.moveHandler);
                Check(EventSystem.current.currentSelectedGameObject == Button("Open Settings").gameObject,
                    "Keyboard navigation selects settings");
                Click("Open Credits");
                Check(Ambient().color.a < .5f, "Particles dim on the credits page");
                var producer = GameObject.Find("Producer")?.GetComponent<TextMeshProUGUI>();
                Check(producer != null && producer.text == "\u9B4F\u65FB\u987E\uFF08\u5B63\u95F2\uFF09", "Confirmed producer credit");
                Check(producer.font.HasCharacters(producer.text), "Pixel font includes the full producer name");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Temp/Fighter403AmbientCredits.png"));
                step++;
                next = EditorApplication.timeSinceStartup + .32;
                return;
            }
            if (step == 2)
            {
                Click("Return");
                Check(EventSystem.current.currentSelectedGameObject == Button("Open Credits").gameObject, "Return restores menu selection");
                Click("Open Settings");
                var slider = GameObject.Find("Effects Slider")?.GetComponent<Slider>();
                Check(slider != null, "Audio settings page");
                Check(Ambient().color.a < .5f, "Particles dim on the settings page");
                Canvas.ForceUpdateCanvases();
                foreach (string name in new[] { "Master Slider", "Music Slider", "Effects Slider" })
                {
                    var row = GameObject.Find(name).GetComponent<Slider>();
                    var transform = (RectTransform)row.transform;
                    Check(Mathf.Abs(row.handleRect.rect.height - 23f) < .01f &&
                        Mathf.Abs(row.handleRect.TransformPoint(row.handleRect.rect.center).y -
                        transform.TransformPoint(transform.rect.center).y) < .01f, name + " handle is 23 units and centered");
                }
                hadEffects = PlayerPrefs.HasKey(Prefix + "Effects");
                oldEffects = PlayerPrefs.GetFloat(Prefix + "Effects");
                changedEffects = true;
                slider.value = 0;
                Check(AudioManager.Instance.AudioSources[EAudioType.Sfx].volume == 0 &&
                    PlayerPrefs.GetFloat(Prefix + "Effects", -1) == 0, "Effects volume applies to audio and saves");
                slider.value = hadEffects ? oldEffects * 100 : 75;
                if (!hadEffects) PlayerPrefs.DeleteKey(Prefix + "Effects");
                changedEffects = false;
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Temp/Fighter403AmbientSettings.png"));
                step++;
                next = EditorApplication.timeSinceStartup + .32;
                return;
            }
            if (step == 3)
            {
                Click("Return");
                Check(Mathf.Approximately(Ambient().color.a, 1f), "Returning home restores ambient particle brightness");
                ObserveEntryAttempts();
                Click("Start Game");
                Check(Shutter() != null && Time.timeScale == 0, "Transition starts while gameplay is paused");
                var hit = RaycastAt(Button("Open Settings"));
                Check(hit.name == "Dungeon Entry Transition", $"Transition blocks menu clicks (hit {hit.name})");
                Click("Start Game");
                entryDeadline = EditorApplication.timeSinceStartup + 12;
                step++;
                return;
            }
            if (step == 4)
            {
                if (Shutter() != null && EditorApplication.timeSinceStartup < entryDeadline) return;
                Check(entryLoadAttempts == 1 && coveredAtLoad, "Repeated start clicks trigger one load under the opaque shutter");
                Check(Shutter() == null && Time.timeScale == 0 &&
                    GameObject.Find("Start Error")?.GetComponent<TMP_Text>().text == SimulatedFailure &&
                    EventSystem.current.currentSelectedGameObject == Button("Start Game").gameObject,
                    "A failed load restores the menu and reselects Start");
                Directory.CreateDirectory(EntryCapturePath);
                captured = 0;
                finishCaptureAt = 0;
                roomReadyUnderCover = false;
                Click("Start Game");
                entryDeadline = EditorApplication.timeSinceStartup + 12;
                step++;
                return;
            }

            var type = Type.GetType("Fighter403.UI.CardHandController, Assembly-CSharp");
            var hud = UnityEngine.Object.FindAnyObjectByType(type);
            var field = type.GetField("dungeonRoom", BindingFlags.NonPublic | BindingFlags.Instance);
            bool shutterActive = Shutter() != null;
            if (shutterActive && hud != null && field.GetValue(hud) != null)
                roomReadyUnderCover |= Time.timeScale == 0;
            double now = EditorApplication.timeSinceStartup;
            if (!shutterActive && finishCaptureAt == 0) finishCaptureAt = now + .50;
            if (now < entryDeadline && (shutterActive || now < finishCaptureAt))
            {
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(EntryCapturePath, $"frame-{captured:D3}.png")));
                captured++;
                next = now + 1.0 / 24.0;
                return;
            }
            Check(entryLoadAttempts == 2 && coveredAtLoad && roomReadyUnderCover,
                "First room is built under the shutter while gameplay is paused");
            var floorField = type.GetField("dungeonFloor", BindingFlags.NonPublic | BindingFlags.Instance);
            Check(hud != null && field.GetValue(hud) != null && (int)floorField.GetValue(hud) == 0 &&
                GameObject.Find("Fighter403 Main Menu") == null && Time.timeScale > 0, "Random dungeon first room loaded and gameplay resumed");
            File.AppendAllText(Report, "PASS" + Environment.NewLine);
        }
        catch (Exception exception)
        {
            File.AppendAllText(Report, "FAIL " + exception + Environment.NewLine);
        }

        if (changedEffects)
        {
            if (hadEffects) PlayerPrefs.SetFloat(Prefix + "Effects", oldEffects);
            else PlayerPrefs.DeleteKey(Prefix + "Effects");
            PlayerPrefs.Save();
        }
        SessionState.EraseBool(Started);
        EditorApplication.isPlaying = false;
    }

    private static void ObserveEntryAttempts()
    {
        var menu = GameObject.Find("Fighter403 Main Menu").GetComponents<MonoBehaviour>()
            .Single(component => component.GetType().Name == "Fighter403MainMenu");
        var begin = menu.GetType().GetField("beginRun", BindingFlags.NonPublic | BindingFlags.Instance);
        var load = (Func<string>)begin.GetValue(menu);
        entryLoadAttempts = 0;
        coveredAtLoad = true;
        begin.SetValue(menu, (Func<string>)(() =>
        {
            var shutter = Shutter();
            // Atlas frame 25 is fully opaque. The Start button runs on unscaled time.
            coveredAtLoad &= shutter != null && Mathf.Approximately(shutter.uvRect.x, 1f / 6) &&
                Mathf.Approximately(shutter.uvRect.y, 3f / 8) && Time.timeScale == 0;
            return ++entryLoadAttempts == 1 ? SimulatedFailure : load();
        }));
    }
}

