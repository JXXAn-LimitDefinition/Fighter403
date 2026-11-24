using System.Collections;
using System.Collections.Generic;
using Fighter403.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Fighter403.Audio;
using Framework.FrameworkAudio;

namespace Fighter403.UI
{
    [DisallowMultipleComponent, DefaultExecutionOrder(120)]
    public sealed partial class CardHandController : MonoBehaviour
    {
        private static readonly Color Ink = new(0.075f, 0.09f, 0.12f, 0.97f), Paper = new(0.96f, 0.92f, 0.83f);
        private const float ComboSlotWidth = 77f, PreviewScale = 0.40f;
        private const float HandHiddenOffset = -430f, HandRevealDuration = 0.22f;
        private const string MenuMusicPath = "Fighter403/Music/MainMenu";
        private const string CombatMusicPath = "Fighter403/Music/Combat";
        [SerializeField] private RectTransform hand, drawPile, discardPile;
        private Fighter403MainMenu mainMenu;
        private AudioManager musicAudio;
        [SerializeField] private CardDefinition[] cardPrefabs;
        [SerializeField] private TestDummy dummyPrefab;
        [SerializeField] private ProgramEnemy meleePrefab;
        [SerializeField] private ProgramEnemy[] additionalEnemyPrefabs;
        [SerializeField] private ProgramEnemy[] eliteEnemyPrefabs;
        [SerializeField] private FirewallBoss firewallBossPrefab;
        [SerializeField] private LayerMask dashObstacleMask = ~0;

        [Header("击杀慢动作")]
        [SerializeField, Min(0f), Tooltip("使用现实时间计时，单位为秒。设为 0 可关闭。")]
        private float killSlowMotionSeconds = 0.1f;
        [SerializeField, Range(0f, 1f), Tooltip("击杀时的战斗时间倍率。0.1 表示十分之一速度。")]
        private float killSlowMotionScale = 0.1f;

        [Header("卡牌就绪闪光")]
        [SerializeField, ColorUsage(false)] private Color readyFlashColor = Color.white;
        [SerializeField, Range(0f, 1f), Tooltip("整张卡面的闪光峰值不透明度。")]
        private float readyFlashIntensity = 0.68f;
        [SerializeField, Min(0f), Tooltip("闪光保持峰值的时间，单位为秒。")]
        private float readyFlashHold = 0.06f;
        [SerializeField, Min(0.01f), Tooltip("闪光从峰值淡出到透明的时间，单位为秒。")]
        private float readyFlashFade = 0.22f;

        private CardBatch batch;
        private RoundPlayerMovement movement;
        private CardCombat combat;
        private Camera cameraView;
        private TopDownCameraFollow cameraFollow;
        private Canvas canvas;
        private RectTransform root, row, pilePanel, dummyLabelRect, spentCards;
        private Font font;
        private Text pileText, drawText, discardText, actionText, dummyText, statusText;
        private Button actionButton, resetButton;
        private TestDummy createdDummy;
        private FirewallBoss firewallBoss;
        private PlayerImpactVfx playerImpact;
        private Vector2 beforeFirewallTrial;
        private readonly List<GameObject> targetsHiddenForFirewall = new();
        private readonly List<ProgramEnemy> createdEnemies = new();
        private int testEncounter;
        private readonly List<CastCardBurn> castCards = new(2);
        private HandCard queueDrag;
        private HandCard dashDrag;
        private CardAimPreview dashAimPreview;
        private DashPredictionVisual dashVisual;
        private CombatTimeStopPostProcess timeStopPostProcess;
        private ThousandSlashesScreenEffect flurryScreen;
        private readonly List<DashPlan> dashPlans = new();
        private readonly List<DashPlan> executingDashPlans = new();
        private int shownPile = -1;
        private float oldScale, oldFixed, statusUntil, hitStopUntil, hitStopScale;
        private float energyDeniedUntil;
        private CombatTarget selectedTarget;
        private Vector2 handRestPosition;
        private float handReveal;
        private bool ownsClock;
        private bool bigBangTimeStop;
        private bool flurryHitStop;
        private bool handWasShown, dualThreadWasActive;
        private GameSfx.Loop sandevistanLoop;
        private readonly RectTransform[] slots = new RectTransform[CardBatch.Size];
        private readonly Text[] slotLabels = new Text[CardBatch.Size];
        private readonly Image[] slotIcons = new Image[CardBatch.Size];
        private readonly Image[] previewIcons = new Image[CardBatch.PreviewCount];
        private readonly Text[] previewLabels = new Text[CardBatch.PreviewCount];
        private readonly RectTransform[] previewSlots = new RectTransform[CardBatch.PreviewCount];
        private readonly Dictionary<HandCard, CardHoverVisual> views = new();
        private float QueueCenterSlot => (slots.Length - 1) * .5f;

        private sealed class DashPlan
        {
            internal readonly HandCard Card;
            internal readonly Vector2 Origin;
            internal readonly Vector2 Aim;
            internal readonly Vector2 Destination;
            internal readonly Vector2 Direction;

            internal DashPlan(HandCard card, Vector2 origin, Vector2 aim, Vector2 destination)
            {
                Card = card;
                Origin = origin;
                Aim = aim;
                Destination = destination;
                Direction = (destination - origin).sqrMagnitude > 0.0001f
                    ? (destination - origin).normalized : Vector2.right;
            }

            internal float Distance => Vector2.Distance(Origin, Destination);
        }

        private void Start()
        {
            movement = FindAnyObjectByType<RoundPlayerMovement>();
            cameraView = Camera.main;
            if (movement == null || cameraView == null)
            {
                Debug.LogWarning("CardHandHUD needs an active RoundPlayer and a MainCamera.", this);
                enabled = false;
                return;
            }
            combat = movement.GetComponent<CardCombat>();
            if (combat == null) combat = movement.gameObject.AddComponent<CardCombat>();
            cameraFollow = cameraView.GetComponent<TopDownCameraFollow>();
            var kinds = new HashSet<CardKind>();
            if (cardPrefabs == null || cardPrefabs.Length == 0)
            {
                Debug.LogError("Assign one card prefab for each card kind on CardHandHUD.", this);
                enabled = false;
                return;
            }
            foreach (var prefab in cardPrefabs)
            {
                if (prefab != null && kinds.Add(prefab.Kind)) continue;
                Debug.LogError("CardHandHUD has a missing or duplicate card prefab.", this);
                enabled = false;
                return;
            }
            // Older HUD prefab overrides still work with the original five-card deck.
            foreach (var kind in new[] { CardKind.TriangleRift, CardKind.PhaseDash, CardKind.OrbitSpin,
                         CardKind.MatrixGuard, CardKind.Starfall })
            {
                if (kinds.Contains(kind)) continue;
                Debug.LogError("Assign one card prefab for each base card kind on CardHandHUD.", this);
                enabled = false;
                return;
            }
            if (EventSystem.current == null)
                new GameObject("Card EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            EventSystem.current.pixelDragThreshold = Mathf.Max(EventSystem.current.pixelDragThreshold, 8);
            root = (RectTransform)transform;
            canvas = GetComponent<Canvas>();
            timeStopPostProcess = new CombatTimeStopPostProcess(cameraView);
            flurryScreen = new ThousandSlashesScreenEffect(cameraView, cameraFollow);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 18);
            batch = new CardBatch(new System.Random(), cardPrefabs, new CombatVitals(movement.BaseStats));
            playerImpact = new PlayerImpactVfx(movement);
            ConnectCombat();
            dashAimPreview = new CardAimPreview();
            var visualObject = new GameObject("Dash prediction visuals");
            visualObject.layer = movement.gameObject.layer;
            dashVisual = visualObject.AddComponent<DashPredictionVisual>();
            dashVisual.Initialize(movement.GetComponent<SpriteRenderer>());
            GameSfx.WarmUp();
            BuildUi();
            BuildBossHud();
            handRestPosition = hand.anchoredPosition;
            handReveal = 0f;
            ApplyHandReveal();
            EnsureDummy();
            SpawnTestEnemies();
            BuildDungeonUi();
            Refresh();
            oldScale = Time.timeScale;
            oldFixed = Time.fixedDeltaTime;
            ownsClock = true;
            if (gameObject.scene.buildIndex == 0 && gameObject.scene == SceneManager.GetActiveScene())
            {
                var menuFont = cardPrefabs[0].GetComponentInChildren<TMPro.TMP_Text>(true)?.font;
                mainMenu = Fighter403MainMenu.Open(menuFont, BeginFromMainMenu, CompleteMenuEntry);
                if (mainMenu != null)
                {
                    movement.SetPlanningLock(true);
                    SetClock(0f);
                    PlayMusic(MenuMusicPath);
                }
            }
        }

        private string BeginFromMainMenu()
        {
            if (BeginDungeonRun(null, playTraverseCue: false)) return null;
            return dungeonMenuText.text;
        }

        private void PlayMusic(string resourcePath)
        {
            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"Missing background music: {resourcePath}", this);
                return;
            }

            musicAudio = AudioManager.Instance;
            musicAudio.ChangeBgm(clip, fadeTime: .45f);
        }

        private void StopMusic()
        {
            if (musicAudio != null) musicAudio.StopAudio(EAudioType.Bgm);
            musicAudio = null;
        }

        private void CompleteMenuEntry()
        {
            mainMenu = null;
            UpdatePlanningState();
            UpdateCombatClock();
        }

        private void BuildUi()
        {
            var center = new Vector2(0.5f, 0.5f);
            LoadHudPixels();
            float comboY = EnergyBarY + EnergyBarHeight * 0.5f + 12f + ComboHeight * 0.5f;
            row = Box("Combo queue", hand, center, new Vector2(0f, comboY),
                new Vector2(slots.Length * ComboSlotWidth + 18f, ComboHeight));
            PaintInterfacePlate(row, InterfaceSurface, QueueFrame);
            for (int i = 0; i < slots.Length; ++i)
            {
                int index = i;
                var slot = slots[i] = Box("Combo slot " + (i + 1), row, center,
                    new Vector2((i - QueueCenterSlot) * ComboSlotWidth, 0), new Vector2(72f, 46f));
                slotFrames[i] = PaintInterfacePlate(slot, InterfaceInset, EmptySocket);
                slotIcons[i] = Paint(Box("Card art", slot, center, new Vector2(0f, 8f), new Vector2(28f, 22f)), Color.white, false);
                slotIcons[i].preserveAspect = true;
                slotOrderLabels[i] = Label("Order", slot, center, new Vector2(-24f, 9f), new Vector2(18f, 24f), 13);
                slotOrderLabels[i].text = (i + 1).ToString();
                slotOrderLabels[i].color = InterfaceMuted;
                slotCostLabels[i] = Label("Cost", slot, center, new Vector2(24f, 9f), new Vector2(19f, 24f), 14);
                slotCostLabels[i].color = InterfaceCyan;
                slotLabels[i] = Label("Card name", slot, center, new Vector2(0f, -9f), new Vector2(64f, 23f), 16);
                slotLabels[i].color = InterfaceText;
                var events = slot.gameObject.AddComponent<EventTrigger>();
                Trigger(events, EventTriggerType.PointerClick, e => { if (Left(e) && !e.dragging && index < batch.Queue.Count) RemoveQueuedCard(batch.Queue[index]); });
                Trigger(events, EventTriggerType.BeginDrag, e => { if (Left(e) && index < batch.Queue.Count) queueDrag = batch.Queue[index]; });
                Trigger(events, EventTriggerType.Drag, _ => { });
                Trigger(events, EventTriggerType.EndDrag, e => EndQueueDrag(e));
            }
            actionButton = ButtonAt("Execute", hand, center, new Vector2(510f, EnergyBarY), new Vector2(190f, EnergyBarHeight), SubmitCombo, out actionText);
            actionText.fontStyle = FontStyle.Bold;
            actionText.resizeTextMaxSize = 18;
            StyleInterfaceButton(actionButton, InterfaceInset, QueueFrame);
            var rail = Box("Queue progress", (RectTransform)actionButton.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            rail.anchorMax = Vector2.right;
            rail.offsetMin = new Vector2(1f, 1f);
            rail.offsetMax = new Vector2(-1f, 4f);
            executeRail = Paint(rail, EmptySocket, false);
            BuildMeters();
            statusPlate = Box("Combat feedback", row, new Vector2(.5f, 1f), new Vector2(0f, 22f), new Vector2(480f, 29f));
            PaintInterfacePlate(statusPlate, InterfaceSurface, QueueFrame);
            statusFade = statusPlate.gameObject.AddComponent<CanvasGroup>();
            statusText = Label("Status", statusPlate, center, Vector2.zero, new Vector2(460f, 26f), 17);
            statusText.color = InterfaceText;
            statusPlate.gameObject.SetActive(false);

            drawPile.SetParent(hand, false);
            discardPile.SetParent(hand, false);
            drawPile.anchoredPosition = new Vector2(-573f, 58f);
            discardPile.anchoredPosition = new Vector2(526f, 78f);
            drawText = WirePile(drawPile, false);
            discardText = WirePile(discardPile, true);
            BuildPreview();
            BuildBuffDisplay();
            spentCards = Box("Consumed cards", hand, center, Vector2.zero, Vector2.zero);
            pilePanel = Box("Pile contents", hand, center, new Vector2(-573f, 309f), new Vector2(194, 168));
            PaintInterfacePlate(pilePanel, InterfaceSurface, QueueFrame, true);
            pileFade = pilePanel.gameObject.AddComponent<CanvasGroup>();
            pileText = Label("Contents", pilePanel, center, new Vector2(-6f, 0f), new Vector2(160, 152), 17);
            pileText.color = InterfaceText;
            var closePile = ButtonAt("Close", pilePanel, Vector2.one, new Vector2(-17, -17), new Vector2(25, 25), () => SetPile(-1), out var close);
            StyleInterfaceButton(closePile, InterfaceInset, QueueFrame);
            close.text = "\u00d7";
            pilePanel.gameObject.SetActive(false);
            var dummyPanel = Box("Dummy controls", root, new Vector2(1, 1), new Vector2(-121, -30), new Vector2(210, 36));
            resetButton = ButtonAt("Reset dummy", dummyPanel, new Vector2(1, 0.5f), new Vector2(-52, 0), new Vector2(104, 34), ResetDummies, out var reset);
            StyleInterfaceButton(resetButton, InterfaceSurface, QueueFrame);
            reset.text = "\u91cd\u7f6e\u654c\u4eba";
            dummyLabelRect = Box("Dummy world readout", root, Vector2.zero, Vector2.zero, new Vector2(190, 53));
            dummyText = Label("HP and last hit", dummyLabelRect, new Vector2(0.5f, 0.5f), Vector2.zero, dummyLabelRect.sizeDelta, 16);
            OutlineText(dummyText);
        }

        private void BuildPreview()
        {
            var center = new Vector2(0.5f, 0.5f);
            const float previewX = 580f;
            const float slotPitch = 65f;
            var heading = Label("Upcoming cards", hand, center, new Vector2(previewX, 195f), new Vector2(198f, 25f), 17);
            heading.text = "下三张";
            heading.color = InterfaceCyan;
            heading.fontStyle = FontStyle.Bold;
            for (int i = 0; i < previewSlots.Length; ++i)
            {
                float x = previewX + (i - (previewSlots.Length - 1) * 0.5f) * slotPitch;
                var slot = previewSlots[i] = Box("Upcoming card " + (i + 1), hand, center, new Vector2(x, 144f), new Vector2(61f, 71f));
                PaintInterfacePlate(slot, InterfaceSurface, i == 0 ? OccupiedSocket : QueueFrame);
                previewIcons[i] = Paint(Box("Card art", slot, center, new Vector2(0f, 8f), new Vector2(49f, 39f)), Color.white, false);
                previewIcons[i].preserveAspect = true;
                previewLabels[i] = Label("Name and cost", slot, center, new Vector2(0f, -20f), new Vector2(55f, 24f), 16);
                previewLabels[i].color = InterfaceText;
            }
        }

        private Text WirePile(RectTransform pile, bool discarded)
        {
            foreach (Transform child in pile) child.gameObject.SetActive(false);
            pile.sizeDelta = new Vector2(90f, 48f);
            var graphicHit = Paint(pile, QueueFrame, true);
            var button = pile.gameObject.AddComponent<Button>();
            button.targetGraphic = graphicHit;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            int id = discarded ? 1 : 0;
            button.onClick.AddListener(() => SetPile(shownPile == id ? -1 : id));
            var label = Label("Count", pile, new Vector2(.5f, .5f), Vector2.zero, new Vector2(80, 35), 17);
            StyleInterfaceButton(button, InterfaceSurface, QueueFrame);
            return label;
        }

        private void SetPile(int id)
        {
            if (shownPile != id) GameSfx.Play(id >= 0 ? Sfx.PileOpen : Sfx.PileClose);
            shownPile = id;
            pilePanel.gameObject.SetActive(id >= 0);
            if (id < 0) return;
            var source = id == 0 ? drawPile : discardPile;
            pilePanel.anchoredPosition = new Vector2(source.anchoredPosition.x, 325f);
            var lines = new List<string> { id == 0 ? "\u62bd\u724c\u5806" : "\u5f03\u724c\u5806" };
            foreach (var definition in cardPrefabs)
            {
                int n = batch.CountInPile(id == 1, definition.Kind);
                if (n > 0) lines.Add(definition.DisplayName + "  \u00d7  " + n);
            }
            if (lines.Count == 1) lines.Add("\u7a7a");
            pileText.text = string.Join("\n", lines);
        }

        private void Update()
        {
            if (mainMenu != null || batch == null || DungeonRoomTransitioning) return;
            StepRootHandLocks();
            flurryScreen?.StepColorRestore();
            batch.Vitals.StepBlock(Time.deltaTime);
            float beforeRecovery = batch.Energy;
            batch.StepEnergy(Time.unscaledDeltaTime);
            if (batch.Energy > beforeRecovery && handWasShown)
            {
                foreach (var card in batch.Hand)
                {
                    float required = batch.ReservedEnergy + batch.PlannedEnergyCost(card);
                    if (beforeRecovery + 0.0001f >= required ||
                        batch.GetSelectionState(card) != CardSelectionState.Ready) continue;
                    GameSfx.Play(Sfx.EnergyReady, minimumInterval: 0.25f);
                    break;
                }
            }

            if (batch.Vitals.SandevistanActive && sandevistanLoop == null)
                sandevistanLoop = GameSfx.StartLoop(Sfx.SandevistanLoop, gameObject);
            else if (!batch.Vitals.SandevistanActive && sandevistanLoop != null)
            {
                GameSfx.StopLoop(ref sandevistanLoop);
                GameSfx.Play(Sfx.SandevistanEnd);
            }

            bool dualThreadActive = batch.Vitals.DualThreadRemaining > 0d;
            if (dualThreadWasActive && !dualThreadActive) GameSfx.Play(Sfx.DualEnd);
            dualThreadWasActive = dualThreadActive;
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (UpdateDungeon(keys)) return;
            UpdateTargetSelection();
            if (mouse?.leftButton.wasPressedThisFrame == true) TrySelectTargetAtMouse();
            if (mouse?.middleButton.wasPressedThisFrame == true) TryCycleTarget();
            if (keys?.escapeKey.wasPressedThisFrame == true)
            {
                if (dashPlans.Count > 0) ClearDashPredictions();
                else if (dashDrag != null) CancelCardDrag();
                else if (queueDrag != null) queueDrag = null;
                else
                {
                    SetPile(-1);
                    if (batch.Queue.Count > 0) GameSfx.Play(Sfx.QueueClear);
                    batch.Clear();
                    Refresh();
                }
            }
            if (keys?.ctrlKey.isPressed == true && keys.zKey.wasPressedThisFrame == true)
            {
                if (dashPlans.Count > 0)
                {
                    CancelCardDrag();
                    queueDrag = null;
                    UndoLastDashPrediction();
                }
                else if (dashDrag != null) CancelCardDrag();
                else if (queueDrag != null) queueDrag = null;
                else if (batch.Queue.Count > 0) RemoveQueuedCard(batch.Queue[batch.Queue.Count - 1]);
            }
            bool toggleTarget = keys?.qKey.wasPressedThisFrame == true;
            if (mouse?.rightButton.wasPressedThisFrame == true)
            {
                if (dashDrag != null) CancelCardDrag();
                else if (queueDrag != null) queueDrag = null;
                else toggleTarget = true;
            }
            if (toggleTarget)
            {
                if (selectedTarget != null) SetSelectedTarget(null);
                else if (!batch.Busy) SetSelectedTarget(FindNearestAliveTarget());
            }
            else if (keys?.spaceKey.wasPressedThisFrame == true && dashDrag == null && !batch.Busy)
            {
                var target = selectedTarget == null ? FindNearestAliveTarget() : null;
                if (target != null) SetSelectedTarget(target);
                else SubmitCombo();
            }
            if (dungeonFloors == null && !batch.Busy)
            {
                if (keys?.f8Key.wasPressedThisFrame == true) ResetDummies();
                if (keys?.f9Key.wasPressedThisFrame == true) NextTestEncounter();
                if (keys?.f10Key.wasPressedThisFrame == true) OpenFirewallTrial();
            }
            if (statusUntil > 0 && Time.unscaledTime >= statusUntil) { statusText.text = ""; statusUntil = 0; }
        }

        private void LateUpdate()
        {
            if (batch == null || movement == null || root == null || energyGauge == null) return;
            playerImpact.Tick();
            UpdateCombatClock();
            timeStopPostProcess.SetSandevistan(movement.transform, combat.SandevistanVisuals,
                batch.Vitals.SandevistanElapsed, batch.Vitals.SandevistanRemaining);
            UpdateMeters();
            LayoutDungeonUi();
            UpdateBossHud();
            UpdateDungeonInteractionMotion();
            UpdateHandReveal();
            UpdateSelectionStates();
            UpdateCombatHudMotion();
            if (batch.Busy && dashPlans.Count > 0 && dashPlans[0].Origin != movement.Position)
                RebuildDashPath(0);
            UpdateQueuedAim();
            // Keep the whole hand and the draw preview on screen on narrow Game Views.
            float scale = Mathf.Min(1.35f, Mathf.Min(root.rect.width / 1580f, root.rect.height / 1010f));
            hand.localScale = Vector3.one * scale;
            buffDisplay.localScale = hand.localScale;
            buffDisplay.anchoredPosition = handRestPosition + BuffDisplayOffset * scale;
            UpdateBuffDisplay();
            var dummy = createdDummy;
            if (dummy == null)
                foreach (var enemy in CombatTarget.Active)
                    if (enemy is TestDummy testDummy) { dummy = testDummy; break; }
            ShowDummyReadout(dummy);
        }

        private void UpdateQueuedAim()
        {
            if (dashDrag != null || dashAimPreview == null) return;
            if (batch.Queue.Count == 0 || dashPlans.Count > 0)
            {
                dashAimPreview.Hide();
                return;
            }

            var card = batch.Queue[0].Definition;
            Vector3 origin = movement.transform.position;
            if (selectedTarget == null)
            {
                if (card.Kind is CardKind.LimitedRedBlackHole or CardKind.InfiniteBlueWhiteHole)
                    dashAimPreview.Show(origin, Vector2.right, card, null);
                else dashAimPreview.Hide();
                return;
            }
            Vector2 target = selectedTarget.transform.position;
            if (card.Kind == CardKind.PhaseDash)
            {
                Vector2 start = movement.Position;
                Vector2 dashDirection = combat.AimDirection(selectedTarget, start);
                Vector2 end = movement.GetDashDestination(start, dashDirection, card.Distance, dashObstacleMask);
                dashAimPreview.Show(new Vector3(start.x, start.y, origin.z), dashDirection, card, end, end);
                return;
            }
            if (card.IsTargeted && !combat.CanTarget(selectedTarget, card))
                dashAimPreview.Hide();
            else
                dashAimPreview.Show(origin, combat.AimDirection(selectedTarget, origin), card, target);
        }

        private void ShowDummyReadout(TestDummy dummy)
        {
            if (firewallBoss != null) dummy = null;
            dummyLabelRect.gameObject.SetActive(dummy != null && cameraView != null);
            if (dummy == null || cameraView == null) return;
            Vector3 screen = cameraView.WorldToScreenPoint(dummy.transform.position + Vector3.up * 0.72f);
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out var local))
                dummyLabelRect.anchoredPosition = local + Vector2.Scale(root.rect.size, root.pivot);
            var text = dummy.DisplayName + "  " + dummy.Health + " / " + dummy.MaxHealth + "\n"
                + (dummy.LastDamage > 0 ? "  \u53d7\u51fb  \u2212" + dummy.LastDamage : "");
            if (dummyText.text != text) dummyText.text = text;
        }

        private void TrySelectTargetAtMouse()
        {
            if (batch.Busy || cameraView == null || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = cameraView.ScreenToWorldPoint(new Vector3(screen.x, screen.y,
                -cameraView.transform.position.z));
            CombatTarget hit = null;
            float nearest = float.PositiveInfinity;
            foreach (var target in CombatTarget.Active)
            {
                if (!target.Alive || !target.Contains(world)) continue;
                float distance = ((Vector2)target.transform.position - (Vector2)world).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                hit = target;
            }

            if (hit != null) SetSelectedTarget(hit == selectedTarget ? null : hit);
        }

        private void UpdateTargetSelection()
        {
            if (ReferenceEquals(selectedTarget, null)) return;
            if (selectedTarget == null || !selectedTarget.Alive ||
                (cameraFollow?.IsLockFramingActive != true && !IsTargetOnScreen(selectedTarget)))
                SetSelectedTarget(null);
        }

        private bool IsTargetOnScreen(CombatTarget target)
        {
            if (cameraView == null) return true;
            Vector3 viewport = cameraView.WorldToViewportPoint(target.transform.position);
            return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0f && viewport.y <= 1f;
        }

        private CombatTarget FindNearestAliveTarget() => combat != null ? combat.Nearest(IsTargetOnScreen) : null;

        private CombatTarget FindNextAliveTarget(CombatTarget current)
        {
            int count = CombatTarget.Active.Count;
            if (count == 0) return null;

            int currentIndex = current != null ? CombatTarget.Active.IndexOf(current) : -1;
            for (int offset = 1; offset <= count; ++offset)
            {
                int index = (currentIndex + offset + count) % count;
                var target = CombatTarget.Active[index];
                if (target != null && target.Alive && IsTargetOnScreen(target)) return target;
            }

            return null;
        }

        private void TryCycleTarget()
        {
            if (batch.Busy || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            SetSelectedTarget(selectedTarget == null
                ? FindNearestAliveTarget()
                : FindNextAliveTarget(selectedTarget));
        }

        private void SetSelectedTarget(CombatTarget target)
        {
            if (target == null || !target.Alive) target = null;
            if (ReferenceEquals(selectedTarget, target)) return;
            GameSfx.Play(target == null ? Sfx.TargetClear
                : selectedTarget == null ? Sfx.TargetLock : Sfx.TargetSwitch);
            if (selectedTarget != null) selectedTarget.GetComponent<TargetLockVisual>()?.SetLocked(false);

            selectedTarget = target;
            if (cameraFollow != null) cameraFollow.SetLockTarget(movement != null ? movement.transform : null, selectedTarget);
            if (selectedTarget != null)
            {
                var lockVisual = selectedTarget.GetComponent<TargetLockVisual>();
                if (lockVisual == null) lockVisual = selectedTarget.gameObject.AddComponent<TargetLockVisual>();
                lockVisual.SetLocked(true);
            }
        }

        private void UpdateHandReveal()
        {
            bool showHand = selectedTarget != null || batch.Busy || batch.Queue.Count > 0 || dashDrag != null
                || dungeonRoom != null && !dungeonFinished && (dungeonFreeHand || NearBreakableProp());
            if (showHand && !handWasShown) GameSfx.Play(Sfx.HandOpen);
            handWasShown = showHand;
            float target = showHand ? 1f : 0f;
            handReveal = Mathf.MoveTowards(handReveal, target,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, HandRevealDuration));
            ApplyHandReveal();
        }

        private void ApplyHandReveal()
        {
            if (hand == null) return;
            float eased = Mathf.SmoothStep(0f, 1f, handReveal);
            hand.anchoredPosition = handRestPosition + Vector2.up * Mathf.Lerp(HandHiddenOffset, 0f, eased);
        }

        private void UpdateCombatClock() =>
            SetClock(mainMenu != null || DungeonRoomTransitioning || FreezeForDashPlan || DungeonMenuOpen && !batch.Busy ? 0f
                : CombatRules.TimeScale(batch.Busy, selectedTarget != null, movement.MoveAmount > 0.01f));

        private void SetClock(float scale)
        {
            if (!ownsClock) return;
            bool sandevistanActive = batch.Vitals.SandevistanActive;
            if (bigBangTimeStop)
                scale = 0f;
            else if (scale > 0f && batch.Vitals.KillSlowMotionActive)
                scale = killSlowMotionScale;
            else if (scale > 0f && Time.unscaledTime < hitStopUntil && (flurryHitStop || !sandevistanActive))
                scale = hitStopScale;
            else if (sandevistanActive && !DungeonRoomTransitioning) scale = 1f;
            Time.timeScale = scale;
            Time.fixedDeltaTime = scale > 0f ? oldFixed * scale : oldFixed;
        }

        private void PlayKillSlowMotion()
        {
            if (!ownsClock || killSlowMotionSeconds <= 0f) return;
            batch.Vitals.KillSlowMotionUntil = Time.unscaledTimeAsDouble + killSlowMotionSeconds;
            UpdateCombatClock();
        }

        private void PlayHitFeedback(CardDefinition card)
        {
            if (cameraFollow != null && cameraFollow.isActiveAndEnabled)
                cameraFollow.Shake(card.ShakeAmplitude, card.ShakeSeconds);
            if (card.HitStopSeconds <= 0f || batch.Vitals.SandevistanActive) return;
            hitStopUntil = Time.unscaledTime + card.HitStopSeconds;
            hitStopScale = card.HitStopScale;
            flurryHitStop = false;
            SetClock(1f);
        }

        private void PlayFlurryHitFeedback(CardDefinition card, bool finisher, float progress)
        {
            float shake = finisher ? 3.7f : Mathf.Lerp(0.85f, 1.75f, progress);
            if (cameraFollow != null && cameraFollow.isActiveAndEnabled)
                cameraFollow.Shake(card.ShakeAmplitude * shake, finisher ? 0.30f : card.ShakeSeconds);
            float stopSeconds = finisher ? card.HitStopSeconds : Mathf.Lerp(0.022f, 0.064f, progress);
            hitStopUntil = Time.unscaledTime + stopSeconds;
            hitStopScale = card.HitStopScale;
            flurryHitStop = true;
            SetClock(1f);
        }

        internal void ToggleSelection(HandCard card)
        {
            if (dungeonFloors != null && dungeonFinished) return;
            if (RemoveQueuedCard(card)) return;
            if (batch.Select(card))
            {
                GameSfx.Play(Sfx.CardSelect);
                Refresh();
                PlayHudBurst(queueBurst, slots[batch.Queue.Count - 1].anchoredPosition + new Vector2(23f, 17f), InterfaceCyan);
            }
            else if (batch.GetSelectionState(card) == CardSelectionState.InsufficientEnergy) ShowEnergyDenied(card);
        }

        private void ShowEnergyDenied(HandCard card)
        {
            GameSfx.Play(Sfx.EnergyDenied);
            energyDeniedUntil = Time.unscaledTime + CardHoverVisual.RejectionDuration;
            if (views.TryGetValue(card, out var view)) view.ShowInsufficientEnergy(energyDeniedUntil);
            Status("能量不足");
        }

        private bool RemoveQueuedCard(HandCard card, bool playSound = true)
        {
            if (!batch.Remove(card)) return false;
            if (playSound) GameSfx.Play(Sfx.CardCancel);
            int index = dashPlans.FindIndex(plan => plan.Card == card);
            if (index >= 0)
            {
                dashPlans.RemoveAt(index);
                RebuildDashPath(index);
            }
            Refresh();
            return true;
        }

        private void EndQueueDrag(PointerEventData e)
        {
            if (!Left(e) || queueDrag == null) return;
            var moved = queueDrag;
            queueDrag = null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(row, e.position, e.pressEventCamera, out var local)
                || !row.rect.Contains(local)) return;
            int destination = Mathf.RoundToInt(local.x / ComboSlotWidth + QueueCenterSlot);
            int planIndex = dashPlans.FindIndex(plan => plan.Card == moved);
            if (planIndex >= 0)
            {
                destination = Mathf.Clamp(destination, 0, dashPlans.Count - 1);
                var plan = dashPlans[planIndex];
                dashPlans.RemoveAt(planIndex);
                dashPlans.Insert(destination, plan);
                RebuildDashPath(Mathf.Min(planIndex, destination));
            }
            else destination = Mathf.Max(destination, dashPlans.Count);
            batch.Reorder(moved, destination);
            GameSfx.Play(Sfx.QueueMove);
            Refresh();
        }

        internal bool BeginCardDrag(HandCard card, PointerEventData e)
        {
            if (dungeonFloors != null && dungeonFinished) return false;
            if (batch == null || card == null || card.Kind != CardKind.PhaseDash
                || dashDrag != null || dashPlans.Exists(plan => plan.Card == card)) return false;
            var state = batch.GetSelectionState(card);
            if (state is CardSelectionState.Unavailable or CardSelectionState.Locked) return false;
            if (state == CardSelectionState.InsufficientEnergy)
            {
                ShowEnergyDenied(card);
                return false;
            }
            dashDrag = card;
            UpdatePlanningState();
            UpdateCardDrag(e);
            return true;
        }

        internal void UpdateCardDrag(PointerEventData e)
        {
            if (dashDrag == null || e == null) return;
            var plan = BuildDashPlan(dashDrag, e.position);
            Vector3 origin = new(plan.Origin.x, plan.Origin.y, movement.transform.position.z);
            dashAimPreview?.Show(origin, plan.Direction, dashDrag.Definition,
                plan.Destination, plan.Destination);
        }

        internal void EndCardDrag(PointerEventData e)
        {
            if (dashDrag == null) return;
            var card = dashDrag;
            dashDrag = null;
            dashAimPreview?.Hide();

            if (!Left(e) || !cameraView.pixelRect.Contains(e.position) || IsPointerOverUi(e))
            {
                UpdatePlanningState();
                return;
            }

            var plan = BuildDashPlan(card, e.position);
            if (plan.Distance < 0.001f)
            {
                GameSfx.Play(Sfx.BadLanding);
                Status("该方向没有可用的位移距离");
                UpdatePlanningState();
                return;
            }
            if (!batch.IsQueued(card) && !batch.Select(card))
            {
                if (batch.GetSelectionState(card) == CardSelectionState.InsufficientEnergy) ShowEnergyDenied(card);
                UpdatePlanningState();
                return;
            }

            // Planned jumps execute as a contiguous chain from the player's current position.
            batch.Reorder(card, dashPlans.Count);
            dashPlans.Add(plan);
            GameSfx.Play(Sfx.PlanConfirm);
            UpdatePredictionVisuals();
            Refresh();
            Status("预测位移已加入");
        }

        private void CancelCardDrag()
        {
            if (dashDrag != null && views.TryGetValue(dashDrag, out var view)) view.StopDragging();
            dashDrag = null;
            dashAimPreview?.Hide();
            if (movement != null) UpdatePlanningState();
        }

        private DashPlan BuildDashPlan(HandCard card, Vector2 screenPosition)
        {
            Vector2 origin = dashPlans.Count > 0 ? dashPlans[dashPlans.Count - 1].Destination : movement.Position;
            float depth = cameraView.WorldToScreenPoint(movement.transform.position).z;
            Vector3 screen = new(screenPosition.x, screenPosition.y, depth);
            return BuildDashPlan(card, origin, cameraView.ScreenToWorldPoint(screen));
        }

        private DashPlan BuildDashPlan(HandCard card, Vector2 origin, Vector2 aim)
        {
            Vector2 delta = aim - origin;
            Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : movement.FacingDirection;
            float distance = Mathf.Min(card.Definition.Distance, delta.magnitude);
            return new DashPlan(card, origin, aim, movement.GetDashDestination(origin, direction, distance, dashObstacleMask));
        }

        private void RebuildDashPath(int index)
        {
            Vector2 origin = index == 0 ? movement.Position : dashPlans[index - 1].Destination;
            for (int i = index; i < dashPlans.Count; ++i)
            {
                var plan = dashPlans[i];
                dashPlans[i] = BuildDashPlan(plan.Card, origin, plan.Aim);
                origin = dashPlans[i].Destination;
            }
            UpdatePredictionVisuals();
        }

        private void UpdatePredictionVisuals()
        {
            if (dashPlans.Count == 0)
            {
                dashVisual?.Hide();
                cameraFollow?.SetPredictionTarget(null);
                UpdatePlanningState();
                return;
            }

            var points = new List<Vector2>(dashPlans.Count + 1) { dashPlans[0].Origin };
            foreach (var plan in dashPlans) points.Add(plan.Destination);
            dashVisual?.Show(points);
            for (int i = 0; i < dashPlans.Count; ++i)
            {
                var plan = dashPlans[i];
                dashVisual?.ShowDamageArea(i, new CardAttackArea(plan.Card.Definition, plan.Origin, plan.Direction, plan.Destination));
            }
            cameraFollow?.SetPredictionTarget(FreezeForDashPlan ? dashVisual?.GhostTransform : null);
            UpdatePlanningState();
        }

        private bool FreezeForDashPlan => !batch.Busy && (dashDrag != null || dashPlans.Count > 0);

        private void UpdatePlanningState()
        {
            if (batch == null || batch.Busy) return;
            movement.SetPlanningLock(mainMenu != null || DungeonRoomTransitioning || FreezeForDashPlan || DungeonInteractionOpen ||
                DungeonMenuOpen || DungeonActorAnimating || dungeonFinished && dungeonRoom != null);
            timeStopPostProcess?.SetDashActive(FreezeForDashPlan);
        }

        private void UndoLastDashPrediction()
        {
            if (dashPlans.Count == 0) return;
            if (!RemoveQueuedCard(dashPlans[dashPlans.Count - 1].Card, playSound: false)) return;
            GameSfx.Play(Sfx.PlanUndo);
            Status("已撤销预测位移");
        }

        private void ClearDashPredictions()
        {
            CancelCardDrag();
            queueDrag = null;
            foreach (var plan in dashPlans) batch.Remove(plan.Card);
            dashPlans.Clear();
            GameSfx.Play(Sfx.QueueClear);
            UpdatePredictionVisuals();
            Refresh();
            Status("已清空预测位移");
        }

        private bool IsPointerOverUi(PointerEventData e)
        {
            var hit = e?.pointerCurrentRaycast.gameObject;
            return hit != null && hit.GetComponentInParent<Canvas>() != null;
        }

        private void SubmitCombo()
        {
            if (batch.Busy || dungeonFloors != null && dungeonFinished) return;
            if (selectedTarget == null || !selectedTarget.Alive)
            {
                foreach (var card in batch.Queue)
                {
                    if (!card.Definition.IsTargeted) continue;
                    GameSfx.Play(Sfx.BadTarget);
                    Status(card.Definition.DisplayName + "需要目标，可按 Q、空格或右键锁定");
                    return;
                }
            }
            if (queueDrag != null || dashDrag != null) return;
            if (batch.Vitals.DualThreadRemaining > 0d && batch.Queue.Count >= 2 &&
                (!ValidateCast(batch.Queue[0].Definition, selectedTarget) ||
                    !ValidateCast(batch.Queue[1].Definition, selectedTarget))) return;
            if (!batch.Submit())
            {
                if (batch.ReservedEnergy > batch.Energy + 0.0001f)
                {
                    GameSfx.Play(Sfx.EnergyDenied);
                    Status("能量不足");
                }
                return;
            }

            executingDashPlans.Clear();
            executingDashPlans.AddRange(dashPlans);
            dashPlans.Clear();
            timeStopPostProcess?.SetDashActive(false);
            dashAimPreview?.Hide();
            dashVisual?.Hide();
            cameraFollow?.SetPredictionTarget(null);
            movement.SetPlanningLock(executingDashPlans.Count > 0);
            BeginExecution();
        }

        private void BeginExecution()
        {
            GameSfx.Play(Sfx.CardSubmit);
            SetPile(-1);
            SetClock(1f);
            StartCoroutine(Execute());
        }

        private IEnumerator Execute()
        {
            var costs = new int[2];
            var replacements = new HandCard[2];
            var routines = new IEnumerator[2];

            while (batch.Busy)
            {
                int count = batch.CurrentGroupSize;
                bool paired = count == 2;
                var target = selectedTarget;
                bool valid = true;
                bool includesBigBang = false;
                int motionOwner = 0;
                int syncFrame = 0;

                for (int i = 0; i < count; i++)
                {
                    var definition = batch.CurrentAt(i).Definition;
                    if (!ValidateCast(definition, target))
                    {
                        valid = false;
                        break;
                    }
                    if (MotionPriority(definition.Kind) > MotionPriority(batch.CurrentAt(motionOwner).Kind))
                        motionOwner = i;
                    syncFrame = Mathf.Max(syncFrame, definition.ActiveStartFrame);
                    includesBigBang |= definition.Kind == CardKind.BigBang;
                }

                if (!valid || !batch.TryPlayCurrentGroup(costs, replacements))
                {
                    if (valid)
                    {
                        GameSfx.Play(Sfx.EnergyDenied);
                        Status("能量不足，无法出牌");
                    }
                    batch.CompleteCurrent(false);
                    continue;
                }

                if (paired)
                {
                    dualThreadWasActive = false;
                    GameSfx.Play(Sfx.DualPair);
                }
                if (includesBigBang)
                {
                    bigBangTimeStop = true;
                    SetClock(0f);
                    timeStopPostProcess.PlayBigBang(movement.Position);
                    GameSfx.Play(Sfx.BigBangCharge);
                    Status("大爆炸  ·  时停");
                    yield return new WaitForSecondsRealtime(CombatTimeStopPostProcess.BigBangInversionSeconds);
                    bigBangTimeStop = false;
                    timeStopPostProcess.RestoreBigBangColors();
                    SetClock(1f);
                }
                GameSfx.Play(Sfx.CastBurn);
                Vector2 facing = movement.FacingDirection;
                // The inversion supplies the wind-up. Both paired cards reach impact as color starts to return.
                int skippedFrames = includesBigBang ? syncFrame : 0;
                double startedAt = Time.timeAsDouble - skippedFrames / 60d;
                string castLabel = "";
                bool hasFlurryScreen = false;
                var motionPlan = FindExecutingDashPlan(batch.CurrentAt(motionOwner));
                movement.SetPlanningLock(motionPlan != null);
                for (int i = 0; i < count; i++)
                {
                    var card = batch.CurrentAt(i);
                    var definition = card.Definition;
                    var plan = FindExecutingDashPlan(card);
                    float multiplier = batch.MultiplierForCurrent(i);
                    var consumed = views[card];
                    views.Remove(card);
                    consumed.transform.SetParent(spentCards, false);
                    consumed.gameObject.AddComponent<CardDissolve>().Play(consumed, card);

                    castLabel += (i == 0 ? "" : "  +  ") + ClassSymbol(card.Class) + " " + definition.DisplayName;
                    if (i == count - 1 && !paired && card.Kind is not (CardKind.Sandevistan or CardKind.DualThread
                        or CardKind.InfiniteBlueWhiteHole or CardKind.BigBang))
                        castLabel += "  ×" + multiplier.ToString("0.##");

                    int delayFrames = syncFrame - definition.ActiveStartFrame;
                    var cast = combat.PrepareCast(i, i == motionOwner, paired, startedAt, delayFrames);
                    ShowCastCard(definition, costs[i], paired ? (i == 0 ? -0.58f : 0.58f) : 0f,
                        (definition.EndFrame + delayFrames - skippedFrames) / 60f);

                    if (card.Kind == CardKind.ThousandSlashes)
                    {
                        routines[i] = combat.PlayFlurry(cast, definition, target, batch.Vitals,
                            multiplier, PlayFlurryHitFeedback, dashObstacleMask, hasFlurryScreen ? null : flurryScreen);
                        hasFlurryScreen = true;
                    }
                    else
                    {
                        // Dragged dashes retain their selected world-space aim when cast in parallel.
                        routines[i] = combat.Play(cast, definition, plan?.Direction ?? facing,
                            plan != null ? null : target, batch.Vitals, multiplier, PlayHitFeedback,
                            paired || dungeonFloors != null ? plan?.Aim : plan?.Destination, dashObstacleMask);
                    }
                    if (plan != null) executingDashPlans.Remove(plan);
                }

                for (int i = 0; i < count; i++)
                    if (replacements[i]?.Kind == CardKind.BigBang)
                        castLabel += "  ·  临时获得大爆炸";
                Status(castLabel);
                Refresh();
                for (int i = 0; i < count; i++)
                    if (replacements[i] != null && views.TryGetValue(replacements[i], out var replacementView))
                        replacementView.DealFrom(previewSlots[0].anchoredPosition, PreviewScale);

                // Advance both timelines on the same rendered frame, even during hit stop.
                while (true)
                {
                    bool running = false;
                    for (int i = 0; i < count; i++)
                    {
                        if (routines[i] == null) continue;
                        if (routines[i].MoveNext()) running = true;
                        else routines[i] = null;
                    }
                    if (!running) break;
                    yield return null;
                }
                ClearCastCards();
                batch.CompleteCurrent(true);
                if (batch.Busy) Refresh();
            }
            executingDashPlans.Clear();
            RebuildDashPath(0);
            Refresh();
        }

        private bool ValidateCast(CardDefinition definition, CombatTarget target)
        {
            bool flurry = definition.Kind == CardKind.ThousandSlashes;
            if (definition.IsTargeted && !combat.CanTarget(target, definition))
            {
                GameSfx.Play(Sfx.BadTarget);
                Status(definition.DisplayName + (flurry ? "需要锁定目标" : "需要范围内的锁定目标"));
                return false;
            }
            if (flurry && !combat.TryGetFlurryDestination(target, dashObstacleMask, out _))
            {
                GameSfx.Play(Sfx.BadLanding);
                Status(definition.DisplayName + "需要目标身边有落脚点");
                return false;
            }
            return true;
        }

        private static int MotionPriority(CardKind kind) => kind switch
        {
            CardKind.PhaseDash or CardKind.ThousandSlashes => 2,
            CardKind.LimitedRedBlackHole => 1,
            _ => 0
        };

        private void ShowCastCard(CardDefinition definition, int cost, float offset, float duration)
        {
            var castObject = new GameObject("Casting card", typeof(RectTransform));
            castObject.layer = movement.gameObject.layer;
            castObject.transform.SetParent(movement.transform, false);
            var castCard = castObject.AddComponent<CastCardBurn>();
            castCards.Add(castCard);
            castCard.Play(definition, cost, offset, duration);
        }

        private DashPlan FindExecutingDashPlan(HandCard card)
        {
            foreach (var plan in executingDashPlans)
                if (plan.Card == card) return plan;
            return null;
        }

        private void Refresh()
        {
            foreach (Transform spent in spentCards)
            {
                if (!spent.gameObject.activeSelf || !spent.TryGetComponent<CardDissolve>(out var dissolve)) continue;
                bool visibleAgain = false;
                foreach (var card in batch.Hand)
                    if (card == dissolve.Card) { visibleAgain = true; break; }
                for (int i = 0; i < previewSlots.Length && !visibleAgain; ++i)
                    visibleAgain = batch.Upcoming(i) == dissolve.Card;
                if (!visibleAgain) continue;
                spent.gameObject.SetActive(false);
                Destroy(spent.gameObject);
            }
            for (int i = 0; i < batch.Hand.Count; ++i)
            {
                var card = batch.Hand[i];
                if (!views.TryGetValue(card, out var view))
                {
                    view = Instantiate(card.Definition, hand).GetComponent<CardHoverVisual>();
                    view.Bind(this, card, font);
                    views.Add(card, view);
                }
                float offset = i - (batch.Hand.Count - 1) * 0.5f;
                view.SetSlot(new Vector2(offset * 156f, 111f));
            }
            spentCards.SetAsLastSibling();
            if (shownPile >= 0) pilePanel.SetAsLastSibling();

            for (int i = 0; i < previewSlots.Length; ++i)
            {
                var upcoming = batch.Upcoming(i);
                CardHoverVisual.UpdateThumbnail(previewIcons[i], upcoming?.Definition);
                previewLabels[i].text = upcoming == null ? "" : $"{upcoming.Definition.DisplayName} {batch.PlannedEnergyCost(upcoming)}";
            }

            for (int i = 0; i < slots.Length; ++i)
            {
                var card = i < batch.Queue.Count ? batch.Queue[i] : null;
                slotFrames[i].raycastTarget = card != null;
                slotOrderLabels[i].enabled = card != null;
                slotCostLabels[i].enabled = card != null;
                slotLabels[i].enabled = card != null;
                CardHoverVisual.UpdateThumbnail(slotIcons[i], card?.Definition);
                if (card == null) continue;
                slotCostLabels[i].text = batch.PlannedEnergyCost(card).ToString();
                slotLabels[i].text = card.Definition.DisplayName;
            }
            drawText.text = "\u62bd\u724c " + batch.DrawCount;
            discardText.text = "\u5f03\u724c " + batch.DiscardCount;
            int pendingCount = batch.Queue.Count;
            string action = batch.Busy ? "出招中" : "空格提交";
            string cost = batch.Busy ? $"下组 {batch.ReservedEnergy:0} 费" : $"{batch.ReservedEnergy:0} 费";
            actionText.text = pendingCount > 0 ? $"{action}  ·  {cost}" : batch.Busy ? action : "空格出牌";
            UpdateSelectionStates();
            resetButton.interactable = !batch.Busy;
            if (shownPile >= 0) SetPile(shownPile);
        }

        internal Color EvaluateReadyFlash(float elapsed)
        {
            float fade = (elapsed - readyFlashHold) / readyFlashFade;
            if (fade >= 1f) return Color.clear;

            Color tint = readyFlashColor;
            tint.a = readyFlashIntensity * (1f - Mathf.SmoothStep(0f, 1f, fade));
            return tint;
        }

        private void UpdateSelectionStates()
        {
            float freeEnergy = batch.Energy - batch.ReservedEnergy;
            foreach (var entry in views)
            {
                var state = batch.GetSelectionState(entry.Key);
                int cost = batch.PlannedEnergyCost(entry.Key);
                float progress = state switch
                {
                    CardSelectionState.InsufficientEnergy => cost > 0 ? Mathf.Clamp01(freeEnergy / cost) : 0f,
                    CardSelectionState.Unavailable or CardSelectionState.Locked => 0f,
                    _ => 1f
                };
                entry.Value.SetEnergyCost(cost);
                entry.Value.SetSelectionState(state, progress);
            }

            bool canSubmit = batch.Queue.Count > 0 && !batch.Busy && batch.Vitals.CanSpendEnergy(batch.ReservedEnergy);
            actionButton.interactable = canSubmit;
        }

        private void Status(string value) { statusText.text = value; statusUntil = Time.unscaledTime + 1.5f; }
        private static string ClassSymbol(CardClass type) => type switch
        {
            CardClass.Defense => "\u25a1", CardClass.Ability => "\u25cb", _ => "\u25b3"
        };
        private void EnsureDummy()
        {
            foreach (var enemy in CombatTarget.Active)
                if (enemy is TestDummy) return;
            if (dummyPrefab == null) return;
            var forward = movement.FacingDirection;
            var point = movement.transform.position + new Vector3(-forward.y, forward.x) * 2.1f;
            createdDummy = Instantiate(dummyPrefab, point, Quaternion.identity);
        }
        private void SpawnTestEnemies()
        {
            var placedRoles = new HashSet<EnemyRole>();
            foreach (var target in CombatTarget.Active)
                if (target is ProgramEnemy placed)
                {
                    placed.Initialize(movement, batch.Vitals, dashObstacleMask);
                    placedRoles.Add(placed.Role);
                }

            SpawnTestEnemy(meleePrefab, placedRoles, 4f, -2.2f);
            if (additionalEnemyPrefabs == null) return;
            foreach (var prefab in additionalEnemyPrefabs)
            {
                if (prefab == null) continue;
                switch (prefab.Role)
                {
                    case EnemyRole.Shooter: SpawnTestEnemy(prefab, placedRoles, 5.6f, 2.25f); break;
                    case EnemyRole.Swarm: SpawnTestEnemy(prefab, placedRoles, 2.8f, 3f); break;
                    case EnemyRole.Shield: SpawnTestEnemy(prefab, placedRoles, 5.8f, -0.55f); break;
                }
            }
        }

        private void SpawnTestEnemy(ProgramEnemy prefab, HashSet<EnemyRole> placedRoles, float depth, float offset)
        {
            if (prefab == null || !placedRoles.Add(prefab.Role)) return;
            SpawnEnemy(prefab, depth, offset);
        }

        private ProgramEnemy SpawnEnemy(ProgramEnemy prefab, float depth, float offset)
        {
            if (prefab == null) return null;
            var forward = movement.FacingDirection;
            var point = movement.transform.position + (Vector3)(forward * depth
                + new Vector2(-forward.y, forward.x) * offset);
            var enemy = Instantiate(prefab, point, Quaternion.identity);
            createdEnemies.Add(enemy);
            enemy.Initialize(movement, batch.Vitals, dashObstacleMask);
            return enemy;
        }

        private void NextTestEncounter()
        {
            int eliteCount = eliteEnemyPrefabs?.Length ?? 0;
            int encounterCount = 1 + eliteCount + (firewallBossPrefab != null ? 1 : 0);
            if (encounterCount <= 1) return;
            GameSfx.Play(Sfx.TrialSwitch);
            PrepareTestEncounter();
            testEncounter = (testEncounter + 1) % encounterCount;
            if (testEncounter > eliteCount)
            {
                SpawnFirewallTrial();
                return;
            }
            if (testEncounter == 0)
            {
                SpawnTestEnemies();
                Status("训练编成");
                return;
            }

            var prefab = eliteEnemyPrefabs[testEncounter - 1];
            var elite = SpawnEnemy(prefab, 3.5f, prefab != null && prefab.Role == EnemyRole.Cluster ? -0.85f : 0f);
            if (elite == null) return;
            switch (elite.Role)
            {
                case EnemyRole.Resonator:
                    SpawnEnemy(meleePrefab, 4.4f, -1.9f);
                    SpawnEnemy(meleePrefab, 4.4f, 1.9f);
                    break;
                case EnemyRole.Cluster:
                    SpawnEnemy(prefab, 3.5f, 0.85f);
                    break;
                case EnemyRole.Protocol:
                    SpawnEnemy(meleePrefab, 4.1f, -1.6f);
                    break;
            }
            Status("精英试战  ·  " + elite.DisplayName);
        }

        private void PrepareTestEncounter()
        {
            ClearRootHandLocks();
            playerImpact?.Clear();
            CancelCardDrag();
            queueDrag = null;
            SetSelectedTarget(null);
            ClearSpawnedEnemies();
            CloseFirewallTrial(restorePosition: true);
            batch.Vitals.ResetHealth();
            batch.Vitals.ClearSandevistan();
            batch.Vitals.ClearDualThread();
            ExpireTemporaryCards();
            Refresh();
        }

        private void OpenFirewallTrial()
        {
            if (firewallBossPrefab == null) return;
            PrepareTestEncounter();
            testEncounter = (eliteEnemyPrefabs?.Length ?? 0) + 1;
            SpawnFirewallTrial();
        }

        private void SpawnFirewallTrial()
        {
            GameSfx.Play(Sfx.BossEnter);
            beforeFirewallTrial = movement.Position;
            // Allow the full width of the room clear of the training wall.
            Vector2 arenaCenter = new(FirewallBoss.ArenaHalfSize.x + 3.2f, 0f);
            movement.SetCardPosition(arenaCenter + FirewallBoss.TrialSpawnOffset);
            foreach (var target in CombatTarget.Active.ToArray())
            {
                targetsHiddenForFirewall.Add(target.gameObject);
                target.gameObject.SetActive(false);
            }
            firewallBoss = Instantiate(firewallBossPrefab, arenaCenter, Quaternion.identity);
            firewallBoss.Initialize(movement, batch.Vitals, dashObstacleMask, cameraFollow, root, encloseTrial: true);
            SetBossHudTarget(firewallBoss);
            cameraFollow?.FrameTrialEntrance(arenaCenter, FirewallBoss.ArenaHalfSize);
            ShowDummyReadout(null);
            Status("Boss 试战  ·  " + firewallBoss.DisplayName);
        }

        private void CloseFirewallTrial(bool restorePosition)
        {
            if (firewallBoss == null) return;
            SetBossHudTarget(null);
            firewallBoss.gameObject.SetActive(false);
            Destroy(firewallBoss.gameObject);
            firewallBoss = null;
            foreach (var target in targetsHiddenForFirewall)
                if (target != null) target.SetActive(true);
            targetsHiddenForFirewall.Clear();
            cameraFollow?.EndTrialFraming();
            if (restorePosition && movement != null) movement.SetCardPosition(beforeFirewallTrial);
        }

        private void ClearSpawnedEnemies()
        {
            foreach (var enemy in createdEnemies)
            {
                if (enemy == null) continue;
                enemy.RemoveSpawnedFragments();
                enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
            }
            createdEnemies.Clear();
        }

        private void ResetDummies()
        {
            if (batch.Busy || dungeonFloors != null) return;
            ClearRootHandLocks();
            playerImpact?.Clear();
            GameSfx.Play(Sfx.TrialReset);
            ExpireTemporaryCards();
            Refresh();
            if (firewallBoss != null)
            {
                batch.Vitals.ResetHealth();
                batch.Vitals.ClearSandevistan();
                batch.Vitals.ClearDualThread();
                movement.SetCardPosition((Vector2)firewallBoss.transform.position + FirewallBoss.TrialSpawnOffset);
                SetSelectedTarget(null);
                firewallBoss.ResetTarget();
                cameraFollow?.FrameTrialEntrance(firewallBoss.transform.position, FirewallBoss.ArenaHalfSize);
                return;
            }
            foreach (var target in CombatTarget.Active.ToArray())
                if (target != null && target.isActiveAndEnabled) target.ResetTarget();
        }

        private void ClearCastCards()
        {
            foreach (var card in castCards)
            {
                if (card == null) continue;
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }
            castCards.Clear();
        }

        private void ExpireTemporaryCards()
        {
            foreach (var card in batch.Hand)
            {
                if (!card.Definition.IsTemporary || !views.Remove(card, out var view)) continue;
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }
            batch.ExpireTemporaryCards();
        }

        private void ConnectCombat()
        {
            CombatTarget.BattleVitals = batch.Vitals;
            CombatTarget.EnemyDefeated += PlayKillSlowMotion;
            combat.EnemyKilled += RecoverConsumedCards;
            batch.Vitals.Impact += playerImpact.Play;
        }

        private void RecoverConsumedCards()
        {
            if (!batch.RecoverConsumed()) return;
            GameSfx.Play(Sfx.CardRecover);
            Refresh();
        }

        private void OnDisable()
        {
            ClearRootHandLocks();
            StopMusic();
            if (mainMenu != null)
            {
                Destroy(mainMenu.gameObject);
                mainMenu = null;
            }
            CancelDungeonActorAnimation();
            GameSfx.StopLoop(ref sandevistanLoop);
            handWasShown = dualThreadWasActive = false;
            CloseFirewallTrial(restorePosition: true);
            if (bossHudTarget != null) bossHudTarget.SetWorldHealthVisible(true);
            if (bossHudPanel != null) bossHudPanel.gameObject.SetActive(false);
            CombatTarget.EnemyDefeated -= PlayKillSlowMotion;
            if (combat != null) combat.EnemyKilled -= RecoverConsumedCards;
            if (batch != null)
            {
                batch.Vitals.Impact -= playerImpact.Play;
                batch.Vitals.KillSlowMotionUntil = 0d;
                batch.Vitals.ClearSandevistan();
                batch.Vitals.ClearDualThread();
                if (CombatTarget.BattleVitals == batch.Vitals) CombatTarget.BattleVitals = null;
            }
            StopAllCoroutines();
            bigBangTimeStop = false;
            HideDungeonRoomCurtain();
            flurryHitStop = false;
            flurryScreen?.End();
            damageVignette?.Clear();
            playerImpact?.Clear();
            ClearCastCards();
            CancelCardDrag();
            dashAimPreview?.Hide();
            SetSelectedTarget(null);
            hitStopUntil = 0f;
            if (cameraFollow != null) cameraFollow.ClearShake();
            if (combat != null && batch?.Busy == true)
            {
                combat.ResetMotion();
                while (batch.Busy) batch.CompleteCurrent(false);
            }
            if (batch != null)
                foreach (var plan in dashPlans) batch.Remove(plan.Card);
            dashPlans.Clear();
            timeStopPostProcess?.SetDashActive(false);
            timeStopPostProcess?.SetSandevistan(null, null, 0f, 0d);
            timeStopPostProcess?.ClearBigBang();
            executingDashPlans.Clear();
            dashVisual?.Hide();
            cameraFollow?.SetPredictionTarget(null);
            movement?.SetPlanningLock(false);
            queueDrag = null;
            if (!ownsClock) return;
            Time.timeScale = oldScale;
            Time.fixedDeltaTime = oldFixed;
            ownsClock = false;
        }
        private void OnEnable()
        {
            if (batch == null || root == null || energyGauge == null) return;
            ConnectCombat();
            oldScale = Time.timeScale;
            oldFixed = Time.fixedDeltaTime;
            ownsClock = true;
            Refresh();
            if (dungeonRoom != null)
            {
                RefreshDungeonInteraction();
                PlayMusic(CombatMusicPath);
            }
            UpdatePlanningState();
        }
        private void OnDestroy()
        {
            if (rootHandLockMaterial != null) Destroy(rootHandLockMaterial);
            DestroyDungeonRoom();
            CloseFirewallTrial(restorePosition: false);
            if (createdDummy != null) Destroy(createdDummy.gameObject);
            ClearSpawnedEnemies();
            if (hudFrameSprite != null) Destroy(hudFrameSprite);
            dashAimPreview?.Dispose();
            timeStopPostProcess?.Dispose();
            flurryScreen?.Dispose();
            damageVignette?.Dispose();
            playerImpact?.Dispose();
            if (dashVisual != null) Destroy(dashVisual.gameObject);
            if (font != null) Destroy(font);
        }

        private static bool Left(PointerEventData e) => e != null && e.button == PointerEventData.InputButton.Left;
        private static void Trigger(EventTrigger trigger, EventTriggerType type, System.Action<PointerEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(e => callback(e as PointerEventData));
            trigger.triggers.Add(entry);
        }
        private static RectTransform Box(string item, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rect = new GameObject(item, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }
        private static Image Paint(RectTransform rect, Color color, bool interactive, Sprite sprite = null)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            if (sprite != null && sprite.border.sqrMagnitude > 0) image.type = Image.Type.Sliced;
            image.raycastTarget = interactive;
            return image;
        }
        private Text Label(string item, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, int points)
        {
            var text = Box(item, parent, anchor, pos, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = points;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Paper;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, points - 4);
            text.resizeTextMaxSize = points;
            return text;
        }
        private static void OutlineText(Text text)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = Ink;
            outline.effectDistance = new Vector2(1, -1);
        }
        private Button ButtonAt(string item, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction click, out Text label)
        {
            var rect = Box(item, parent, anchor, pos, size);
            var bg = Paint(rect, Ink, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.72f);
            button.colors = colors;
            button.onClick.AddListener(click);
            label = Label("Label", rect, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(4, 0), 16);
            return button;
        }
    }
}
