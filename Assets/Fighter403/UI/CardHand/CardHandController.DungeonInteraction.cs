using System;
using System.Collections.Generic;
using System.Linq;
using Fighter403.Audio;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fighter403.UI
{
    public sealed partial class CardHandController
    {
        private const int CardsPerPage = 4;
        private const float ActorInteractionRadius = 1.8f;
        private const float ActorPromptDiameter = 34f;
        private const float PromptKeyArea = 42f, PromptRightPadding = 16f;
        private const int RewardSparkColumns = 4, RewardSparkFrames = 16, RewardSparkFps = 30;
        private static readonly Vector2 DungeonMiddle = Vector2.one * .5f;
        private static readonly Vector2 DungeonTop = new(.5f, 1f);
        private static readonly Vector2 DungeonTopLeft = new(0, 1);
        private static readonly Color DungeonMuted = new Color32(175, 197, 211, 255);
        private static readonly Color DungeonLine = new Color32(60, 89, 110, 255);
        private static readonly Color DungeonSurface = new Color32(14, 23, 36, 255);
        private static readonly Color DungeonChoiceFill = new Color32(28, 43, 60, 255);
        private static readonly Color DungeonInset = new Color32(9, 17, 29, 255);
        private static readonly Color DungeonHeader = new Color32(23, 37, 52, 255);
        private static readonly Color SelectionCyan = new Color32(95, 227, 249, 255);

        private sealed class InteractionChoice
        {
            public readonly string Title, Detail;
            public readonly Action Apply;
            public readonly CardDefinition Card;
            public readonly int Price;
            public readonly Sprite Icon;

            public InteractionChoice(string title, string detail, Action apply, CardDefinition card, int price, Sprite icon = null)
            {
                Title = title;
                Detail = detail;
                Apply = apply;
                Card = card;
                Price = price;
                Icon = icon;
            }
        }

        private sealed class InteractionTile
        {
            public InteractionChoice Choice;
            public Graphic Border;
            public RectTransform CardVisual;
            public CanvasGroup Group;
            public RawImage Spark;
            public float SparkStarted = float.NegativeInfinity;
            public float Lift, EntryDelay;
        }

        private readonly List<InteractionChoice> interactionChoices = new();
        private readonly List<InteractionTile> interactionTiles = new();
        private RectTransform dungeonPrompt, actorPrompt, dungeonShade, dungeonPanel, dungeonChoices, dungeonDetail;
        private CanvasGroup dungeonPromptGroup, dungeonShadeGroup, dungeonChoicesGroup;
        private Text dungeonPromptLabel, dungeonConfirmLabel, dungeonFeedback;
        private Text dungeonDetailTitle, dungeonDetailMeta, dungeonDetailBody;
        private Button dungeonConfirm;
        private Texture2D rewardSpark;
        private DungeonRoom presentedRoom;
        private bool presentedFinished, dungeonInteractionAvailable, dungeonInteractionPending, removingDungeonCards;
        private int dungeonCardPage;
        private InteractionChoice dungeonSelected;
        private string interactionTitle, interactionCaption;
        private (string Label, Action Apply) interactionSecondary;
        private float dungeonPanelProgress, dungeonPromptProgress, dungeonPageStarted, dungeonFeedbackStarted;

        private bool DungeonInteractionOpen { get; set; }
        private bool UsesActorPrompt => !dungeonFinished && dungeonRoom != null &&
            DungeonRoomActor.HasProximityInteraction(dungeonRoom.Kind);

        private void BuildDungeonInteraction()
        {
            var prompt = DungeonButton("Room prompt", "", root, DungeonMiddle, Vector2.zero,
                new Vector2(144, 36), OpenDungeonInteraction);
            dungeonPrompt = (RectTransform)prompt.transform;
            dungeonPromptGroup = dungeonPrompt.gameObject.AddComponent<CanvasGroup>();
            var promptPlate = (Image)prompt.targetGraphic;
            promptPlate.sprite = Resources.Load<Sprite>("Fighter403RoomPrompt");
            promptPlate.type = Image.Type.Sliced;
            promptPlate.color = Color.white;
            dungeonPromptLabel = dungeonPrompt.GetComponentInChildren<Text>();
            dungeonPromptLabel.color = new Color32(224, 239, 244, 255);
            dungeonPromptLabel.alignment = TextAnchor.MiddleLeft;
            dungeonPromptLabel.resizeTextForBestFit = false;
            dungeonPromptLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            var promptText = dungeonPromptLabel.rectTransform;
            promptText.anchorMin = Vector2.zero;
            promptText.anchorMax = Vector2.one;
            promptText.offsetMin = new Vector2(PromptKeyArea, 1);
            promptText.offsetMax = new Vector2(-PromptRightPadding, 1);
            dungeonPrompt.gameObject.SetActive(false);

            actorPrompt = Box("Nearby room actor", root, DungeonMiddle, Vector2.zero,
                Vector2.one * ActorPromptDiameter);
            actorPrompt.gameObject.AddComponent<CanvasRenderer>();
            var disc = actorPrompt.gameObject.AddComponent<RoomInteractionCircle>();
            disc.color = Color.white;
            var shadow = actorPrompt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(7, 14, 23, 198);
            shadow.effectDistance = new Vector2(0, -2);
            var actorButton = actorPrompt.gameObject.AddComponent<Button>();
            actorButton.targetGraphic = disc;
            actorButton.transition = Selectable.Transition.None;
            actorButton.navigation = new Navigation { mode = Navigation.Mode.None };
            actorButton.onClick.AddListener(OpenDungeonInteraction);
            var actorKey = Label("Interact key", actorPrompt, DungeonMiddle, Vector2.zero,
                Vector2.one * ActorPromptDiameter, 18);
            actorKey.text = "E";
            actorKey.color = DungeonInset;
            actorKey.fontStyle = FontStyle.Bold;
            actorKey.resizeTextForBestFit = false;
            actorKey.gameObject.AddComponent<RoomInteractionGlyphCenter>();
            actorPrompt.gameObject.SetActive(false);

            rewardSpark = Resources.Load<Texture2D>("Fighter403RewardSpark");
            dungeonShade = BuildDungeonShade("Dungeon interaction overlay");
            dungeonShadeGroup = dungeonShade.gameObject.AddComponent<CanvasGroup>();
            dungeonPanel = Box("Dungeon interaction", dungeonShade, DungeonMiddle, Vector2.zero, Vector2.zero);
            dungeonChoices = Box("Interaction content", dungeonPanel, DungeonMiddle, Vector2.zero, Vector2.zero);
            dungeonChoicesGroup = dungeonChoices.gameObject.AddComponent<CanvasGroup>();
            dungeonShade.gameObject.SetActive(false);
        }

        private RectTransform BuildDungeonShade(string name)
        {
            var shade = Box(name, root, Vector2.zero, Vector2.zero, Vector2.zero);
            shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            Paint(shade, new Color32(5, 10, 19, 181), true);
            return shade;
        }

        private string DungeonInteractionLabel()
        {
            if (dungeonRoom == null) return null;
            if (dungeonFinished) return "查看探索结果";
            if (!dungeonRoom.Cleared) return null;
            if (dungeonRoom.Kind == DungeonRoomKind.Shop) return "打开商店";
            if (dungeonRoom.Kind == DungeonRoomKind.Boss) return "查看首领奖励";
            if (dungeonRoom.Claimed) return null;
            return dungeonRoom.Kind switch
            {
                DungeonRoomKind.Start => null,
                DungeonRoomKind.Maintenance => "使用维护站",
                DungeonRoomKind.Vault => "开启宝箱",
                DungeonRoomKind.Event => "查看事件",
                _ => "选择奖励"
            };
        }

        private void RefreshDungeonInteraction()
        {
            bool newState = presentedRoom != dungeonRoom || presentedFinished != dungeonFinished;
            bool autoOpen = newState || !dungeonInteractionAvailable || dungeonInteractionPending;
            dungeonInteractionPending = false;
            if (newState)
            {
                presentedRoom = dungeonRoom;
                presentedFinished = dungeonFinished;
                removingDungeonCards = false;
                dungeonCardPage = 0;
                SetDungeonInteractionOpen(false);
                dungeonPanelProgress = 0f;
                dungeonShade.gameObject.SetActive(false);
                actorPrompt.gameObject.SetActive(false);
            }

            string prompt = DungeonInteractionLabel();
            dungeonInteractionAvailable = prompt != null;
            dungeonPrompt.gameObject.SetActive(false);
            if (prompt == null)
            {
                SetDungeonInteractionOpen(false);
                actorPrompt.gameObject.SetActive(false);
                return;
            }

            bool manual = UsesActorPrompt;
            dungeonPromptLabel.text = prompt;
            dungeonPrompt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                PromptKeyArea + Mathf.Ceil(dungeonPromptLabel.preferredWidth) + PromptRightPadding);
            if (DungeonActorAnimating) return;
            if (batch.Busy || DungeonMenuOpen)
            {
                dungeonInteractionPending = !manual && autoOpen;
                if (!dungeonInteractionPending && !DungeonInteractionOpen) ShowDungeonPrompt();
                return;
            }
            if (DungeonInteractionOpen || !manual && autoOpen)
            {
                OpenDungeonInteraction();
                return;
            }
            ShowDungeonPrompt();
        }

        private void SetDungeonInteractionOpen(bool open)
        {
            if (DungeonInteractionOpen == open) return;
            DungeonInteractionOpen = open;
            dungeonShadeGroup.blocksRaycasts = dungeonShadeGroup.interactable = open;
            if (open)
            {
                dungeonShadeGroup.alpha = dungeonPanelProgress;
                dungeonShade.gameObject.SetActive(true);
            }
            UpdatePlanningState();
        }

        private void ShowDungeonPrompt()
        {
            if (UsesActorPrompt)
            {
                UpdateActorPrompt();
                return;
            }
            if (dungeonPrompt.gameObject.activeSelf) return;
            dungeonPromptProgress = 0f;
            dungeonPromptGroup.alpha = 0f;
            dungeonPrompt.gameObject.SetActive(true);
        }

        private void OpenDungeonInteraction()
        {
            if (!CanOpenDungeonInteraction()) return;
            if (!DungeonInteractionOpen && TryAnimateDungeonActor(RevealDungeonInteraction))
            {
                dungeonPrompt.gameObject.SetActive(false);
                actorPrompt.gameObject.SetActive(false);
                return;
            }
            if (!DungeonInteractionOpen && UsesActorPrompt) GameSfx.Play(Sfx.PileOpen, .65f);
            RevealDungeonInteraction();
        }

        private bool CanOpenDungeonInteraction()
        {
            if (batch.Busy || DungeonMenuOpen || DungeonActorAnimating || DungeonInteractionLabel() == null)
                return false;
            return DungeonInteractionOpen || !UsesActorPrompt ||
                (!DungeonRoomTransitioning && dungeonActor != null && dungeonActor.isActiveAndEnabled &&
                    ((Vector2)dungeonActor.transform.position - movement.Position).sqrMagnitude
                        <= ActorInteractionRadius * ActorInteractionRadius);
        }

        private void UpdateActorPrompt()
        {
            bool visible = UsesActorPrompt && !DungeonInteractionOpen && CanOpenDungeonInteraction() && cameraView != null;
            if (visible)
            {
                Vector3 screen = cameraView.WorldToScreenPoint(dungeonActor.PromptPosition);
                var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                bool projected = RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out Vector2 point);
                visible = screen.z > 0f && projected && root.rect.Contains(point);
                if (visible)
                {
                    float scale = Mathf.Min(root.rect.width / 820f, root.rect.height / 720f);
                    actorPrompt.localScale = Vector3.one * scale;
                    actorPrompt.anchoredPosition = point - root.rect.center + Vector2.up * (22f * scale);
                }
            }
            if (actorPrompt.gameObject.activeSelf != visible) actorPrompt.gameObject.SetActive(visible);
        }

        private void RevealDungeonInteraction()
        {
            if (batch.Busy || DungeonMenuOpen || DungeonInteractionLabel() == null) return;
            if (!DungeonInteractionOpen)
            {
                CancelCardDrag();
                queueDrag = null;
                SetPile(-1);
                SetSelectedTarget(null);
                dungeonShade.SetAsLastSibling();
                SetDungeonInteractionOpen(true);
            }
            dungeonPrompt.gameObject.SetActive(false);
            actorPrompt.gameObject.SetActive(false);
            RenderDungeonInteraction();
        }

        private void BackFromDungeonInteraction()
        {
            if (removingDungeonCards)
            {
                BackFromDungeonRemoval();
                return;
            }
            if (dungeonFinished) return;
            GameSfx.Play(Sfx.PileClose, .65f);
            SetDungeonInteractionOpen(false);
            if (DungeonInteractionLabel() != null) ShowDungeonPrompt();
        }

        private bool UpdateDungeonInteractionInput(Keyboard keyboard)
        {
            if (DungeonActorAnimating) return true;
            if (keyboard?.f11Key.wasPressedThisFrame == true)
            {
                ToggleDungeonMenu();
                if (!DungeonMenuOpen && dungeonInteractionPending && !batch.Busy)
                    RefreshDungeonInteraction();
                return true;
            }
            if (DungeonMenuOpen)
            {
                if (keyboard?.escapeKey.wasPressedThisFrame == true) DismissDungeonMenu();
                return true;
            }
            if (dungeonInteractionPending && !batch.Busy) RefreshDungeonInteraction();
            if (keyboard?.tabKey.wasPressedThisFrame == true) ToggleDungeonMap();
            if (DungeonInteractionOpen)
            {
                if (keyboard?.escapeKey.wasPressedThisFrame == true) BackFromDungeonInteraction();
                else if (keyboard?.enterKey.wasPressedThisFrame == true || keyboard?.numpadEnterKey.wasPressedThisFrame == true)
                    ConfirmDungeonChoice();
                else if (keyboard?.leftArrowKey.wasPressedThisFrame == true || keyboard?.upArrowKey.wasPressedThisFrame == true)
                    StepDungeonSelection(-1);
                else if (keyboard?.rightArrowKey.wasPressedThisFrame == true || keyboard?.downArrowKey.wasPressedThisFrame == true)
                    StepDungeonSelection(1);
                return true;
            }
            if (keyboard?.eKey.wasPressedThisFrame == true &&
                (UsesActorPrompt || dungeonPrompt.gameObject.activeSelf) && CanOpenDungeonInteraction())
            {
                OpenDungeonInteraction();
                return true;
            }
            return false;
        }

        private void AddRoomChoice(string title, string detail, Action apply, int price = 0, Sprite icon = null) =>
            interactionChoices.Add(new InteractionChoice(title, detail, apply, null, price, icon));

        private void AddCardChoice(CardDefinition card, Action apply, int price = 0, int copies = 0) =>
            interactionChoices.Add(new InteractionChoice(card.DisplayName, copies > 0 ? $"牌组中 ×{copies}" : "",
                apply, card, price));

        private void RenderDungeonInteraction()
        {
            interactionChoices.Clear();
            interactionTiles.Clear();
            interactionTitle = interactionCaption = "";
            interactionSecondary = default;
            if (removingDungeonCards) BuildRemoveChoices();
            else BuildRoomChoices();

            var cards = interactionChoices.Where(choice => choice.Card != null).ToArray();
            var services = interactionChoices.Where(choice => choice.Card == null).ToArray();
            int lastPage = Mathf.Max(0, (cards.Length - 1) / CardsPerPage);
            dungeonCardPage = Mathf.Clamp(dungeonCardPage, 0, lastPage);
            var page = cards.Skip(dungeonCardPage * CardsPerPage).Take(CardsPerPage).ToArray();

            ClearDungeonChildren(dungeonChoices);
            dungeonDetail = null;
            dungeonPageStarted = Time.unscaledTime;
            dungeonChoicesGroup.alpha = 0f;
            bool hasCards = cards.Length > 0;
            float width = hasCards ? 760f : 496f;
            float rowsTop = hasCards ? 468f : 104f;
            float height = Mathf.Max(hasCards ? 544f : 214f, rowsTop + services.Length * 52f + 81f);
            dungeonPanel.sizeDelta = dungeonChoices.sizeDelta = new Vector2(width, height);
            BuildInteractionFrame(width, height);
            if (lastPage > 0) BuildDungeonPagination(lastPage);

            float pitch = page.Length == CardsPerPage ? 174f : 204f;
            for (int i = 0; i < page.Length; i++)
                BuildInteractionCard(page[i], new Vector2((i - (page.Length - 1) * .5f) * pitch, -206), i * .045f);
            if (hasCards) BuildDungeonCardDetails(width);
            for (int i = 0; i < services.Length; i++)
                BuildInteractionService(services[i], new Vector2(0, -rowsTop - 21f - 52f * i), width - 56f);

            BuildInteractionFooter(width);
            SelectDungeonChoice(page.FirstOrDefault() ?? services.FirstOrDefault());
        }

        private void BuildInteractionFrame(float width, float height)
        {
            Paint(Box("Panel shadow", dungeonChoices, DungeonMiddle, new Vector2(0, -9),
                new Vector2(width + 8, height + 6)), new Color32(4, 9, 17, 176), false);
            Paint(Box("Panel edge", dungeonChoices, DungeonMiddle, Vector2.zero, new Vector2(width, height)),
                DungeonLine, false);
            Paint(Box("Panel fill", dungeonChoices, DungeonMiddle, Vector2.zero, new Vector2(width - 2, height - 2)),
                DungeonSurface, false);
            Paint(Box("Header", dungeonChoices, DungeonTop, new Vector2(0, -46),
                new Vector2(width - 2, 90)), DungeonHeader, false);
            Paint(Box("Header divider", dungeonChoices, DungeonTop, new Vector2(0, -91.5f),
                new Vector2(width - 2, 1)), DungeonLine, false);
            Paint(Box("Header light", dungeonChoices, DungeonTopLeft, new Vector2(54, -1),
                new Vector2(56, 3)), MapAccent, false);
            AddCircuitMargins(dungeonChoices, 21f, 12f, .04f);
            bool hasCaption = !string.IsNullOrEmpty(interactionCaption);
            var heading = Label("Interaction title", dungeonChoices, DungeonTopLeft,
                new Vector2(211, hasCaption ? -37 : -46), new Vector2(366, 42), 28);
            heading.text = interactionTitle;
            heading.alignment = TextAnchor.MiddleLeft;
            heading.fontStyle = FontStyle.Bold;
            if (!hasCaption) return;
            var caption = Label("Interaction subtitle", dungeonChoices, DungeonTopLeft, new Vector2(211, -67),
                new Vector2(366, 23), 15);
            caption.alignment = TextAnchor.MiddleLeft;
            caption.color = DungeonMuted;
            caption.text = interactionCaption;
        }

        private void BuildDungeonPagination(int lastPage)
        {
            DungeonButton("Previous card page", "‹", dungeonChoices, TopRight, new Vector2(-130, -54),
                new Vector2(36, 36), () => ChangeDungeonCardPage(-1)).interactable = dungeonCardPage > 0;
            Label("Card page", dungeonChoices, TopRight, new Vector2(-87, -54), new Vector2(44, 32), 14).text =
                $"{dungeonCardPage + 1} / {lastPage + 1}";
            DungeonButton("Next card page", "›", dungeonChoices, TopRight, new Vector2(-45, -54),
                new Vector2(36, 36), () => ChangeDungeonCardPage(1)).interactable = dungeonCardPage < lastPage;
        }

        private void ChangeDungeonCardPage(int direction)
        {
            dungeonCardPage += direction;
            GameSfx.Play(Sfx.QueueMove, .60f);
            RenderDungeonInteraction();
        }

        private void BuildInteractionCard(InteractionChoice choice, Vector2 position, float entryDelay)
        {
            var visual = choice.Card.GetComponent<CardHoverVisual>();
            var cardSize = visual.Face.sizeDelta;
            var tile = Box("Choose " + choice.Title, dungeonChoices, DungeonTop, position, cardSize + new Vector2(14, 44));
            var hitBox = Paint(tile, Color.clear, true);
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = hitBox;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => SelectDungeonChoice(choice, playSound: true));

            var cardVisual = Box("Card motion", tile, DungeonMiddle, Vector2.zero, cardSize);
            var group = cardVisual.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            visual.CreateDisplayFace(cardVisual, choice.Card);
            var outlineBox = Box("Card selection", cardVisual, DungeonMiddle, Vector2.zero, cardSize + new Vector2(9, 9));
            outlineBox.gameObject.AddComponent<CanvasRenderer>();
            var outline = outlineBox.gameObject.AddComponent<CardOutline>();
            outline.raycastTarget = false;

            var interactionTile = new InteractionTile
            {
                Choice = choice, Border = outline, CardVisual = cardVisual, Group = group,
                EntryDelay = entryDelay
            };
            if (rewardSpark != null)
            {
                var spark = Box("Selection spark", cardVisual, DungeonMiddle,
                    cardSize * .5f - new Vector2(9, 18), Vector2.one * 88f);
                interactionTile.Spark = spark.gameObject.AddComponent<RawImage>();
                interactionTile.Spark.texture = rewardSpark;
                interactionTile.Spark.raycastTarget = false;
                interactionTile.Spark.enabled = false;
            }
            interactionTiles.Add(interactionTile);

            string price = choice.Price > 0 ? $"碎片 {choice.Price}" : "";
            string detail = string.Join("  ·  ", new[] { price, choice.Detail }.Where(value => value.Length > 0));
            if (detail.Length == 0) return;
            var tag = Label("Price and copies", tile, DungeonMiddle, new Vector2(0, -cardSize.y / 2f - 19f),
                new Vector2(164, 23), 14);
            tag.text = detail;
            tag.color = DungeonMuted;
        }

        private void BuildDungeonCardDetails(float width)
        {
            dungeonDetail = Box("Selected card details", dungeonChoices, DungeonTop, new Vector2(0, -391),
                new Vector2(width - 56, 126));
            Paint(dungeonDetail, DungeonInset, false);
            Paint(Box("Details divider", dungeonDetail, DungeonTop, Vector2.zero,
                new Vector2(width - 56, 1)), DungeonLine, false);
            Paint(Box("Details light", dungeonDetail, new Vector2(0, .5f), new Vector2(1, 0),
                new Vector2(2, 126)), MapAccent, false);

            float textWidth = dungeonDetail.sizeDelta.x - 38;
            dungeonDetailTitle = Label("Card title", dungeonDetail, DungeonTopLeft,
                new Vector2(20 + textWidth * .5f, -23), new Vector2(textWidth, 27), 20);
            dungeonDetailTitle.alignment = TextAnchor.MiddleLeft;
            dungeonDetailTitle.fontStyle = FontStyle.Bold;
            dungeonDetailMeta = Label("Card type and cost", dungeonDetail, DungeonTopLeft,
                new Vector2(20 + textWidth * .5f, -23), new Vector2(textWidth, 27), 15);
            dungeonDetailMeta.alignment = TextAnchor.MiddleRight;
            dungeonDetailMeta.color = MapAccent;
            dungeonDetailBody = Label("Card effect", dungeonDetail, DungeonTopLeft,
                new Vector2(20 + textWidth * .5f, -79), new Vector2(textWidth, 81), 16);
            dungeonDetailBody.alignment = TextAnchor.UpperLeft;
            dungeonDetailBody.color = DungeonMuted;
            dungeonDetailBody.lineSpacing = 1.05f;
        }

        private void SetDungeonCardDetails(InteractionChoice choice)
        {
            if (dungeonDetail == null) return;
            CardDefinition card = choice?.Card;
            dungeonDetailTitle.text = choice?.Title ?? "";
            if (card == null)
            {
                dungeonDetailMeta.text = choice?.Price > 0 ? $"碎片 {choice.Price}" : "";
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
            dungeonDetailBody.text = card.Describe();
        }

        private void BuildInteractionService(InteractionChoice choice, Vector2 position, float width)
        {
            var row = DungeonButton("Choose " + choice.Title, "", dungeonChoices, DungeonTop, position,
                new Vector2(width, 46), () => SelectDungeonChoice(choice, playSound: true));
            var rowRect = (RectTransform)row.transform;
            float inset = choice.Icon != null ? 52 : 15;
            if (choice.Icon != null)
            {
                var icon = Paint(Box("Reward icon", rowRect, new Vector2(0, .5f), new Vector2(25, 0),
                    new Vector2(30, 30)), Color.white, false, choice.Icon);
                icon.preserveAspect = true;
            }
            var title = Label("Choice name", rowRect, new Vector2(0, .5f), new Vector2(inset + 91, 0),
                new Vector2(182, 32), 17);
            title.alignment = TextAnchor.MiddleLeft;
            title.text = choice.Title;
            string detail = choice.Detail;
            if (choice.Price > 0)
                detail = string.IsNullOrEmpty(detail) ? $"碎片 {choice.Price}" : $"{detail}   ·   碎片 {choice.Price}";
            var info = Label("Choice details", rowRect, new Vector2(1, .5f), new Vector2(-108, 0),
                new Vector2(186, 32), 14);
            info.alignment = TextAnchor.MiddleRight;
            info.color = DungeonMuted;
            info.text = detail;
            interactionTiles.Add(new InteractionTile { Choice = choice, Border = row.targetGraphic });
        }

        private void BuildInteractionFooter(float width)
        {
            Paint(Box("Footer divider", dungeonChoices, new Vector2(.5f, 0), new Vector2(0, 79),
                new Vector2(width - 56, 1)), DungeonLine, false);
            var lowerRight = new Vector2(1, 0);
            dungeonConfirm = DungeonButton("Confirm choice", "", dungeonChoices, lowerRight, new Vector2(-105, 49),
                new Vector2(154, 46), ConfirmDungeonChoice, primary: true);
            dungeonConfirmLabel = dungeonConfirm.GetComponentInChildren<Text>();
            float feedbackWidth = width - (interactionSecondary.Apply != null ? 300 : 215);
            dungeonFeedback = Label("Interaction feedback", dungeonChoices, Vector2.zero,
                new Vector2(28 + feedbackWidth / 2f, 49), new Vector2(feedbackWidth, 38), 15);
            dungeonFeedback.alignment = TextAnchor.MiddleLeft;
            if (interactionSecondary.Apply == null) return;
            var secondary = DungeonButton("Secondary action", interactionSecondary.Label, dungeonChoices, lowerRight,
                new Vector2(-228, 49), new Vector2(78, 42), () => interactionSecondary.Apply());
            secondary.targetGraphic.color = DungeonSurface;
            secondary.GetComponentInChildren<Text>().color = DungeonMuted;
        }

        private Button DungeonButton(string name, string title, RectTransform parent, Vector2 anchor, Vector2 position,
            Vector2 size, UnityAction onClick, bool primary = false)
        {
            var button = ButtonAt(name, parent, anchor, position, size, onClick, out var label);
            button.targetGraphic.color = primary ? MapAccent : DungeonChoiceFill;
            var colors = button.colors;
            colors.fadeDuration = .14f;
            colors.colorMultiplier = 1.12f;
            colors.normalColor = new Color(.893f, .893f, .893f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(.75f, .82f, .88f);
            button.colors = colors;
            label.text = title;
            label.color = primary ? MapPanel : Paper;
            label.fontStyle = primary ? FontStyle.Bold : FontStyle.Normal;
            return button;
        }

        private void SelectDungeonChoice(InteractionChoice choice, bool playSound = false)
        {
            if (!DungeonInteractionOpen) return;
            if (choice != null && dungeonSelected == choice && !playSound) return;
            if (playSound) GameSfx.Play(Sfx.CardSelect);
            dungeonSelected = choice;
            foreach (var tile in interactionTiles)
            {
                bool selected = tile.Choice == choice;
                if (tile.Border is CardOutline outline)
                    outline.ShowRewardSelection(selected, selected ? SelectionCyan : DungeonLine, playSound);
                if (playSound && selected) tile.SparkStarted = Time.unscaledTime;
            }
            SetDungeonCardDetails(choice);
            dungeonConfirm.interactable = choice != null;
            dungeonConfirmLabel.text = choice?.Card != null
                ? removingDungeonCards ? "确认删牌" : choice.Price > 0 ? "购买卡牌" : "领取卡牌"
                : "确认";
            SetDungeonFeedback("");
        }

        private void StepDungeonSelection(int direction)
        {
            if (interactionTiles.Count == 0) return;
            int index = interactionTiles.FindIndex(tile => tile.Choice == dungeonSelected);
            index = index < 0 ? 0 : (index + direction + interactionTiles.Count) % interactionTiles.Count;
            SelectDungeonChoice(interactionTiles[index].Choice, playSound: true);
        }

        private void UpdateDungeonInteractionMotion()
        {
            float delta = Time.unscaledDeltaTime;
            UpdateActorPrompt();
            if (dungeonPrompt.gameObject.activeSelf)
            {
                dungeonPromptProgress = Mathf.MoveTowards(dungeonPromptProgress, 1f, delta / .21f);
                float ease = DungeonEaseOut(dungeonPromptProgress);
                dungeonPromptGroup.alpha = ease;
                dungeonPrompt.anchoredPosition = Vector2.down * (10f * (1f - ease) * dungeonPrompt.localScale.y);
            }
            if (!dungeonShade.gameObject.activeSelf) return;
            bool open = DungeonInteractionOpen;
            dungeonPanelProgress = Mathf.MoveTowards(dungeonPanelProgress, open ? 1f : 0f, delta / (open ? .24f : .16f));
            if (!open && dungeonPanelProgress <= 0f)
            {
                dungeonShade.gameObject.SetActive(false);
                return;
            }

            float easedPanel = DungeonEaseOut(dungeonPanelProgress);
            dungeonShadeGroup.alpha = easedPanel;
            dungeonChoices.localScale = Vector3.one * Mathf.Lerp(.965f, 1f, easedPanel);
            dungeonChoices.anchoredPosition = Vector2.down * (16f * (1f - easedPanel));
            float elapsed = Time.unscaledTime - dungeonPageStarted;
            dungeonChoicesGroup.alpha = DungeonEaseOut(elapsed / .15f);
            foreach (var tile in interactionTiles)
            {
                bool selected = tile.Choice == dungeonSelected;
                tile.Lift = Mathf.MoveTowards(tile.Lift, selected ? 1f : 0f, delta / .19f);
                float lift = Mathf.SmoothStep(0f, 1f, tile.Lift);
                if (tile.CardVisual == null)
                {
                    tile.Border.color = Color.Lerp(DungeonChoiceFill, Color.Lerp(DungeonChoiceFill, MapAccent, .45f), lift);
                    continue;
                }
                float entry = DungeonEaseOut((elapsed - tile.EntryDelay) / .23f);
                tile.Group.alpha = entry;
                tile.CardVisual.anchoredPosition = Vector2.up * (15f * lift - 18f * (1f - entry));
                tile.CardVisual.localScale = Vector3.one * (1f + .045f * lift + .03f * Mathf.Sin(tile.Lift * Mathf.PI));
                UpdateRewardSpark(tile, open && selected);
            }
            if (dungeonFeedback != null && dungeonFeedback.text.Length > 0)
            {
                float feedback = DungeonEaseOut((Time.unscaledTime - dungeonFeedbackStarted) / .15f);
                var tint = dungeonFeedback.color;
                tint.a = feedback;
                dungeonFeedback.color = tint;
            }
        }

        private static void UpdateRewardSpark(InteractionTile tile, bool selected)
        {
            if (tile.Spark == null) return;
            float elapsed = Time.unscaledTime - tile.SparkStarted;
            tile.Spark.enabled = selected && elapsed >= 0f && elapsed < RewardSparkFrames / (float)RewardSparkFps;
            if (!tile.Spark.enabled) return;
            int frame = Mathf.Min(Mathf.FloorToInt(elapsed * RewardSparkFps), RewardSparkFrames - 1);
            const float cell = 1f / RewardSparkColumns;
            tile.Spark.uvRect = new Rect(frame % RewardSparkColumns * cell,
                1f - (frame / RewardSparkColumns + 1) * cell, cell, cell);
        }

        private static float DungeonEaseOut(float progress)
        {
            float remaining = 1f - Mathf.Clamp01(progress);
            return 1f - remaining * remaining * remaining;
        }

        private void ConfirmDungeonChoice()
        {
            if (!DungeonInteractionOpen || DungeonRoomTransitioning || DungeonMenuOpen || batch.Busy || dungeonSelected == null) return;
            if (fragments < dungeonSelected.Price)
            {
                SetDungeonFeedback("数据碎片不足");
                return;
            }
            dungeonSelected.Apply();
        }

        private void SetDungeonFeedback(string message, bool warning = true)
        {
            if (!DungeonInteractionOpen) return;
            if (warning && message.Length > 0) GameSfx.Play(Sfx.DungeonDenied);
            dungeonFeedback.color = warning ? new Color32(255, 191, 141, 255) : MapAccent;
            dungeonFeedback.text = message;
            dungeonFeedbackStarted = Time.unscaledTime;
        }
    }

    internal sealed class RoomInteractionCircle : MaskableGraphic
    {
        private const int ArcSegments = 40;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            Vector2 center = rect.center;
            Vector2 radius = rect.size * .5f;
            mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < ArcSegments; i++)
            {
                float angle = i * (2f * Mathf.PI / ArcSegments);
                Vector2 edge = new(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
                mesh.AddVert(center + edge, color, Vector2.zero);
                mesh.AddTriangle(0, (i + 1) % ArcSegments + 1, i + 1);
            }
        }
    }

    internal sealed class RoomInteractionGlyphCenter : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount == 0) return;

            UIVertex vertex = default;
            mesh.PopulateUIVertex(ref vertex, 0);
            Vector2 lower = vertex.position;
            Vector2 upper = lower;
            for (int i = 1; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                lower = Vector2.Min(lower, vertex.position);
                upper = Vector2.Max(upper, vertex.position);
            }

            Vector3 offset = graphic.rectTransform.rect.center - (lower + upper) * .5f;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                vertex.position += offset;
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
