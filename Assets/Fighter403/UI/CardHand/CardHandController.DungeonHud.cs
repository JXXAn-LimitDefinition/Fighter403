using System.Collections.Generic;
using System.Linq;
using Fighter403.Audio;
using Fighter403.Debuggers;
using UnityEngine;
using UnityEngine.UI;

namespace Fighter403.UI
{
    public sealed partial class CardHandController
    {
        private static readonly Vector2 TopRight = Vector2.one;
        private static readonly Color MapPanel = new Color32(12, 22, 36, 248);
        private static readonly Color MapAccent = new Color32(107, 220, 236, 255);
        private const float MapPanelTop = -72f, MapGraphWidth = 204f, MapGraphHeight = 124f;
        private RectTransform dungeonSetup, dungeonMenu, dungeonHud, dungeonMap, mapTiles, dungeonEntry;
        private Text dungeonMenuText, dungeonFloorText, dungeonMapText, dungeonFragmentText;
        private InputField dungeonSeedInput;
        private CanvasGroup dungeonSetupFade, dungeonHudFade, dungeonMapFade;
        private RectTransform dungeonCurrentRoomNode;
        private Image dungeonMapToggleFrame, dungeonMapScanline, dungeonSetupScanline;
        private DungeonRoom dungeonHudRoom;
        private int displayedFloor = -1, displayedFragments;
        private float dungeonRoomChangedAt = float.NegativeInfinity, dungeonFragmentsChangedAt = float.NegativeInfinity;
        private float dungeonHudReveal, dungeonMapReveal, dungeonSetupReveal;
        private bool dungeonMapVisible, dungeonMenuOpen;
        private bool DungeonMenuOpen => dungeonMenuOpen;
        private int ShopCardRemovalPrice => 200 + 100 * dungeonFloor;

        private void BuildDungeonUi()
        {
            var entryButton = ButtonAt("Dungeon mode", root, TopRight, new Vector2(-80, -71), new Vector2(134, 34),
                OpenDungeonSetup, out var entry);
            dungeonEntry = (RectTransform)entryButton.transform;
            StyleInterfaceButton(entryButton, MapPanel, OccupiedSocket);
            AddCircuitBand(dungeonEntry, new Vector2(0.5f, 0f), Vector2.up * 5f, new Vector2(112f, 8f), .24f);
            entry.text = "地牢探索";

            dungeonHud = Box("Dungeon status", root, TopRight, Vector2.zero, new Vector2(226, 68));
            dungeonHud.pivot = TopRight;
            PaintInterfacePlate(dungeonHud, MapPanel, QueueFrame);
            AddCircuitBand(dungeonHud, new Vector2(0.5f, 0f), Vector2.up * 5f, new Vector2(204f, 8f), .19f);
            dungeonHudFade = dungeonHud.gameObject.AddComponent<CanvasGroup>();
            var top = new Vector2(.5f, 1f);
            var left = new Vector2(0f, 1f);
            var floorBadge = Box("Floor badge", dungeonHud, left, new Vector2(31f, -21f), new Vector2(40f, 23f));
            Paint(floorBadge, InterfaceInset, false);
            dungeonFloorText = Label("Floor number", floorBadge, DungeonMiddle, Vector2.zero, floorBadge.sizeDelta, 16);
            dungeonFloorText.color = MapAccent;
            dungeonFloorText.fontStyle = FontStyle.Bold;
            dungeonMapText = Label("Zone name", dungeonHud, left, new Vector2(137, -20), new Vector2(158, 26), 18);
            dungeonMapText.alignment = TextAnchor.MiddleLeft;
            dungeonMapText.fontStyle = FontStyle.Bold;
            dungeonMapText.color = InterfaceText;
            var gem = Box("Fragments mark", dungeonHud, left, new Vector2(17f, -47f), new Vector2(7f, 7f));
            gem.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Paint(gem, MapAccent, false);
            dungeonFragmentText = Label("Fragments", dungeonHud, left, new Vector2(69, -47), new Vector2(88, 23), 15);
            dungeonFragmentText.alignment = TextAnchor.MiddleLeft;
            dungeonFragmentText.color = DungeonMuted;

            var mapButton = ButtonAt("Toggle map", dungeonHud, top, new Vector2(86, -47), new Vector2(42, 23),
                ToggleDungeonMap, out var mapLabel);
            StyleInterfaceButton(mapButton, InterfaceInset, QueueFrame);
            mapLabel.text = "Tab";
            mapLabel.resizeTextMaxSize = 13;
            dungeonMapToggleFrame = (Image)mapButton.targetGraphic;
            dungeonMap = Box("Dungeon map", dungeonHud, top, new Vector2(0, MapPanelTop), new Vector2(226, 156));
            dungeonMap.pivot = top;
            PaintInterfacePlate(dungeonMap, MapPanel, QueueFrame);
            AddCircuitBand(dungeonMap, TopRight, new Vector2(-58f, -13f), new Vector2(94f, 16f), .67f);
            dungeonMapFade = dungeonMap.gameObject.AddComponent<CanvasGroup>();
            var mapTitle = Label("Map title", dungeonMap, top, new Vector2(-42f, -13f), new Vector2(118, 20), 14);
            mapTitle.text = "楼层地图";
            mapTitle.color = InterfaceCyan;
            mapTitle.alignment = TextAnchor.MiddleLeft;
            var graphSurface = Box("Map background", dungeonMap, DungeonMiddle, Vector2.down * 10f,
                new Vector2(MapGraphWidth, MapGraphHeight));
            Paint(graphSurface, new Color32(10, 20, 32, 250), false);
            for (int i = -3; i <= 3; i++)
            {
                Paint(Box("Map grid column", graphSurface, DungeonMiddle, Vector2.right * (i * 25f),
                    new Vector2(1f, MapGraphHeight)), new Color32(27, 49, 67, 145), false);
                if (Mathf.Abs(i) <= 2)
                    Paint(Box("Map grid row", graphSurface, DungeonMiddle, Vector2.up * (i * 24f),
                        new Vector2(MapGraphWidth, 1f)), new Color32(27, 49, 67, 145), false);
            }
            mapTiles = Box("Room graph", graphSurface, DungeonMiddle, Vector2.zero, graphSurface.sizeDelta);
            dungeonMapScanline = Paint(Box("Map scan", graphSurface, DungeonMiddle, Vector2.zero,
                new Vector2(MapGraphWidth, 2f)), MapAccent, false);
            dungeonMap.gameObject.SetActive(false);
            dungeonHud.gameObject.SetActive(false);

            BuildDungeonInteraction();
            dungeonSetup = BuildDungeonShade("Dungeon menu overlay");
            dungeonSetupFade = dungeonSetup.gameObject.AddComponent<CanvasGroup>();
            dungeonSetupFade.alpha = 0f;
            dungeonMenu = Box("Dungeon setup", dungeonSetup, DungeonMiddle, Vector2.zero, new Vector2(460, 214));
            PaintInterfacePlate(dungeonMenu, MapPanel, OccupiedSocket, true);
            AddCircuitMargins(dungeonMenu, 18f, 12f, .08f);
            var title = Label("Dungeon setup title", dungeonMenu, top, new Vector2(0, -37), new Vector2(414, 35), 25);
            title.text = "地牢探索";
            title.color = InterfaceText;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleLeft;
            dungeonMenuText = Label("Seed instructions", dungeonMenu, top, new Vector2(0, -65), new Vector2(414, 25), 16);
            dungeonMenuText.color = InterfaceMuted;
            dungeonMenuText.alignment = TextAnchor.MiddleLeft;
            var divider = Box("Header divider", dungeonMenu, top, new Vector2(0, -84), new Vector2(414, 1));
            Paint(divider, QueueFrame, false);
            dungeonSetupScanline = Paint(Box("Header sweep", divider, new Vector2(0f, .5f), Vector2.zero,
                new Vector2(30f, 2f)), InterfaceCyan, false);
            var field = Box("Seed field", dungeonMenu, DungeonMiddle, new Vector2(0, -4f), new Vector2(414, 38));
            PaintInterfacePlate(field, InterfaceInset, QueueFrame, true);
            dungeonSeedInput = field.gameObject.AddComponent<InputField>();
            dungeonSeedInput.textComponent = Label("Seed text", field, DungeonMiddle, Vector2.zero, new Vector2(386, 34), 18);
            dungeonSeedInput.textComponent.resizeTextForBestFit = false;
            dungeonSeedInput.textComponent.alignment = TextAnchor.MiddleLeft;
            dungeonSeedInput.textComponent.color = InterfaceText;
            dungeonSeedInput.targetGraphic = field.GetComponent<Image>();
            var hint = Label("Seed placeholder", field, DungeonMiddle, Vector2.zero, new Vector2(386, 34), 16);
            hint.text = "输入种子，留空则随机生成";
            hint.color = InterfaceMuted;
            hint.alignment = TextAnchor.MiddleLeft;
            dungeonSeedInput.placeholder = hint;
            dungeonSeedInput.characterLimit = 64;
            var bottom = new Vector2(.5f, 0f);
            var startButton = ButtonAt("Generate dungeon", dungeonMenu, bottom, new Vector2(118, 41), new Vector2(178, 39),
                () => BeginDungeon(dungeonSeedInput.text), out var start);
            start.text = "生成并进入";
            StyleInterfaceButton(startButton, new Color32(24, 66, 88, 255), InterfaceCyan);

            var closeButton = ButtonAt("Close dungeon menu", dungeonMenu, bottom, new Vector2(-150, 41), new Vector2(112, 39),
                DismissDungeonMenu, out var close);
            close.text = "关闭";
            StyleInterfaceButton(closeButton, InterfaceInset, QueueFrame);
            dungeonSetup.gameObject.SetActive(false);
        }


        private static void AddCircuitBand(RectTransform panel, Vector2 anchor, Vector2 position, Vector2 size,
            float phase)
        {
            Box("Electronic erosion", panel, anchor, position, size).gameObject
                .AddComponent<InterfaceCircuit>().Initialize(false, phase);
        }

        private static void AddCircuitMargins(RectTransform panel, float width, float margin, float phase)
        {
            for (int side = 0; side <= 1; side++)
            {
                var anchor = new Vector2(side, 0f);
                var strip = Box("Electronic erosion", panel, anchor, Vector2.zero, Vector2.zero);
                strip.anchorMax = new Vector2(side, 1f);
                float start = side == 0 ? 3f : -width - 3f;
                strip.offsetMin = new Vector2(start, margin);
                strip.offsetMax = new Vector2(start + width, -margin);
                strip.gameObject.AddComponent<InterfaceCircuit>().Initialize(true, phase + side * .43f, side != 0);
            }
        }

        private void OpenDungeonSetup()
        {
            if (DungeonMenuOpen || dungeonFloors != null || mainMenu != null) return;
            dungeonMenuText.text = "设置本局种子";
            dungeonMenuOpen = true;
            dungeonSetupFade.interactable = dungeonSetupFade.blocksRaycasts = true;
            dungeonSetup.SetAsLastSibling();
            dungeonSetup.gameObject.SetActive(true);
            GameSfx.Play(Sfx.PileOpen, .75f);
            UpdatePlanningState();
        }

        private void DismissDungeonMenu()
        {
            GameSfx.Play(Sfx.PileClose, .75f);
            CloseDungeonMenu();
        }

        private void CloseDungeonMenu()
        {
            dungeonMenuOpen = false;
            dungeonSeedInput.DeactivateInputField();
            dungeonSetupFade.interactable = dungeonSetupFade.blocksRaycasts = false;
            if (dungeonSetupReveal == 0f) dungeonSetup.gameObject.SetActive(false);
            UpdatePlanningState();
        }

        private void ToggleDungeonMap()
        {
            if (dungeonRoom == null) return;
            dungeonMapVisible = !dungeonMapVisible;
            if (dungeonMapVisible) dungeonMap.gameObject.SetActive(true);
            GameSfx.Play(dungeonMapVisible ? Sfx.PileOpen : Sfx.PileClose, .55f);
        }

        private void RefreshDungeonUi()
        {
            bool running = dungeonRoom != null;
            dungeonHud.gameObject.SetActive(running);
            dungeonEntry.gameObject.SetActive(!running);
            ClearDungeonChildren(mapTiles);
            dungeonCurrentRoomNode = null;
            if (running)
            {
                bool floorChanged = displayedFloor != dungeonFloor;
                if (floorChanged)
                {
                    displayedFloor = dungeonFloor;
                    dungeonHudReveal = 0f;
                    dungeonHudFade.alpha = 0f;
                }
                if (dungeonHudRoom != dungeonRoom)
                {
                    dungeonHudRoom = dungeonRoom;
                    dungeonRoomChangedAt = Time.unscaledTime;
                }
                if (displayedFragments != fragments)
                {
                    if (!floorChanged) dungeonFragmentsChangedAt = Time.unscaledTime;
                    displayedFragments = fragments;
                }
                dungeonFloorText.text = $"{dungeonFloor + 1}F";
                dungeonMapText.text = DungeonGenerator.ThemeLabels[dungeonFloor];
                dungeonFragmentText.text = $"碎片  {fragments}";
                if (dungeonMapVisible) dungeonMap.gameObject.SetActive(true);
                DrawDungeonMap();
            }
            else
            {
                displayedFloor = -1;
                dungeonHudRoom = null;
                dungeonHudReveal = dungeonMapReveal = 0f;
                dungeonMapFade.alpha = 0f;
                dungeonMap.gameObject.SetActive(false);
                dungeonFragmentsChangedAt = float.NegativeInfinity;
            }
            RefreshDungeonInteraction();
        }

        private void LayoutDungeonUi()
        {
            float scale = Mathf.Min(root.rect.width / 820f, root.rect.height / 720f);
            var size = Vector3.one * scale;
            if (dungeonShade.gameObject.activeSelf)
                LayoutTerminalPanel();
            dungeonPrompt.localScale = Vector3.one * Mathf.Max(.8f, scale);
            float hudScale = Mathf.Clamp(scale, .45f, 1.45f);
            dungeonHud.localScale = dungeonEntry.localScale = Vector3.one * hudScale;
            dungeonEntry.anchoredPosition = new Vector2(-80f * hudScale, -50f - 17f * hudScale);
            float dt = Time.unscaledDeltaTime;
            if (dungeonSetup.gameObject.activeSelf)
            {
                dungeonSetupReveal = Mathf.MoveTowards(dungeonSetupReveal, dungeonMenuOpen ? 1f : 0f,
                    dt / (dungeonMenuOpen ? .26f : .16f));
                float reveal = Mathf.SmoothStep(0f, 1f, dungeonSetupReveal);
                dungeonSetupFade.alpha = reveal;
                dungeonMenu.localScale = size * Mathf.Lerp(.95f, 1f, reveal);
                dungeonMenu.anchoredPosition = Vector2.down * (12f * scale * (1f - reveal));
                dungeonSetupScanline.rectTransform.anchoredPosition = Vector2.right * Mathf.Lerp(15f, 399f, reveal);
                dungeonSetupScanline.color = WithHudAlpha(InterfaceCyan, Mathf.Sin(reveal * Mathf.PI) * .85f);
                if (!dungeonMenuOpen && dungeonSetupReveal == 0f) dungeonSetup.gameObject.SetActive(false);
            }
            if (!dungeonHud.gameObject.activeSelf) return;

            dungeonHudReveal = Mathf.MoveTowards(dungeonHudReveal, 1f, dt / .23f);
            float hudReveal = Mathf.SmoothStep(0f, 1f, dungeonHudReveal);
            dungeonHudFade.alpha = hudReveal;
            dungeonHud.anchoredPosition = new Vector2(-24, -24 + 10f * (1f - hudReveal)) * hudScale;

            float fragmentPulse = 1f - Mathf.Clamp01((Time.unscaledTime - dungeonFragmentsChangedAt) / .34f);
            dungeonFragmentText.color = Color.Lerp(DungeonMuted, MapAccent, fragmentPulse);
            dungeonMapToggleFrame.color = dungeonMapVisible ? OccupiedSocket : QueueFrame;
            if (!dungeonMap.gameObject.activeSelf) return;
            dungeonMapReveal = Mathf.MoveTowards(dungeonMapReveal, dungeonMapVisible ? 1f : 0f,
                dt / (dungeonMapVisible ? .26f : .16f));
            float mapReveal = Mathf.SmoothStep(0f, 1f, dungeonMapReveal);
            dungeonMapFade.alpha = mapReveal;
            dungeonMap.localScale = new Vector3(1f, Mathf.Lerp(.86f, 1f, mapReveal), 1f);
            dungeonMap.anchoredPosition = new Vector2(0f, MapPanelTop + 7f * (1f - mapReveal));
            dungeonMapScanline.rectTransform.anchoredPosition = Vector2.up * Mathf.Lerp(MapGraphHeight / 2f - 1f,
                -MapGraphHeight / 2f + 1f, mapReveal);
            dungeonMapScanline.color = WithHudAlpha(MapAccent, Mathf.Sin(mapReveal * Mathf.PI) * .24f);
            if (!dungeonMapVisible && dungeonMapReveal == 0f) dungeonMap.gameObject.SetActive(false);
            if (dungeonCurrentRoomNode == null) return;
            float roomPulse = Mathf.Clamp01((Time.unscaledTime - dungeonRoomChangedAt) / .28f);
            dungeonCurrentRoomNode.localScale = Vector3.one * (1f + .2f * Mathf.Sin(roomPulse * Mathf.PI));
        }

        private static void ClearDungeonChildren(Transform parent)
        {
            foreach (Transform child in parent)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private void DrawDungeonMap()
        {
            var visible = dungeonFloors[dungeonFloor].Where(room => room.Kind != DungeonRoomKind.Hidden
                || room.Doors.Any(passage => passage?.Breached == true)).ToArray();
            Vector2 low = new(visible.Min(room => room.Cell.x), visible.Min(room => room.Cell.y));
            Vector2 high = new(visible.Max(room => room.Cell.x), visible.Max(room => room.Cell.y));
            Vector2 center = (high + low) * .5f;
            float pitch = Mathf.Min(29f, (MapGraphWidth - 8f) / (high.x - low.x + 1f),
                (MapGraphHeight - 8f) / (high.y - low.y + 1f));
            var edge = new Color32(49, 82, 109, 255);
            foreach (var room in visible)
            {
                Vector2 position = ((Vector2)room.Cell - center) * pitch;
                for (int side = 0; side < 2; side++)
                {
                    var passage = room.Doors[side];
                    if (passage == null || passage.Hidden && !passage.Breached) continue;
                    var direction = (Vector2)DungeonGenerator.Directions[side];
                    Vector2 size = side == 0 ? new Vector2(2, pitch) : new Vector2(pitch, 2);
                    bool traversed = room.Cleared && passage.Other(room).Cleared;
                    Paint(Box("Passage", mapTiles, DungeonMiddle, position + direction * (pitch * .5f), size),
                        traversed ? OccupiedSocket : edge, false);
                }
            }
            foreach (var room in visible)
            {
                Vector2 position = ((Vector2)room.Cell - center) * pitch;
                Vector2 size = Vector2.one * Mathf.Max(8, pitch - 5f);
                var node = Box("Room", mapTiles, DungeonMiddle, position, size);
                bool current = room == dungeonRoom;
                if (current) dungeonCurrentRoomNode = node;
                var frame = current ? InterfaceText : room.Cleared ? OccupiedSocket : edge;
                Paint(node, frame, false);
                Paint(Box("Room surface", node, DungeonMiddle, Vector2.zero, size - new Vector2(2f, 2f)),
                    current ? MapAccent : room.Cleared ? new Color32(25, 62, 76, 255) : InterfaceInset, false);
                var label = Label("Room type", node, DungeonMiddle, Vector2.zero, size, Mathf.Min(14, Mathf.FloorToInt(size.x)));
                label.resizeTextMinSize = 8;
                label.color = current ? MapPanel : InterfaceText;
                if (current) label.fontStyle = FontStyle.Bold;
                label.text = room.Kind switch
                {
                    DungeonRoomKind.Start => "起", DungeonRoomKind.Combat => "战",
                    DungeonRoomKind.Elite => "精", DungeonRoomKind.Vault => "宝",
                    DungeonRoomKind.Shop => "商", DungeonRoomKind.Maintenance => "修",
                    DungeonRoomKind.Event => "事", DungeonRoomKind.Hidden => "隐", _ => "B"
                };
            }
        }

        private IReadOnlyList<CardKind> OfferRoomCards(DungeonRoom room) =>
            DungeonGenerator.OfferCards(room, cardPrefabs.Where(card => card != null).Select(card => card.Kind),
                batch.DeckKinds(), batch.HasAcquiredThousandSlashes);

        private void BuildRoomChoices()
        {
            var room = dungeonRoom;
            if (dungeonFinished)
            {
                interactionTitle = dungeonLost ? "本局失败" : "探索完成";
                interactionKeyboardHint = "Enter 重新开始\nEsc 返回主界面";
                interactionSecondary = ("返回主界面", ReturnToMainMenu);
                AddRoomChoice("重新开始", "", RestartDungeon);
                return;
            }
            if (room.Kind == DungeonRoomKind.Shop)
            {
                interactionTitle = "商店";
                interactionSecondary = ("返回", BackFromDungeonInteraction);
                foreach (var kind in OfferRoomCards(room).Except(batch.DeckKinds()).Where(kind => !room.Bought.Contains(kind)))
                {
                    var card = DefinitionFor(kind);
                    if (card == null) continue;
                    AddCardChoice(card, () =>
                    {
                        if (!batch.AddToDeck(card)) return;
                        fragments -= 50;
                        room.Bought.Add(kind);
                        GameSfx.Play(Sfx.DungeonPurchase);
                        RefreshDeckAndDungeon();
                        SetDungeonFeedback($"已购买 {card.DisplayName}", false);
                    }, 50);
                }
                AddDungeonInjector(price: 100);
                AddRoomChoice("回复 30% 生命", "", () => HealInRoom(.30f, 100), 100);
                if (floorsWithShopCardRemoval.Contains(dungeonFloor))
                    interactionCaption = "本层商人删牌次数已用完";
                else
                    AddRemoveChoice(ShopCardRemovalPrice);
                return;
            }

            if (room.Kind == DungeonRoomKind.Boss)
            {
                interactionTitle = "击败首领";
                string next = dungeonFloor + 1 == dungeonFloors.Length ? "结束探索" : "前往下一层";
                string detail = dungeonFloor + 1 == dungeonFloors.Length ? "" : "回复 50% 生命";
                if (room.Cleared && !room.Claimed) AddDungeonInjector();
                AddRoomChoice(next, detail, NextDungeonFloor);
                return;
            }

            interactionSecondary = ("略过", () =>
            {
                GameSfx.Play(Sfx.CardCancel, .70f);
                ClaimDungeonRoom();
            });
            if (room.Kind == DungeonRoomKind.Maintenance)
            {
                interactionTitle = "维护站";
                interactionCaption = "本房间可维护一次";
                AddRoomChoice("回复 30% 生命", "", () => HealInRoom(.30f, 0));
                AddRemoveChoice(0);
            }
            else if (room.Kind == DungeonRoomKind.Event)
            {
                interactionTitle = "事件";
                switch (room.Event)
                {
                    case 0:
                        AddRoomChoice("接入病毒代码", "伤害 +20% / 格挡上限 −10%", () =>
                        {
                            batch.Vitals.DungeonDamageMultiplier *= 1.2f;
                            batch.Vitals.SetMaxBlock(Mathf.FloorToInt(batch.Vitals.MaxBlock * .9f));
                            GameSfx.Play(Sfx.DungeonReward);
                            ClaimDungeonRoom();
                        });
                        break;
                    case 1:
                        interactionTitle = "备份";
                        interactionCaption = "取得备份并加入本局牌组";
                        foreach (var choice in OfferRoomCards(room))
                        {
                            var card = DefinitionFor(choice);
                            if (card != null)
                                AddCardChoice(card, () => ClaimDungeonCard(card));
                        }
                        break;
                    default:
                        interactionTitle = "系统升级";
                        AddRoomChoice("回复 50% 生命", "", () => HealInRoom(.50f, 0));
                        AddRemoveChoice(0);
                        break;
                }
            }
            else
            {
                interactionTitle = "选择奖励";
                interactionCaption = "选择一项奖励";
                foreach (var kind in OfferRoomCards(room))
                {
                    var card = DefinitionFor(kind);
                    if (card == null) continue;
                    AddCardChoice(card, () => ClaimDungeonCard(card));
                }
                if (room.Cleared && room.Kind == DungeonRoomKind.Elite)
                    AddDungeonInjector();
                if (interactionChoices.All(choice => choice.Card != null))
                {
                    interactionTitle = "选择一张奖励卡牌";
                    interactionCaption = "选择后加入本局牌组";
                    interactionKeyboardHint = "";
                }
            }
        }

        private void HealInRoom(float fraction, int price)
        {
            if (batch.Vitals.Health >= batch.Vitals.MaxHealth)
            {
                SetDungeonFeedback("生命已满");
                return;
            }
            fragments -= price;
            batch.Vitals.HealPercent(fraction);
            if (price == 0) ClaimDungeonRoom();
            else
            {
                RefreshDungeonUi();
                SetDungeonFeedback("已回复生命", false);
            }
        }

        private void AddDungeonInjector(int price = 0)
        {
            var injector = dungeonDebugger;
            if (injector == null || !injector.IsConfigured || DebuggersManager.Owns(injector)) return;
            string detail = price > 0 ? "数据注入器" : "数据注入器 · 免费领取";
            AddRoomChoice(injector.DisplayName, detail, () =>
            {
                if (!DebuggersManager.TryAcquire(injector)) return;
                if (price == 0)
                {
                    ClaimDungeonRoom();
                    ShowTerminalReceipt($"已领取 {injector.DisplayName}");
                    return;
                }
                fragments -= price;
                RefreshDungeonUi();
                SetDungeonFeedback($"已购买 {injector.DisplayName}", false);
            }, price, injector.Icon);
        }

        private void AddRemoveChoice(int price)
        {
            if (batch.DeckKinds().Take(2).Count() < 2) return;
            AddRoomChoice("删牌", "从本局牌组移除一张卡牌", () =>
            {
                GameSfx.Play(Sfx.PileOpen, .65f);
                removingDungeonCards = true;
                terminalFirstCard = 0;
                RenderDungeonInteraction();
            }, price);
        }

        private void BuildRemoveChoices()
        {
            bool paid = dungeonRoom.Kind == DungeonRoomKind.Shop;
            int floor = dungeonFloor;
            int price = paid ? ShopCardRemovalPrice : 0;
            interactionTitle = "选择要删除的牌";
            interactionCaption = "从本局牌组移除一张卡牌";
            interactionSecondary = ("返回", BackFromDungeonRemoval);
            var deck = batch.DeckKinds().ToArray();
            if (deck.Length < 2) return;
            foreach (var group in deck.GroupBy(kind => kind).OrderBy(group => group.Key))
            {
                var card = DefinitionFor(group.Key);
                if (card == null) continue;
                AddCardChoice(card, () =>
                {
                    if (paid && (dungeonFloor != floor || floorsWithShopCardRemoval.Contains(floor) || fragments < price)) return;
                    if (!batch.RemoveFromDeck(card.Kind)) return;
                    if (paid)
                    {
                        fragments -= price;
                        floorsWithShopCardRemoval.Add(floor);
                    }
                    GameSfx.Play(Sfx.DungeonCardRemoved);
                    if (dashPlans.RemoveAll(plan => !batch.Queue.Contains(plan.Card)) > 0) RebuildDashPath(0);
                    if (paid)
                    {
                        removingDungeonCards = false;
                        terminalFirstCard = 0;
                        RefreshDeckAndDungeon();
                        SetDungeonFeedback($"已删除 {card.DisplayName}", false);
                    }
                    else ClaimDungeonRoom();
                }, price, group.Count());
            }
        }

        private void BackFromDungeonRemoval()
        {
            GameSfx.Play(Sfx.PileClose, .65f);
            removingDungeonCards = false;
            terminalFirstCard = 0;
            RenderDungeonInteraction();
        }

        private void ClaimDungeonCard(CardDefinition card)
        {
            if (!batch.AddToDeck(card)) return;
            GameSfx.Play(Sfx.DungeonReward);
            ClaimDungeonRoom();
            ShowTerminalReceipt($"已领取 {card.DisplayName} · 已加入牌组");
        }

        private void ClaimDungeonRoom()
        {
            dungeonRoom.Claimed = true;
            RefreshDeckAndDungeon();
        }

        private void RefreshDeckAndDungeon()
        {
            foreach (var entry in views.Where(entry => !batch.Hand.Contains(entry.Key)).ToArray())
            {
                entry.Value.gameObject.SetActive(false);
                Destroy(entry.Value.gameObject);
                views.Remove(entry.Key);
            }
            Refresh();
            RefreshDungeonUi();
        }
    }
}
