using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fighter403.UI
{
    [DisallowMultipleComponent]
    public sealed class CardHoverVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float ExpandedHeight = 60f;
        private const float DescriptionWidth = 138f;
        private const float DescriptionHeight = 58f;
        private const float DescriptionBottomPadding = 5f;
        private const float DealDuration = 0.29f;
        internal const float RejectionDuration = 0.26f;
        private const float ShakeDistance = 7f;
        private const int LockSheetColumns = 7, LockSheetRows = 6;
        private const int LockInFrames = 10, LockHoldFrames = 20, LockOutFrames = 10;
        private const float LockFrameRate = 30f;
        private static readonly Color SummaryColor = new(0.90f, 0.93f, 0.89f);

        [SerializeField] private RectTransform face, frame;
        [SerializeField] private GameObject focusBorder, detail;
        [SerializeField, Min(0.01f)] private float transitionTime = 0.14f;

        private readonly List<(RectTransform rect, Vector2 position)> faceChildren = new();
        private List<(UnityEngine.UI.Graphic graphic, Color color, Material material)> rootLockGraphics;
        private CardHandController owner;
        private HandCard card;
        private RectTransform rect, shadow;
        private Graphic rootGraphic, frameImage;
        private CardOutline outline;
        private RawImage rootLockEffect;
        private float rootLockEffectStarted;
        private int rootLockEffectFrame = -1;
        private UnityEngine.UI.Text expandedText;
        private TMP_Text feeLabel;
        private int shownEnergyCost;
        private Vector2 restPosition, restSize, frameSize, shadowSize, slotPosition, dealOrigin, dragOffset;
        private float expanded, expansionHeight, dealTime = DealDuration, dealScale, deniedUntil;
        private float readyFlashStart = float.NegativeInfinity;
        private CardSelectionState selectionState;
        private bool hovered, dragging;

        private static readonly Sprite[] AttributeIcons = new Sprite[3];
        private static readonly Color32 Ink = new(12, 16, 25, 255);

        internal RectTransform Face => face;

        internal static Color AttributeColor(CardClass attribute) => attribute switch
        {
            CardClass.Attack => new Color32(255, 105, 109, 255),
            CardClass.Ability => new Color32(255, 205, 101, 255),
            CardClass.Defense => new Color32(105, 195, 255, 255),
            _ => Color.white
        };

        private static Sprite AttributeIcon(CardClass attribute)
        {
            string key = attribute switch
            {
                CardClass.Attack => "Triangle",
                CardClass.Ability => "Circle",
                CardClass.Defense => "Square",
                _ => null
            };
            if (key == null) return null;
            return AttributeIcons[(int)attribute] ??=
                Resources.Load<Sprite>("Fighter403/CardAttributes/" + key);
        }

        internal static void DressFace(RectTransform cardFace, CardDefinition definition, int energyCost)
        {
            Color accent = AttributeColor(definition.Class);
            cardFace.Find("Frame").GetComponent<Image>().color = Color.Lerp(Color.white, accent, 0.68f);
            var name = cardFace.Find("Name").GetComponent<TMP_Text>();
            name.color = Color.Lerp(Color.white, accent, 0.26f);
            var fee = cardFace.Find("Fee").GetComponent<TMP_Text>();
            fee.color = Color.Lerp(Color.white, accent, 0.4f);
            fee.text = energyCost.ToString();
            var shape = cardFace.Find("Shape").GetComponent<Image>();
            shape.sprite = AttributeIcon(definition.Class);
            shape.color = accent;
            var art = cardFace.Find("Illustration").GetComponent<Image>();
            DrawMinor(art, definition.MinorAttribute, "Minor attribute", 0);
            DrawMinor(art, definition.SecondMinorAttribute, "Second minor attribute", 1);
        }

        internal static void UpdateThumbnail(Image artwork, CardDefinition definition)
        {
            artwork.sprite = definition != null ? definition.GetComponent<CardHoverVisual>().Icon : null;
            artwork.enabled = artwork.sprite != null;
            DrawMinor(artwork, definition != null ? definition.MinorAttribute : CardClass.Unspecified,
                "Minor attribute", 0, true);
            DrawMinor(artwork, definition != null ? definition.SecondMinorAttribute : CardClass.Unspecified,
                "Second minor attribute", 1, true);
        }

        private static void DrawMinor(Image artwork, CardClass attribute, string label, int index, bool thumbnail = false)
        {
            var existing = artwork.transform.Find(label) as RectTransform;
            var sprite = AttributeIcon(attribute);
            if (sprite == null)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }

            float size = thumbnail ? 10f : 24f;
            var plate = existing != null ? existing.GetComponent<Image>()
                : NewBadge(artwork.rectTransform, label);
            plate.gameObject.SetActive(true);
            plate.color = Ink;
            plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            plate.rectTransform.sizeDelta = Vector2.one * size;
            plate.rectTransform.anchoredPosition = thumbnail
                ? new Vector2(artwork.rectTransform.rect.width * 0.34f - index * 11f, -artwork.rectTransform.rect.height * 0.33f)
                : new Vector2(25f - index * 26f, -19f);

            var glyph = plate.transform.Find("Glyph")?.GetComponent<Image>() ?? NewBadge(plate.rectTransform, "Glyph");
            glyph.rectTransform.sizeDelta = Vector2.one * (size - (thumbnail ? 0f : 2f));
            glyph.sprite = sprite;
            glyph.color = AttributeColor(attribute);
        }

        private static Image NewBadge(RectTransform parent, string label)
        {
            var image = new GameObject(label, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.gameObject.layer = parent.gameObject.layer;
            image.raycastTarget = false;
            return image;
        }
        internal Sprite Icon => face.Find("Illustration")?.GetComponent<Image>().sprite;

        internal void CreateDisplayFace(RectTransform parent, CardDefinition definition)
        {
            var display = Instantiate(face, parent, false);
            display.name = face.name;
            display.anchoredPosition = Vector2.zero;
            if (rootLockEffect != null)
                display.Find(rootLockEffect.name)?.gameObject.SetActive(false);
            DressFace(display, definition, definition.EnergyCost);
            StyleSummary(display.Find(detail.name).gameObject);
            display.Find(frame.name)?.Find(focusBorder.name)?.gameObject.SetActive(false);
            foreach (var graphic in display.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        }

        private static void StyleSummary(GameObject summary)
        {
            ((RectTransform)summary.transform).anchoredPosition = new Vector2(0f, -67f);
            var label = summary.GetComponentInChildren<TMP_Text>(true);
            label.fontSize = 15f;
            label.color = SummaryColor;
            summary.SetActive(true);
        }

        internal void Bind(CardHandController controller, HandCard item, Font font)
        {
            owner = controller;
            card = item;
            rect = (RectTransform)transform;
            rect.localRotation = Quaternion.identity;
            foreach (var graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            rootGraphic = GetComponent<Graphic>();
            rootGraphic.raycastTarget = true;
            frameImage = frame.GetComponent<Image>();
            frameImage.raycastTarget = true;

            restPosition = face.anchoredPosition;
            restSize = face.sizeDelta;
            frameSize = frame.sizeDelta;
            shadow = (RectTransform)face.Find("Shadow");
            shadowSize = shadow.sizeDelta;
            shownEnergyCost = item.Definition.EnergyCost;
            DressFace(face, item.Definition, shownEnergyCost);
            feeLabel = face.Find("Fee").GetComponent<TMP_Text>();

            StyleSummary(detail);
            focusBorder.SetActive(false);
            foreach (RectTransform child in face)
                if (child.anchorMin.y == 0.5f && child.anchorMax.y == 0.5f)
                    faceChildren.Add((child, child.anchoredPosition));

            expandedText = new GameObject("Expanded description", typeof(RectTransform), typeof(UnityEngine.UI.Text))
                .GetComponent<UnityEngine.UI.Text>();
            expandedText.gameObject.layer = gameObject.layer;
            expandedText.rectTransform.SetParent(face, false);
            expandedText.rectTransform.sizeDelta = new Vector2(DescriptionWidth, DescriptionHeight);
            expandedText.font = font;
            expandedText.fontSize = 14;
            expandedText.alignment = TextAnchor.MiddleCenter;
            expandedText.color = new Color(0.93f, 0.96f, 0.90f);
            expandedText.raycastTarget = false;
            expandedText.text = item.Definition.Describe();
            float descriptionHeight = Mathf.Max(DescriptionHeight, Mathf.Ceil(expandedText.preferredHeight));
            expansionHeight = ExpandedHeight + descriptionHeight - DescriptionHeight;
            expandedText.rectTransform.sizeDelta = new Vector2(DescriptionWidth, descriptionHeight);
            expandedText.rectTransform.anchoredPosition = new Vector2(0f,
                -(restSize.y + expansionHeight - descriptionHeight) * 0.5f + DescriptionBottomPadding);
            expandedText.gameObject.SetActive(false);

            var borderObject = new GameObject("Card state outline", typeof(RectTransform), typeof(CanvasRenderer));
            borderObject.layer = gameObject.layer;
            var borderRect = (RectTransform)borderObject.transform;
            borderRect.SetParent(face, false);
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = borderRect.offsetMax = Vector2.zero;
            outline = borderObject.AddComponent<CardOutline>();
            outline.raycastTarget = false;
        }

        internal void SetSlot(Vector2 position)
        {
            slotPosition = position;
            if (!dragging && dealTime >= DealDuration) rect.anchoredPosition = position;
        }

        internal void DealFrom(Vector2 position, float scale)
        {
            dealOrigin = position;
            dealScale = scale;
            dealTime = 0f;
            UpdateDeal();
        }

        internal void SetEnergyCost(int cost)
        {
            if (shownEnergyCost == cost) return;
            shownEnergyCost = cost;
            feeLabel.text = cost.ToString();
        }

        internal void SetSelectionState(CardSelectionState state, float progress)
        {
            outline.SetReadiness(progress);
            if (state == selectionState) return;
            if (state == CardSelectionState.Locked || selectionState == CardSelectionState.Locked)
                SetRootLockVisual(state == CardSelectionState.Locked);
            bool flash = state == CardSelectionState.Ready && selectionState == CardSelectionState.InsufficientEnergy;
            readyFlashStart = flash ? Time.unscaledTime : float.NegativeInfinity;
            outline.SetReadyFlash(flash ? owner.EvaluateReadyFlash(0f) : Color.clear);
            selectionState = state;
            if (state != CardSelectionState.InsufficientEnergy) deniedUntil = 0f;
        }

        private void SetRootLockVisual(bool locked)
        {
            if (locked && rootLockEffect == null) CreateRootLockEffect();
            outline.SetRootLocked(locked, locked && rootLockEffect == null);
            if (rootLockEffect != null)
            {
                rootLockEffectStarted = Time.unscaledTime;
                rootLockEffect.gameObject.SetActive(true);
                UpdateRootLockEffect(rootLockEffectStarted, locked);
            }
            if (!locked)
            {
                foreach (var entry in rootLockGraphics)
                {
                    if (entry.graphic == null) continue;
                    entry.graphic.color = entry.color;
                    if (entry.graphic is UnityEngine.UI.Image) entry.graphic.material = entry.material;
                }
                rootLockGraphics.Clear();
                return;
            }

            rootLockGraphics ??= new();
            Material grayscale = owner.GetRootHandLockMaterial();
            foreach (var graphic in face.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                if (graphic == outline || graphic == rootLockEffect) continue;
                Color original = graphic.color;
                rootLockGraphics.Add((graphic, original, graphic.material));
                float gray = original.grayscale;
                graphic.color = new Color(gray, gray, gray, original.a);
                if (grayscale != null && graphic is UnityEngine.UI.Image) graphic.material = grayscale;
            }
        }

        private void CreateRootLockEffect()
        {
            var sheet = Resources.Load<Texture2D>("Fighter403/Bosses/RootCardLock");
            if (sheet == null) return;
            var effect = new GameObject("Root card seal", typeof(RectTransform), typeof(CanvasRenderer));
            effect.layer = gameObject.layer;
            var effectRect = (RectTransform)effect.transform;
            effectRect.SetParent(face, false);
            effectRect.anchorMin = effectRect.anchorMax = new Vector2(.5f, .5f);
            effectRect.anchoredPosition = Vector2.zero;
            effectRect.sizeDelta = new Vector2(88f, 88f);
            rootLockEffect = effect.AddComponent<RawImage>();
            rootLockEffect.texture = sheet;
            rootLockEffect.raycastTarget = false;
        }

        private void UpdateRootLockEffect(float now, bool locked)
        {
            if (rootLockEffect == null || !rootLockEffect.gameObject.activeSelf) return;
            int frame = Mathf.Max(0, Mathf.FloorToInt((now - rootLockEffectStarted) * LockFrameRate));
            if (locked && frame >= LockInFrames)
                frame = LockInFrames + (frame - LockInFrames) % LockHoldFrames;
            else if (!locked)
            {
                if (frame >= LockOutFrames)
                {
                    rootLockEffect.gameObject.SetActive(false);
                    return;
                }
                frame += LockInFrames + LockHoldFrames;
            }
            if (rootLockEffectFrame == frame) return;
            rootLockEffectFrame = frame;
            rootLockEffect.uvRect = new Rect(
                (frame % LockSheetColumns) / (float)LockSheetColumns,
                (LockSheetRows - 1 - frame / LockSheetColumns) / (float)LockSheetRows,
                1f / LockSheetColumns, 1f / LockSheetRows);
        }

        internal void ShowInsufficientEnergy(float until) => deniedUntil = until;

        internal static float RejectionProgress(float remaining) => 1f - Mathf.Clamp01(remaining / RejectionDuration);

        internal static float RejectionShake(float progress) => (1f - progress) * Mathf.Sin(progress * 4f * Mathf.PI);

        internal void LockVisual()
        {
            SetSelectionState(CardSelectionState.Ready, 1f);
            owner = null;
            rootGraphic.raycastTarget = false;
            frameImage.raycastTarget = false;
            outline.gameObject.SetActive(false);
            enabled = false;
        }

        public void OnPointerEnter(PointerEventData e) => hovered = owner != null;
        public void OnPointerExit(PointerEventData e) => hovered = false;
        private void OnDisable()
        {
            hovered = false;
            readyFlashStart = float.NegativeInfinity;
            outline?.SetReadyFlash(Color.clear);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && !e.dragging) owner?.ToggleSelection(card);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || owner == null || !owner.BeginCardDrag(card, e)) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent,
                e.pressPosition, e.pressEventCamera, out var pressPoint);
            dragOffset = (Vector2)rect.localPosition - pressPoint;
            dragging = true;
            dealTime = DealDuration;
            rect.localScale = Vector3.one;
            rootGraphic.raycastTarget = frameImage.raycastTarget = false;
            rect.SetAsLastSibling();
            DragTo(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!dragging || e.button != PointerEventData.InputButton.Left) return;
            DragTo(e);
            owner?.UpdateCardDrag(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging || e.button != PointerEventData.InputButton.Left) return;
            StopDragging();
            owner?.EndCardDrag(e);
        }

        private void DragTo(PointerEventData e)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent,
                e.position, e.pressEventCamera, out var point))
                rect.localPosition = point + dragOffset;
        }

        internal void StopDragging()
        {
            if (!dragging) return;
            dragging = false;
            rect.anchoredPosition = slotPosition;
            rootGraphic.raycastTarget = frameImage.raycastTarget = true;
        }

        private void LateUpdate()
        {
            if (owner == null) return;

            float now = Time.unscaledTime;
            UpdateRootLockEffect(now, selectionState == CardSelectionState.Locked);
            float remainingFeedback = deniedUntil - now;
            bool denied = remainingFeedback > 0f;
            bool selected = selectionState == CardSelectionState.Selected;
            bool hoverOutline = hovered && selectionState == CardSelectionState.Ready;
            outline.Show(selected && !denied, denied, hoverOutline);
            if (!float.IsNegativeInfinity(readyFlashStart))
            {
                Color flash = owner.EvaluateReadyFlash(now - readyFlashStart);
                outline.SetReadyFlash(flash);
                if (flash.a <= 0f) readyFlashStart = float.NegativeInfinity;
            }

            float dt = Time.unscaledDeltaTime;
            if (dealTime < DealDuration)
            {
                dealTime = Mathf.Min(DealDuration, dealTime + dt);
                UpdateDeal();
            }

            bool showDescription = selectionState != CardSelectionState.Locked && (hovered || selected);
            float nextExpanded = Mathf.MoveTowards(expanded, showDescription ? 1f : 0f, dt / transitionTime);
            float extra = expansionHeight * Mathf.SmoothStep(0f, 1f, nextExpanded);
            float shake = ShakeDistance * RejectionShake(RejectionProgress(remainingFeedback));
            var facePosition = restPosition + new Vector2(shake, extra * 0.5f);
            if (face.anchoredPosition != facePosition) face.anchoredPosition = facePosition;
            if (expanded == nextExpanded) return;
            expanded = nextExpanded;

            Vector2 growth = Vector2.up * extra;
            face.sizeDelta = restSize + growth;
            frame.sizeDelta = frameSize + growth;
            shadow.sizeDelta = shadowSize + growth;
            foreach (var child in faceChildren) child.rect.anchoredPosition = child.position + growth * 0.5f;

            expandedText.gameObject.SetActive(expanded > 0.5f);
            var color = expandedText.color;
            color.a = Mathf.InverseLerp(0.5f, 1f, expanded);
            expandedText.color = color;
        }

        private void UpdateDeal()
        {
            float t = dealTime / DealDuration;
            float ease = Mathf.SmoothStep(0f, 1f, t);
            rect.anchoredPosition = Vector2.Lerp(dealOrigin, slotPosition, ease) + Vector2.up * (36f * Mathf.Sin(Mathf.PI * t));
            rect.localScale = Vector3.one * Mathf.Lerp(dealScale, 1f, ease);
            if (t >= 1f) rect.anchoredPosition = slotPosition;
        }
    }
}
