using System;
using System.Collections.Generic;
using Fighter403.Audio;
using TMPro;
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
        private static readonly Vector2 DungeonMiddle = Vector2.one * .5f;
        private static readonly Vector2 DungeonTop = new(.5f, 1f);
        private static readonly Vector2 DungeonTopLeft = new(0, 1);
        private static readonly Color DungeonMuted = new Color32(175, 197, 211, 255);
        private static readonly Color DungeonChoiceFill = new Color32(28, 43, 60, 255);
        private static readonly Color DungeonInset = new Color32(9, 17, 29, 255);

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
            public DungeonTerminalPlate Plate;
            public Graphic Marker;
            public RectTransform CardVisual;
            public CanvasGroup Group;
            public float Lift, EntryDelay, BaseY;
        }

        private readonly List<InteractionChoice> interactionChoices = new();
        private readonly List<InteractionTile> interactionTiles = new();
        private RectTransform dungeonPrompt, actorPrompt, dungeonShade, dungeonPanel, dungeonChoices, dungeonDetail;
        private CanvasGroup dungeonPromptGroup, dungeonShadeGroup, dungeonChoicesGroup;
        private Text dungeonPromptLabel;
        private TMP_Text dungeonConfirmLabel, dungeonFeedback;
        private TMP_Text dungeonDetailTitle, dungeonDetailMeta, dungeonDetailBody;
        private Button dungeonConfirm;
        private DungeonRoom presentedRoom;
        private bool presentedFinished, dungeonInteractionAvailable, dungeonInteractionPending, removingDungeonCards;
        private int dungeonCardPage;
        private InteractionChoice dungeonSelected;
        private string interactionTitle, interactionCaption, interactionKeyboardHint;
        private (string Label, Action Apply) interactionSecondary;
        private float dungeonPanelProgress, dungeonPromptProgress, dungeonPageStarted;

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

            dungeonShade = BuildDungeonShade("Dungeon interaction overlay");
            dungeonShadeGroup = dungeonShade.gameObject.AddComponent<CanvasGroup>();
            dungeonPanel = Box("Dungeon interaction", dungeonShade, DungeonMiddle, Vector2.zero, Vector2.zero);
            dungeonChoices = Box("Interaction content", dungeonPanel, DungeonMiddle, Vector2.zero, Vector2.zero);
            dungeonChoicesGroup = dungeonChoices.gameObject.AddComponent<CanvasGroup>();
            dungeonShade.gameObject.SetActive(false);
            BuildTerminalToast();
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
                terminalFirstCard = 0;
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

        private Button DungeonButton(string name, string title, RectTransform parent, Vector2 anchor, Vector2 position,
            Vector2 size, UnityAction onClick)
        {
            var button = ButtonAt(name, parent, anchor, position, size, onClick, out var label);
            button.targetGraphic.color = DungeonChoiceFill;
            var colors = button.colors;
            colors.fadeDuration = .14f;
            colors.colorMultiplier = 1.12f;
            colors.normalColor = new Color(.893f, .893f, .893f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(.75f, .82f, .88f);
            button.colors = colors;
            label.text = title;
            label.color = Paper;
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
                tile.Plate.Edge = selected ? TerminalFocus : TerminalEdge;
                tile.Plate.color = tile.CardVisual != null ? TerminalWell :
                    selected ? new Color32(32, 70, 86, 255) : TerminalRaised;
                tile.Marker.enabled = selected;
            }
            SetDungeonCardDetails(choice);
            dungeonConfirm.interactable = choice != null;
            bool unaffordable = choice != null && fragments < choice.Price;
            dungeonConfirm.targetGraphic.color = unaffordable ? new Color32(98, 58, 51, 255) : TerminalFocus;
            ((DungeonTerminalPlate)dungeonConfirm.targetGraphic).Edge = unaffordable ? TerminalWarning : TerminalFocus;
            dungeonConfirmLabel.color = unaffordable ? TerminalWarning : TerminalWell;
            dungeonConfirmLabel.text = GetTerminalAction(choice);
            SetDungeonFeedback("");
            terminalHint.text = unaffordable ? $"碎片不足 · 还差 {choice.Price - fragments}" : interactionKeyboardHint;
            terminalHint.color = unaffordable ? TerminalWarning : DungeonMuted;
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
            UpdateTerminalToast();
            if (dungeonPrompt.gameObject.activeSelf)
            {
                dungeonPromptProgress = Mathf.MoveTowards(dungeonPromptProgress, 1f, delta / .21f);
                float ease = DungeonEaseOut(dungeonPromptProgress);
                dungeonPromptGroup.alpha = ease;
                dungeonPrompt.anchoredPosition = Vector2.down * (10f * (1f - ease) * dungeonPrompt.localScale.y);
            }
            if (!dungeonShade.gameObject.activeSelf) return;
            bool open = DungeonInteractionOpen;
            if (open && WantsCompactTerminal != terminalCompact)
                RenderDungeonInteraction(dungeonSelected?.Card, dungeonSelected?.Title);
            dungeonPanelProgress = Mathf.MoveTowards(dungeonPanelProgress, open ? 1f : 0f, delta / (open ? .24f : .16f));
            if (!open && dungeonPanelProgress <= 0f)
            {
                dungeonShade.gameObject.SetActive(false);
                return;
            }

            float easedPanel = DungeonEaseOut(dungeonPanelProgress);
            dungeonShadeGroup.alpha = easedPanel;
            dungeonChoices.anchoredPosition = Vector2.down * (2f * Mathf.Round(6f * (1f - easedPanel)));
            float elapsed = Time.unscaledTime - dungeonPageStarted;
            dungeonChoicesGroup.alpha = DungeonEaseOut(elapsed / .15f);
            foreach (var tile in interactionTiles)
            {
                if (tile.CardVisual == null) continue;
                bool selected = tile.Choice == dungeonSelected;
                tile.Lift = Mathf.MoveTowards(tile.Lift, selected ? 1f : 0f, delta / .12f);
                float entry = DungeonEaseOut((elapsed - tile.EntryDelay) / .16f);
                tile.Group.alpha = entry;
                tile.CardVisual.anchoredPosition = Vector2.up * (tile.BaseY + 2f * Mathf.Round(2f * tile.Lift - 3f * (1f - entry)));
            }
            UpdateTerminalSpark(terminalFooterSpark, Time.unscaledTime - terminalSuccessAt);
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
            if (!DungeonInteractionOpen || dungeonFeedback == null) return;
            if (warning && message.Length > 0) GameSfx.Play(Sfx.DungeonDenied);
            dungeonFeedback.color = warning ? TerminalWarning : TerminalFocus;
            dungeonFeedback.text = message;
            terminalHint.enabled = message.Length == 0;
            terminalSuccessAt = !warning && message.Length > 0 ? Time.unscaledTime : float.NegativeInfinity;
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
