using System;
using System.Linq;
using Fighter403.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fighter403.UI
{
    public sealed partial class CardHandController
    {
        private const float TerminalInset = 24f;
        private const float TerminalHeader = 76f;
        private const float TerminalFooter = 68f;
        private const float TerminalShelf = 266f;
        private const float TerminalDetailsHeight = 152f;
        private const float TerminalServiceStep = 54f;
        private const int SuccessColumns = 7, SuccessRows = 6, SuccessFrames = 42;
        private static readonly Color TerminalEdge = new Color32(57, 87, 111, 255);
        private static readonly Color TerminalShell = new Color32(24, 38, 56, 255);
        private static readonly Color TerminalWell = new Color32(12, 22, 36, 255);
        private static readonly Color TerminalRaised = new Color32(33, 52, 73, 255);
        private static readonly Color TerminalFocus = new Color32(83, 207, 222, 255);
        private static readonly Color TerminalCredit = new Color32(255, 202, 131, 255);
        private static readonly Color TerminalWarning = new Color32(255, 171, 144, 255);

        private bool terminalCompact;
        private int terminalFirstCard;
        private TMP_FontAsset terminalFont;
        private TMP_Text terminalHint, terminalToastText;
        private RawImage terminalFooterSpark, terminalToastSpark;
        private RectTransform terminalToast;
        private CanvasGroup terminalToastGroup;
        private Texture2D terminalSuccessSheet;
        private float terminalSuccessAt = float.NegativeInfinity;
        private float terminalToastAt = float.NegativeInfinity;

        private bool WantsCompactTerminal => root.rect.width < root.rect.height * 1.05f;
        private bool TerminalHasWallet => !dungeonFinished && dungeonRoom.Kind == DungeonRoomKind.Shop;

        private void RenderDungeonInteraction(CardDefinition keepCard = null, string keepService = null)
        {
            interactionChoices.Clear();
            interactionTiles.Clear();
            interactionTitle = interactionCaption = "";
            interactionKeyboardHint = "方向键选择   ·   Enter 确认";
            interactionSecondary = default;
            if (removingDungeonCards) BuildRemoveChoices();
            else BuildRoomChoices();

            InteractionChoice[] cards = interactionChoices.Where(choice => choice.Card != null).ToArray();
            InteractionChoice[] services = dungeonFinished ? Array.Empty<InteractionChoice>() :
                interactionChoices.Where(choice => choice.Card == null).ToArray();
            terminalCompact = WantsCompactTerminal;
            int pageSize = terminalCompact ? 3 : CardsPerPage;
            int keptIndex = Array.FindIndex(cards, choice => choice.Card == keepCard);
            if (keptIndex >= 0) terminalFirstCard = keptIndex;
            int lastPage = Mathf.Max(0, (cards.Length - 1) / pageSize);
            dungeonCardPage = Mathf.Clamp(terminalFirstCard / pageSize, 0, lastPage);
            terminalFirstCard = dungeonCardPage * pageSize;
            InteractionChoice[] page = cards.Skip(terminalFirstCard).Take(pageSize).ToArray();

            ClearDungeonChildren(dungeonChoices);
            dungeonDetail = null;
            dungeonPageStarted = Time.unscaledTime;
            dungeonChoicesGroup.alpha = 0f;
            terminalSuccessAt = float.NegativeInfinity;
            bool hasCards = cards.Length > 0;
            bool sideBySide = hasCards && services.Length > 0 && !terminalCompact;
            float width = hasCards && !terminalCompact ? 792f : 520f;
            float innerWidth = width - TerminalInset * 2f;
            float bodyTop = TerminalHeader + (hasCards ? TerminalShelf : TerminalHasWallet && !removingDungeonCards ? 40f : 12f);
            float servicesHeight = services.Length * TerminalServiceStep;
            float bodyHeight = sideBySide ? Mathf.Max(TerminalDetailsHeight, servicesHeight) :
                (hasCards ? TerminalDetailsHeight + (services.Length > 0 ? 12f : 0f) : 0f) + servicesHeight;
            float height = bodyTop + Mathf.Max(bodyHeight, 48f) + TerminalFooter + 12f;
            dungeonPanel.sizeDelta = dungeonChoices.sizeDelta = new Vector2(width, height);
            LayoutTerminalPanel();
            BuildTerminalFrame(width, height);

            if (hasCards)
            {
                BuildTerminalShelf(page, lastPage, innerWidth, pageSize);
                float detailsWidth = sideBySide ? 366f : innerWidth;
                BuildDungeonCardDetails(TerminalInset, bodyTop, detailsWidth);
            }
            else if (TerminalHasWallet && !removingDungeonCards)
            {
                var empty = TerminalLabel("暂无可购买卡牌", dungeonChoices, TerminalInset, TerminalHeader + 5, innerWidth, 28, 16);
                empty.color = DungeonMuted;
            }

            float servicesLeft = sideBySide ? TerminalInset + 380f : TerminalInset;
            float servicesWidth = width - TerminalInset - servicesLeft;
            float servicesTop = bodyTop + (hasCards && !sideBySide ? TerminalDetailsHeight + 12f : 0f);
            for (int i = 0; i < services.Length; i++)
                BuildInteractionService(services[i], servicesLeft, servicesTop + i * TerminalServiceStep, servicesWidth);

            BuildInteractionFooter(width);
            var selected = dungeonFinished ? interactionChoices[0] :
                page.FirstOrDefault(choice => choice.Card == keepCard) ??
                services.FirstOrDefault(choice => choice.Title == keepService) ??
                page.FirstOrDefault() ?? services.FirstOrDefault();
            SelectDungeonChoice(selected);
        }

        private void BuildTerminalFrame(float width, float height)
        {
            TerminalPlate("Terminal shadow", dungeonChoices, 3, 6, width, height,
                new Color32(5, 10, 20, 210), new Color32(5, 10, 20, 210));
            TerminalPlate("Terminal shell", dungeonChoices, 0, 0, width, height, TerminalShell, TerminalEdge);
            TerminalPlate("Terminal display", dungeonChoices, 10, TerminalHeader - 2, width - 20,
                height - TerminalHeader - TerminalFooter + 4, TerminalWell, new Color32(30, 52, 73, 255));
            TerminalSolid("Header latch", dungeonChoices, TerminalInset, 0, 46, 4, TerminalFocus);
            TerminalSolid("Frame joint", dungeonChoices, width - 66, height - 4, 42, 4, TerminalEdge);
            float headingWidth = width - TerminalInset * 2f - (TerminalHasWallet ? 132f : 0f);
            bool hasCaption = interactionCaption.Length > 0;
            var heading = TerminalLabel(interactionTitle, dungeonChoices, TerminalInset, hasCaption ? 11 : 20,
                headingWidth, 37, 27);
            heading.fontStyle = FontStyles.Bold;
            if (hasCaption)
            {
                var caption = TerminalLabel(interactionCaption, dungeonChoices, TerminalInset, 45, headingWidth, 22, 15);
                caption.color = DungeonMuted;
            }
            if (TerminalHasWallet)
            {
                var wallet = TerminalPlate("Fragment balance", dungeonChoices, width - TerminalInset - 120, 20,
                    120, 36, TerminalWell, TerminalEdge);
                TerminalFragment(wallet.rectTransform, 12, 11, TerminalCredit);
                var balance = TerminalLabel($"碎片 {fragments}", wallet.rectTransform, 32, 0, 80, 36, 17);
                balance.color = TerminalCredit;
                balance.fontStyle = FontStyles.Bold;
            }
        }

        private void BuildTerminalShelf(InteractionChoice[] page, int lastPage, float innerWidth, int pageSize)
        {
            const float shelfTop = TerminalHeader + 16f;
            var rail = TerminalPlate("Card shelf", dungeonChoices, TerminalInset, shelfTop, innerWidth, 234,
                new Color32(16, 28, 44, 255), new Color32(36, 58, 81, 255));
            float pitch = terminalCompact ? 150f : 170f;
            if (lastPage > 0) BuildDungeonPagination(rail.rectTransform, innerWidth, lastPage, pageSize);

            for (int i = 0; i < page.Length; i++)
            {
                float x = TerminalInset + innerWidth / 2 + (i - (page.Length - 1) * .5f) * pitch;
                BuildInteractionCard(page[i], x, shelfTop + 34, pitch - 10, i * .035f);
            }
        }

        private void BuildDungeonPagination(RectTransform rail, float width, int lastPage, int pageSize)
        {
            TerminalButton("Previous card page", "‹", rail, width - 112, 4, 26, 26,
                () => ChangeDungeonCardPage(-1, pageSize)).interactable = dungeonCardPage > 0;
            TerminalButton("Next card page", "›", rail, width - 34, 4, 26, 26,
                () => ChangeDungeonCardPage(1, pageSize)).interactable = dungeonCardPage < lastPage;
            var count = TerminalLabel($"{dungeonCardPage + 1} / {lastPage + 1}", rail, width - 85, 4, 50, 26, 13);
            count.alignment = TextAlignmentOptions.MidlineGeoAligned;
            count.color = DungeonMuted;
        }

        private void ChangeDungeonCardPage(int direction, int pageSize)
        {
            terminalFirstCard = (dungeonCardPage + direction) * pageSize;
            GameSfx.Play(Sfx.QueueMove, .60f);
            RenderDungeonInteraction();
        }

        private void BuildInteractionCard(InteractionChoice choice, float centerX, float top, float width, float entryDelay)
        {
            const float height = 196f;
            var visual = choice.Card.GetComponent<CardHoverVisual>();
            Vector2 cardSize = visual.Face.sizeDelta;
            float scale = Mathf.Min((width - 12f) / cardSize.x, 159f / cardSize.y);
            var slot = TerminalPlate("Choose " + choice.Title, dungeonChoices, centerX - width / 2, top, width, height,
                TerminalWell, TerminalEdge, interactive: true);
            AddTerminalButton(slot, () => SelectDungeonChoice(choice, playSound: true));
            string tag = choice.Price > 0 ? $"碎片 {choice.Price}" : choice.Detail;
            if (choice.Price > 0 && choice.Detail.Length > 0) tag = $"{tag}  ·  {choice.Detail}";
            float baseY = tag.Length > 0 ? 17f : 0f;
            var cardVisual = Box("Card motion", slot.rectTransform, DungeonMiddle, new Vector2(0, baseY), cardSize);
            cardVisual.localScale = Vector3.one * scale;
            var group = cardVisual.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            visual.CreateDisplayFace(cardVisual, choice.Card);

            var marker = TerminalSolid("Card focus", slot.rectTransform, width / 2 - 16, 0, 32, 4, TerminalFocus);
            if (tag.Length > 0)
            {
                TerminalSolid("Card status divider", slot.rectTransform, 7, 161, width - 14, 2, TerminalEdge);
                var text = TerminalLabel(tag, slot.rectTransform, 6, 166, width - 12, 26, 15);
                text.alignment = TextAlignmentOptions.MidlineGeoAligned;
                text.color = choice.Price > 0 ? PriceColor(choice.Price) : DungeonMuted;
            }
            interactionTiles.Add(new InteractionTile
            {
                Choice = choice, Plate = slot, Marker = marker, CardVisual = cardVisual, Group = group,
                EntryDelay = entryDelay, BaseY = baseY
            });
        }

        private void BuildDungeonCardDetails(float left, float top, float width)
        {
            var panel = TerminalPlate("Selected card details", dungeonChoices, left, top, width, TerminalDetailsHeight,
                TerminalWell, TerminalEdge);
            dungeonDetail = panel.rectTransform;
            TerminalSolid("Detail inset line", dungeonDetail, 14, 55, width - 28, 2, TerminalEdge);
            dungeonDetailTitle = TerminalLabel("", dungeonDetail, 14, 7, width - 28, 24, 20);
            dungeonDetailTitle.fontStyle = FontStyles.Bold;
            dungeonDetailMeta = TerminalLabel("", dungeonDetail, 14, 31, width - 28, 20, 15);
            dungeonDetailMeta.color = TerminalFocus;
            dungeonDetailBody = TerminalLabel("", dungeonDetail, 14, 64, width - 28, 80, 16);
            dungeonDetailBody.alignment = TextAlignmentOptions.TopLeft;
            dungeonDetailBody.color = DungeonMuted;
            dungeonDetailBody.textWrappingMode = TextWrappingModes.Normal;
            dungeonDetailBody.lineSpacing = 2f;
        }

        private void SetDungeonCardDetails(InteractionChoice choice)
        {
            if (dungeonDetail == null) return;
            CardDefinition card = choice?.Card;
            dungeonDetailTitle.text = choice?.Title ?? "";
            if (card == null)
            {
                dungeonDetailMeta.text = choice?.Price > 0 ? $"碎片 {choice.Price}" : "";
                dungeonDetailMeta.color = choice?.Price > 0 ? PriceColor(choice.Price) : TerminalFocus;
                dungeonDetailBody.text = choice?.Detail ?? "";
                return;
            }

            string cardType = card.Class switch
            {
                CardClass.Defense => "防御",
                CardClass.Ability => "能力",
                _ => "攻击"
            };
            dungeonDetailMeta.text = $"{ClassSymbol(card.Class)}  {cardType}    /    {card.EnergyCost} 能量";
            dungeonDetailMeta.color = TerminalFocus;
            dungeonDetailBody.text = card.Describe();
        }

        private void BuildInteractionService(InteractionChoice choice, float left, float top, float width)
        {
            var plate = TerminalPlate("Choose " + choice.Title, dungeonChoices, left, top, width, 48,
                TerminalRaised, TerminalEdge, interactive: true);
            AddTerminalButton(plate, () => SelectDungeonChoice(choice, playSound: true));
            RectTransform row = plate.rectTransform;
            var marker = TerminalSolid("Service focus", row, 0, 12, 4, 24, TerminalFocus);
            float inset = 16f;
            if (choice.Icon != null)
            {
                var icon = Paint(Box("Reward icon", row, DungeonTopLeft, new Vector2(29, -24), new Vector2(26, 26)),
                    Color.white, false, choice.Icon);
                icon.preserveAspect = true;
                inset = 49f;
            }
            bool paid = choice.Price > 0;
            float contentWidth = width - inset - (paid ? 85f : 16f);
            bool hasDetail = choice.Detail.Length > 0;
            var title = TerminalLabel(choice.Title, row, inset, hasDetail ? 3 : 0, contentWidth, hasDetail ? 23 : 48, 17);
            title.fontStyle = FontStyles.Bold;
            if (hasDetail)
            {
                var info = TerminalLabel(choice.Detail, row, inset, 25, contentWidth, 19, 14);
                info.color = DungeonMuted;
            }
            if (paid)
            {
                Color priceColor = PriceColor(choice.Price);
                var tag = TerminalPlate("Service price", row, width - 74, 11, 62, 26, TerminalWell, TerminalEdge);
                TerminalFragment(tag.rectTransform, 8, 7, priceColor);
                var price = TerminalLabel(choice.Price.ToString(), tag.rectTransform, 23, 0, 32, 26, 16);
                price.alignment = TextAlignmentOptions.MidlineRight;
                price.color = priceColor;
            }
            interactionTiles.Add(new InteractionTile { Choice = choice, Plate = plate, Marker = marker });
        }

        private void BuildInteractionFooter(float width)
        {
            float top = dungeonPanel.sizeDelta.y - TerminalFooter;
            float confirmLeft = width - TerminalInset - 160;
            dungeonConfirm = TerminalButton("Confirm choice", "", dungeonChoices, confirmLeft, top + 9, 160, 44,
                ConfirmDungeonChoice, primary: true);
            dungeonConfirmLabel = dungeonConfirm.GetComponentInChildren<TMP_Text>();
            bool hasSecondary = interactionSecondary.Apply != null;
            float secondaryWidth = dungeonFinished ? 120f : 86f;
            float actionLeft = confirmLeft - (hasSecondary ? secondaryWidth + 10f : 0f);
            if (hasSecondary)
                TerminalButton("Secondary action", interactionSecondary.Label, dungeonChoices, actionLeft,
                    top + 9, secondaryWidth, 44, () => interactionSecondary.Apply());

            float statusWidth = actionLeft - TerminalInset - 14f;
            terminalHint = TerminalLabel(interactionKeyboardHint, dungeonChoices, TerminalInset, top + 9, statusWidth, 44, 14);
            terminalHint.color = DungeonMuted;
            dungeonFeedback = TerminalLabel("", dungeonChoices, TerminalInset, top + 9, statusWidth, 44, 16);
            terminalFooterSpark = CreateTerminalSpark(dungeonChoices, new Vector2(0, 0),
                new Vector2(actionLeft - (hasSecondary ? 7f : 12f), 32));
        }

        private Button TerminalButton(string name, string title, RectTransform parent, float left, float top,
            float width, float height, UnityEngine.Events.UnityAction action, bool primary = false)
        {
            var plate = TerminalPlate(name, parent, left, top, width, height,
                primary ? TerminalFocus : TerminalRaised, primary ? new Color32(160, 239, 240, 255) : TerminalEdge,
                interactive: true);
            var button = AddTerminalButton(plate, action);
            var label = TerminalLabel(title, plate.rectTransform, 6, 0, width - 12, height, 16);
            label.alignment = TextAlignmentOptions.MidlineGeoAligned;
            label.color = primary ? TerminalWell : Paper;
            label.fontStyle = FontStyles.Bold;
            return button;
        }

        private static Button AddTerminalButton(DungeonTerminalPlate plate, UnityEngine.Events.UnityAction action)
        {
            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.colorMultiplier = 1.12f;
            colors.fadeDuration = .06f;
            colors.normalColor = new Color(.89f, .89f, .89f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(.72f, .79f, .86f);
            colors.disabledColor = new Color(.43f, .51f, .59f, .82f);
            button.colors = colors;
            button.onClick.AddListener(action);
            return button;
        }

        private TMP_Text TerminalLabel(string value, RectTransform parent, float left, float top,
            float width, float height, int size)
        {
            var rect = Box(value.Length > 0 ? value : "Text", parent, DungeonTopLeft,
                new Vector2(left + width / 2, -top - height / 2), new Vector2(width, height));
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = terminalFont;
            label.color = Paper;
            label.raycastTarget = false;
            label.richText = false;
            label.fontSize = size;
            label.fontSizeMin = size - 3;
            label.fontSizeMax = size;
            label.enableAutoSizing = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.text = value;
            return label;
        }

        private static DungeonTerminalPlate TerminalPlate(string name, RectTransform parent, float left, float top,
            float width, float height, Color fill, Color edge, bool interactive = false)
        {
            var rect = Box(name, parent, DungeonTopLeft, new Vector2(left + width / 2, -top - height / 2),
                new Vector2(width, height));
            rect.gameObject.AddComponent<CanvasRenderer>();
            var plate = rect.gameObject.AddComponent<DungeonTerminalPlate>();
            plate.color = fill;
            plate.Edge = edge;
            plate.raycastTarget = interactive;
            return plate;
        }

        private static Image TerminalSolid(string name, RectTransform parent, float left, float top,
            float width, float height, Color color) =>
            Paint(Box(name, parent, DungeonTopLeft, new Vector2(left + width / 2, -top - height / 2),
                new Vector2(width, height)), color, false);

        private static void TerminalFragment(RectTransform parent, float left, float top, Color color)
        {
            TerminalSolid("Fragment core", parent, left + 3, top, 6, 12, color);
            TerminalSolid("Fragment shoulders", parent, left, top + 3, 12, 6, color);
            TerminalSolid("Fragment glint", parent, left + 3, top + 3, 3, 3, Paper);
        }

        private Color PriceColor(int price) => fragments >= price ? TerminalCredit : TerminalWarning;

        private void LayoutTerminalPanel()
        {
            Vector2 size = dungeonPanel.sizeDelta;
            float canvasScale = Mathf.Max(.01f, canvas.scaleFactor);
            Vector2 viewport = root.rect.size * canvasScale;
            float fit = Mathf.Min((viewport.x - 28f) / size.x, (viewport.y - 40f) / size.y);
            float preferred = Mathf.Max(1f, viewport.y / 640f);
            dungeonPanel.localScale = Vector3.one * (Mathf.Max(.05f, Mathf.Min(fit, preferred)) / canvasScale);
        }

        private string GetTerminalAction(InteractionChoice choice)
        {
            if (choice == null) return "确认";
            string action = "确认";
            if (choice.Card != null)
                action = removingDungeonCards ? "确认删牌" : choice.Price > 0 ? "购买卡牌" : "领取卡牌";
            else if (choice.Title == "删牌") action = "选择删牌";
            else if (choice.Icon != null) action = choice.Price > 0 ? "购买注入器" : "领取注入器";
            else if (choice.Price == 0 && interactionChoices.Count == 1) action = choice.Title;
            return choice.Price > 0 ? $"{action} · {choice.Price}" : action;
        }

        private void BuildTerminalToast()
        {
            terminalFont = cardPrefabs[0].GetComponentInChildren<TMP_Text>(true)?.font ?? TMP_Settings.defaultFontAsset;
            terminalSuccessSheet = Resources.Load<Texture2D>("Fighter403TerminalSuccess");
            terminalToast = Box("Dungeon reward receipt", root, DungeonTop, new Vector2(0, -42), new Vector2(352, 48));
            terminalToastGroup = terminalToast.gameObject.AddComponent<CanvasGroup>();
            terminalToastGroup.blocksRaycasts = terminalToastGroup.interactable = false;
            TerminalPlate("Receipt plate", terminalToast, 0, 0, 352, 48, TerminalShell, TerminalFocus);
            TerminalSolid("Receipt light", terminalToast, 0, 11, 4, 26, TerminalFocus);
            terminalToastText = TerminalLabel("", terminalToast, 50, 0, 286, 48, 18);
            terminalToastText.fontStyle = FontStyles.Bold;
            terminalToastSpark = CreateTerminalSpark(terminalToast, new Vector2(0, .5f), new Vector2(25, -6));
            terminalToast.gameObject.SetActive(false);
        }

        private RawImage CreateTerminalSpark(RectTransform parent, Vector2 anchor, Vector2 position)
        {
            if (terminalSuccessSheet == null) return null;
            var spark = Box("PixelFX collect burst", parent, anchor, position, new Vector2(90, 86));
            var image = spark.gameObject.AddComponent<RawImage>();
            image.texture = terminalSuccessSheet;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private void ShowTerminalReceipt(string message)
        {
            terminalToastText.text = message;
            terminalToastAt = Time.unscaledTime;
            terminalToast.SetAsLastSibling();
            terminalToast.gameObject.SetActive(true);
            UpdateTerminalToast();
        }

        private void UpdateTerminalToast()
        {
            if (!terminalToast.gameObject.activeSelf) return;
            float elapsed = Time.unscaledTime - terminalToastAt;
            if (elapsed >= 1.8f)
            {
                terminalToast.gameObject.SetActive(false);
                return;
            }
            float canvasScale = Mathf.Max(.01f, canvas.scaleFactor);
            float pixelWidth = root.rect.width * canvasScale;
            float scale = Mathf.Max(.05f, Mathf.Min(1f, (pixelWidth - 24f) / terminalToast.sizeDelta.x)) / canvasScale;
            terminalToast.localScale = Vector3.one * Mathf.Max(.1f, scale);
            terminalToast.anchoredPosition = Vector2.down * (33f / canvasScale + 24f * scale);
            terminalToastGroup.alpha = Mathf.Min(DungeonEaseOut(elapsed / .12f), (1.8f - elapsed) / .24f);
            UpdateTerminalSpark(terminalToastSpark, elapsed);
        }

        private static void UpdateTerminalSpark(RawImage spark, float elapsed)
        {
            if (spark == null) return;
            const float playbackFps = 72f;
            spark.enabled = elapsed >= 0f && elapsed < SuccessFrames / playbackFps;
            if (!spark.enabled) return;
            int frame = Mathf.Min((int)(elapsed * playbackFps), SuccessFrames - 1);
            const float columns = 1f / SuccessColumns;
            const float rows = 1f / SuccessRows;
            spark.uvRect = new Rect(frame % SuccessColumns * columns,
                1f - (frame / SuccessColumns + 1) * rows, columns, rows);
        }
    }

    internal sealed class DungeonTerminalPlate : MaskableGraphic
    {
        private Color edge;
        public Color Edge
        {
            get => edge;
            set
            {
                if (edge == value) return;
                edge = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float left = rect.xMin, right = rect.xMax, bottom = rect.yMin, top = rect.yMax;
            if (rect.width < 12 || rect.height < 12) return;

            AddBand(mesh, left + 6, right - 6, top - 2, top, edge);
            AddBand(mesh, left + 2, right - 2, top - 6, top - 2, edge);
            AddBand(mesh, left, right, bottom + 6, top - 6, edge);
            AddBand(mesh, left + 2, right - 2, bottom + 2, bottom + 6, edge);
            AddBand(mesh, left + 6, right - 6, bottom, bottom + 2, edge);

            AddBand(mesh, left + 6, right - 6, top - 6, top - 2, Color.Lerp(color, edge, .32f));
            AddBand(mesh, left + 2, right - 2, bottom + 6, top - 6, color);
            AddBand(mesh, left + 6, right - 6, bottom + 2, bottom + 6, color * new Color(.73f, .80f, .85f, 1f));
        }

        private static void AddBand(VertexHelper mesh, float left, float right, float bottom, float top, Color32 tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(left, bottom), tint, Vector2.zero);
            mesh.AddVert(new Vector3(left, top), tint, Vector2.zero);
            mesh.AddVert(new Vector3(right, top), tint, Vector2.zero);
            mesh.AddVert(new Vector3(right, bottom), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
