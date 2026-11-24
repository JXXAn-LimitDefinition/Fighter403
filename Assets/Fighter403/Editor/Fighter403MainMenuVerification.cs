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
    private const int CaptureFrames = 64;
    private const string Prefix = "Fighter403.MainMenu.";
    private const string Started = "Fighter403AmbientVerifyStarted";
    private static double next;
    private static double menuDeadline;
    private static int step;
    private static int captured;
    private static float oldEffects;
    private static Rect firstParticleFrame;
    private static Vector3[] firstAmbientVertices;
    private static bool hadEffects;
    private static bool changedEffects;

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
            File.Delete(Trigger);
            File.WriteAllText(Report, "");
            step = captured = 0;
            SessionState.SetBool(Started, true);
            next = EditorApplication.timeSinceStartup + 2.5;
            menuDeadline = next + 12.0;
            EditorApplication.isPlaying = true;
            return;
        }
        if (!EditorApplication.isPlaying) return;

        try
        {
            if (step == 0)
            {
                var menu = GameObject.Find("Fighter403 Main Menu");
                if (menu == null && EditorApplication.timeSinceStartup < menuDeadline) return;
                Check(menu != null && Button("Start Game") != null, "Startup menu and start button");
                Check(Time.timeScale == 0 && EventSystem.current.currentSelectedGameObject == Button("Start Game").gameObject,
                    "Gameplay is paused and Start is selected");
                Check(Resources.Load<AudioClip>("Fighter403/Sfx/MenuStart") != null &&
                    Resources.Load<Texture2D>("Fighter403/MainMenu/Iris").width == 2240, "Menu audio and unscaled transition atlas imported");
                var titleArt = GameObject.Find("Fighter403").GetComponent<RawImage>();
                var pixelFont = Resources.Load<TMP_FontAsset>("Fighter403/MainMenu/MenuPixelFont");
                var button = Button("Start Game");
                Check(titleArt.texture.filterMode == FilterMode.Point && titleArt.texture.height == 18 &&
                    pixelFont.atlasTexture.filterMode == FilterMode.Point && button.GetComponentInChildren<TextMeshProUGUI>().font == pixelFont &&
                    pixelFont.HasCharacters("\u5F00\u59CB\u6E38\u620F"), "Point-sampled title and pixel glyphs");
                Check(button.transition == Selectable.Transition.SpriteSwap &&
                    button.spriteState.highlightedSprite != button.spriteState.pressedSprite, "Distinct button states");
                var raycastAt = RectTransformUtility.WorldToScreenPoint(null, button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
                var hit = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = raycastAt }, hit);
                Check(hit.Count > 0 && hit[0].gameObject == button.gameObject, "Styled button receives pointer raycasts");
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
                Click("Start Game");
                Check(GameObject.Find("PixelFX Iris") != null && Time.timeScale == 0, "Transition starts while player input is paused");
                step++;
                next = EditorApplication.timeSinceStartup + 1.65;
                return;
            }

            var type = Type.GetType("Fighter403.UI.CardHandController, Assembly-CSharp");
            var hud = UnityEngine.Object.FindAnyObjectByType(type);
            var field = type.GetField("dungeonRoom", BindingFlags.NonPublic | BindingFlags.Instance);
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
}

