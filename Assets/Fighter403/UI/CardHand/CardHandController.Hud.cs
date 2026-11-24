using System;
using System.Collections.Generic;
using Fighter403.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fighter403.UI
{
    public sealed partial class CardHandController
    {
        private const float EnergyBarHeight = 48f, EnergyBarY = 284f, ComboHeight = 60f;
        private const int HudFrameWidth = 48, HudFrameHeight = 28, HudBurstFrames = 12, HudBurstFps = 30;
        private const int BuffColumns = 4;
        private const float BuffColumnPitch = 48f, BuffRowPitch = 58f;
        private static readonly Vector2 BuffDisplayOffset = new(-580f, 144f);
        private static readonly Color InterfaceSurface = new Color32(13, 23, 38, 248);
        private static readonly Color InterfaceInset = new Color32(19, 34, 51, 255);
        private static readonly Color InterfaceCyan = new Color32(109, 225, 240, 255);
        private static readonly Color InterfaceText = new Color32(230, 246, 255, 255);
        private static readonly Color InterfaceMuted = new Color32(157, 184, 205, 255);
        private static readonly Color EnergyFlash = new Color32(255, 117, 109, 255);
        private static readonly Color QueueFrame = new Color32(51, 89, 113, 255);
        private static readonly Color EmptySocket = new Color32(39, 65, 86, 255);
        private static readonly Color OccupiedSocket = new Color32(87, 167, 188, 255);
        private static readonly Color Recess = new Color32(15, 21, 30, 255);

        private readonly Image[] slotFrames = new Image[CardBatch.Size];
        private readonly Text[] slotOrderLabels = new Text[CardBatch.Size];
        private readonly Text[] slotCostLabels = new Text[CardBatch.Size];
        private readonly HandCard[] animatedQueue = new HandCard[CardBatch.Size];
        private readonly float[] queueChangedAt = new float[CardBatch.Size];
        private readonly HandCard[] animatedPreview = new HandCard[CardBatch.PreviewCount];
        private readonly float[] previewChangedAt = new float[CardBatch.PreviewCount];
        private readonly List<BuffTile> buffTiles = new();
        private RectTransform buffDisplay, buffHeading, emptyBuffPlate;
        private RectTransform vitalsPanel;
        private Sprite hudFrameSprite;
        private Texture2D hudPixels;
        private HudMeter healthMeter, blockMeter;
        private EnergyGauge energyGauge;
        private Text energyAmount;
        private HudMeterAccent energyAccent;
        private PlayerDamageVignette damageVignette;
        private Image executeRail;
        private RectTransform statusPlate;
        private CanvasGroup statusFade, pileFade;
        private HudBurst queueBurst;
        private double previousHealth, previousBlock;
        private int previousBlockLimit, previousReservedEnergy;
        private float reservedEnergyChangedAt = float.NegativeInfinity;
        private float previousEnergy, lastEnergySpend = float.NegativeInfinity, energySpendFrom;
        private float displayedEnergy = -1f;
        private int displayedMaxEnergy = -1;
        private float energyFullAt = float.NegativeInfinity, executeReadyAt = float.NegativeInfinity;
        private bool executeWasReady;
        private int animatedPile = -1;
        private float pileOpenedAt = float.NegativeInfinity;

        private sealed class HudMeter
        {
            public Image Frame;
            public RectTransform Track, Fill, Lost;
            public Text Amount;
            public HudBurst Burst;
            public Color RestFrame;
            public float LossStart, LossTime, DisplayedFraction;
            public float LastImpact = float.NegativeInfinity;
            public double DisplayedValue = -1;
            public int DisplayedMaximum = -1;
        }

        private sealed class HudBurst
        {
            public RawImage Image;
            public float StartTime;
        }

        private sealed class BuffTile
        {
            public RectTransform Rect, Tooltip;
            public RectTransform TimeFill;
            public CanvasGroup Fade, TooltipFade;
            public Text Timer;
            public Func<double> Remaining;
            public int DisplayedSeconds;
            public double PreviousRemaining, PeakRemaining;
            public float ShownAt, TooltipShownAt;
        }

        private void BuildBuffDisplay()
        {
            var center = Vector2.one * 0.5f;
            buffDisplay = Box("Buff display", root, hand.anchorMin, Vector2.zero, new Vector2(192f, 54f));
            var heading = Label("Buff heading", buffDisplay, center, Vector2.up * 43f, new Vector2(192f, 25f), 17);
            heading.text = "增益状态";
            heading.fontStyle = FontStyle.Bold;
            heading.color = InterfaceCyan;
            buffHeading = heading.rectTransform;
            emptyBuffPlate = Box("No active buffs", buffDisplay, center, Vector2.zero, new Vector2(150f, 28f));
            PaintInterfacePlate(emptyBuffPlate, InterfaceSurface, QueueFrame);
            var emptyBuffs = Label("Empty", emptyBuffPlate, center, Vector2.zero, new Vector2(146f, 26f), 15);
            emptyBuffs.text = "暂无增益";
            emptyBuffs.color = InterfaceMuted;

            AddBuffTile("Sandevistan", "伤害 +50%", Resources.Load<Sprite>("Fighter403SandevistanBuff"),
                new Color32(117, 205, 193, 255), () => batch.Vitals.SandevistanRemaining);
            foreach (var card in cardPrefabs)
            {
                if (card.Kind is not (CardKind.DualThread or CardKind.Counterattack or CardKind.GuardShift)) continue;
                var icon = card.GetComponent<CardHoverVisual>().Icon;
                var tint = CardHoverVisual.AttributeColor(card.Class);
                switch (card.Kind)
                {
                    case CardKind.DualThread:
                        AddBuffTile(card.DisplayName, "下次出牌消耗\n前两张同时生效", icon, tint,
                            () => batch.Vitals.DualThreadRemaining);
                        break;
                    case CardKind.Counterattack:
                        AddBuffTile(card.DisplayName, "反弹所受伤害的 50%\n含格挡吸收", icon, tint,
                            () => batch.Vitals.CounterattackRemaining);
                        break;
                    case CardKind.GuardShift:
                        AddBuffTile("反击时刻", "额外伤害等于\n当前格挡值的 50%", icon, tint,
                            () => batch.Vitals.CounterattackMomentRemaining);
                        break;
                }
            }
            UpdateBuffDisplay();
        }

        private void AddBuffTile(string name, string description, Sprite icon, Color tint, Func<double> remaining)
        {
            var center = Vector2.one * 0.5f;
            var tile = Box(name, buffDisplay, center, Vector2.zero, new Vector2(44f, 54f));
            var fade = tile.gameObject.AddComponent<CanvasGroup>();
            PaintInterfacePlate(tile, InterfaceInset, Color.Lerp(QueueFrame, tint, .58f), true);
            var iconRect = Box("Icon", tile, center, Vector2.up * 9f, new Vector2(31f, 30f));
            Paint(iconRect, Color.white, false, icon).preserveAspect = true;
            var timer = Label("Time remaining", tile, center, Vector2.down * 14f, new Vector2(40f, 18f), 15);
            timer.color = Color.Lerp(tint, Color.white, 0.65f);
            var timeTrack = Box("Time track", tile, center, Vector2.down * 23f, new Vector2(34f, 2f));
            Paint(timeTrack, EmptySocket, false);

            float tooltipHeight = description.Contains('\n') ? 70f : 52f;
            var tooltip = Box(name + " details", buffDisplay, center, Vector2.zero, new Vector2(170f, tooltipHeight));
            var buff = new BuffTile
            {
                Rect = tile, Tooltip = tooltip, Fade = fade, TooltipFade = tooltip.gameObject.AddComponent<CanvasGroup>(),
                Timer = timer, Remaining = remaining,
                TimeFill = PaintFill("Time left", timeTrack, tint, Color.Lerp(tint, Color.white, .35f))
            };
            PaintInterfacePlate(tooltip, InterfaceSurface, QueueFrame);
            var effect = Label("Effect", tooltip, center, Vector2.zero, new Vector2(158f, tooltipHeight - 8f), 17);
            effect.text = name + "\n" + description;
            effect.color = InterfaceText;
            tooltip.gameObject.SetActive(false);
            var events = tile.gameObject.AddComponent<EventTrigger>();
            Trigger(events, EventTriggerType.PointerEnter, _ =>
            {
                buff.TooltipShownAt = Time.unscaledTime;
                buff.TooltipFade.alpha = 0f;
                tooltip.SetAsLastSibling();
                tooltip.gameObject.SetActive(true);
            });
            Trigger(events, EventTriggerType.PointerExit, _ => tooltip.gameObject.SetActive(false));
            buffTiles.Add(buff);
        }

        private void UpdateBuffDisplay()
        {
            int activeCount = 0;
            float now = Time.unscaledTime;
            foreach (var buff in buffTiles)
            {
                double remaining = buff.Remaining();
                int seconds = (int)Math.Ceiling(remaining);
                bool visible = seconds > 0;
                if (buff.Rect.gameObject.activeSelf != visible)
                {
                    buff.Rect.gameObject.SetActive(visible);
                    if (visible) buff.ShownAt = now;
                    else buff.Tooltip.gameObject.SetActive(false);
                }
                if (!visible)
                {
                    buff.PreviousRemaining = 0d;
                    continue;
                }
                if (remaining > buff.PreviousRemaining) buff.PeakRemaining = remaining;
                buff.PreviousRemaining = remaining;
                SetSpan(buff.TimeFill, 0f, Mathf.Clamp01((float)(remaining / buff.PeakRemaining)));
                if (seconds != buff.DisplayedSeconds)
                {
                    buff.Timer.text = $"{seconds}s";
                    buff.DisplayedSeconds = seconds;
                }
                ++activeCount;
            }

            int index = 0;
            foreach (var buff in buffTiles)
            {
                if (!buff.Rect.gameObject.activeSelf) continue;
                int row = index / BuffColumns;
                int column = index % BuffColumns;
                int rowCount = Mathf.Min(BuffColumns, activeCount - row * BuffColumns);
                var position = new Vector2((column - (rowCount - 1) * 0.5f) * BuffColumnPitch, row * BuffRowPitch);
                float reveal = Mathf.SmoothStep(0f, 1f, (now - buff.ShownAt) / .18f);
                buff.Rect.anchoredPosition = position + Vector2.down * (5f * (1f - reveal));
                buff.Rect.localScale = Vector3.one * Mathf.Lerp(.9f, 1f, reveal);
                buff.Fade.alpha = Mathf.Lerp(.25f, 1f, reveal);
                if (buff.Tooltip.gameObject.activeSelf)
                {
                    float tooltipReveal = Mathf.SmoothStep(0f, 1f, (now - buff.TooltipShownAt) / .12f);
                    buff.TooltipFade.alpha = tooltipReveal;
                    buff.Tooltip.anchoredPosition = position + Vector2.up * Mathf.Lerp(57f, 62f, tooltipReveal);
                }
                ++index;
            }
            int lastRow = Mathf.Max(0, activeCount - 1) / BuffColumns;
            buffHeading.anchoredPosition = Vector2.up * (43f + lastRow * BuffRowPitch);
            emptyBuffPlate.gameObject.SetActive(activeCount == 0);
            buffDisplay.gameObject.SetActive(activeCount > 0 || handReveal > 0.5f);
        }

        private void LoadHudPixels()
        {
            hudPixels = Resources.Load<Texture2D>("Fighter403CombatHudPixels");
            if (hudPixels == null) return;
            hudFrameSprite = Sprite.Create(hudPixels, new Rect(0f, hudPixels.height - 24, 24, 24),
                Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * 5f);
        }

        private Image PaintPlate(RectTransform rect, Color color)
        {
            var image = Paint(rect, color, false);
            StylePlate(image, color);
            return image;
        }

        private void StylePlate(Image image, Color color)
        {
            image.color = color;
            if (hudFrameSprite == null) return;
            image.sprite = hudFrameSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.52f;
        }

        private static Image PaintInterfacePlate(RectTransform rect, Color fill, Color accent, bool raycast = false)
        {
            var frame = rect.GetComponent<Image>();
            if (frame == null) frame = Paint(rect, accent, raycast);
            frame.color = accent;
            frame.raycastTarget = raycast;
            var face = Box("Inset surface", rect, Vector2.zero, Vector2.zero, Vector2.zero);
            face.anchorMax = Vector2.one;
            face.offsetMin = Vector2.one;
            face.offsetMax = -Vector2.one;
            Paint(face, fill, false);
            face.SetAsFirstSibling();
            var mark = Box("Top contact", rect, new Vector2(0f, 1f), new Vector2(8f, 0f), new Vector2(21f, 2f));
            mark.pivot = new Vector2(0f, 1f);
            Paint(mark, Color.Lerp(accent, InterfaceCyan, .26f), false);
            mark.SetSiblingIndex(1);
            return frame;
        }

        private static void StyleInterfaceButton(Button button, Color surface, Color frameColor)
        {
            PaintInterfacePlate((RectTransform)button.transform, surface, frameColor, true);
            var colors = button.colors;
            colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f, 1f);
            colors.pressedColor = new Color(.65f, .87f, .94f, 1f);
            colors.disabledColor = new Color(.65f, .75f, .8f, .8f);
            colors.fadeDuration = .09f;
            button.colors = colors;
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.color = InterfaceText;
        }

        private static Color WithHudAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void BuildMeters()
        {
            damageVignette = new PlayerDamageVignette(root);
            var topLeft = new Vector2(0f, 1f);
            var center = Vector2.one * 0.5f;
            vitalsPanel = Box("Player vitals", root, topLeft, new Vector2(40f, -38f), new Vector2(690f, 152f));
            vitalsPanel.pivot = topLeft;
            healthMeter = BuildMeter("Player health", "生命", vitalsPanel, topLeft, new Vector2(345f, -35f),
                new Vector2(690f, 70f), 1, new Color32(192, 114, 116, 255), new Color32(180, 63, 72, 255), 10);
            blockMeter = BuildMeter("Player block", "格挡", vitalsPanel, topLeft, new Vector2(345f, -119f),
                new Vector2(690f, 66f), 2, new Color32(154, 180, 200, 255), new Color32(118, 150, 175, 255), 6);
            BuildEnergyGauge(center);
            queueBurst = BuildHudBurst(row);
            previousHealth = batch.Vitals.Health;
            previousBlock = batch.Vitals.Block;
            previousBlockLimit = batch.Vitals.MaxBlock;
            previousEnergy = batch.Vitals.Energy;
            healthMeter.DisplayedFraction = 1f;
            blockMeter.DisplayedFraction = previousBlockLimit > 0 ? (float)(previousBlock / previousBlockLimit) : 0f;
            UpdateMeters();
        }

        private void BuildEnergyGauge(Vector2 anchor)
        {
            var frame = Box("Energy", hand, anchor, new Vector2(0f, EnergyBarY), new Vector2(812f, EnergyBarHeight));
            energyGauge = frame.gameObject.AddComponent<EnergyGauge>();
            energyGauge.raycastTarget = false;
            var left = new Vector2(0f, .5f);
            if (hudPixels != null)
            {
                var icon = Box("Energy icon", frame, left, new Vector2(30f, 0f), new Vector2(19f, 19f))
                    .gameObject.AddComponent<RawImage>();
                icon.texture = hudPixels;
                icon.uvRect = AtlasRect(3 * HudFrameWidth, 0, 16, 16);
                icon.color = InterfaceCyan;
                icon.raycastTarget = false;
            }

            var title = Label("Energy name", frame, left, new Vector2(95f, 11f), new Vector2(92f, 20f), 16);
            title.text = "能量";
            title.alignment = TextAnchor.MiddleLeft;
            title.resizeTextForBestFit = false;
            title.verticalOverflow = VerticalWrapMode.Overflow;
            title.color = InterfaceCyan;
            energyAmount = Label("Energy amount", frame, left, new Vector2(95f, -8f), new Vector2(92f, 23f), 20);
            energyAmount.alignment = TextAnchor.MiddleLeft;
            energyAmount.resizeTextForBestFit = false;
            energyAmount.verticalOverflow = VerticalWrapMode.Overflow;
            energyAmount.fontStyle = FontStyle.Bold;

            var track = Box("Energy effects", frame, left, Vector2.zero, Vector2.zero);
            track.anchorMax = new Vector2(1f, .5f);
            track.offsetMin = new Vector2(EnergyGauge.TrackInsetLeft, EnergyGauge.TrackCenter - EnergyGauge.TrackHeight * .5f);
            track.offsetMax = new Vector2(-EnergyGauge.TrackInsetRight, EnergyGauge.TrackCenter + EnergyGauge.TrackHeight * .5f);
            energyAccent = HudMeterAccent.Create(track, new Color32(95, 238, 227, 255));
        }

        private HudMeter BuildMeter(string name, string caption, RectTransform parent, Vector2 anchor,
            Vector2 position, Vector2 size, int iconIndex, Color frameTint, Color fillTint, int divisions)
        {
            var bar = Box(name, parent, anchor, position, size);
            var meter = new HudMeter
            {
                Frame = PaintPlate(bar, frameTint),
                RestFrame = frameTint
            };
            const float left = 65f, inset = 18f, labelY = 13f, trackHeight = 18f;
            if (hudPixels != null)
            {
                var icon = Box("Icon", bar, new Vector2(0f, 0.5f), new Vector2(left * 0.5f + 2f, 0f),
                    Vector2.one * 40f).gameObject.AddComponent<RawImage>();
                icon.texture = hudPixels;
                icon.color = Color.Lerp(frameTint, Color.white, 0.22f);
                icon.uvRect = AtlasRect(iconIndex * HudFrameWidth, 0, 16, 16);
                icon.raycastTarget = false;
            }

            meter.Track = Box("Track", bar, Vector2.zero, Vector2.zero, Vector2.zero);
            meter.Track.anchorMax = new Vector2(1f, 0f);
            meter.Track.offsetMin = new Vector2(left, 10f);
            meter.Track.offsetMax = new Vector2(-inset, 10f + trackHeight);
            Paint(meter.Track, Color.Lerp(Recess, frameTint, 0.14f), false);
            meter.Fill = PaintFill("Fill", meter.Track, fillTint, Color.Lerp(fillTint, Color.white, 0.44f));
            meter.Lost = PaintFill("Damage trail", meter.Track, frameTint, Color.Lerp(frameTint, Color.white, 0.72f));

            for (int i = 1; i < divisions; ++i)
            {
                var tick = Box("Division", meter.Track, new Vector2((float)i / divisions, 0.5f),
                    Vector2.zero, new Vector2(1.5f, trackHeight));
                Paint(tick, new Color(0.045f, 0.07f, 0.11f, .45f), false);
            }

            var title = Label("Name", bar, new Vector2(0f, 0.5f), new Vector2(left + 52f, labelY),
                new Vector2(104f, 34f), 20);
            title.text = caption;
            title.alignment = TextAnchor.MiddleLeft;
            title.resizeTextForBestFit = false;
            title.color = Color.Lerp(frameTint, Color.white, 0.62f);
            meter.Amount = Label("Available and maximum", bar, new Vector2(1f, 0.5f), new Vector2(-100f - inset, labelY),
                new Vector2(200f, 34f), 23);
            meter.Amount.alignment = TextAnchor.MiddleRight;
            meter.Amount.resizeTextForBestFit = false;
            meter.Amount.fontStyle = FontStyle.Bold;
            meter.Burst = BuildHudBurst(meter.Track);
            return meter;
        }

        private static RectTransform PaintFill(string name, RectTransform track, Color color, Color edge)
        {
            var fill = Box(name, track, Vector2.zero, Vector2.zero, Vector2.zero);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            SetSpan(fill, 0f, 0f);
            Paint(fill, color, false);
            var light = Box("Upper bevel", fill, new Vector2(0f, 0.84f), Vector2.zero, Vector2.zero);
            light.anchorMax = Vector2.one;
            light.offsetMin = light.offsetMax = Vector2.zero;
            Paint(light, edge, false);
            var shade = Box("Lower bevel", fill, Vector2.zero, Vector2.zero, Vector2.zero);
            shade.anchorMax = new Vector2(1f, 0.18f);
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            Paint(shade, Color.Lerp(color, Recess, 0.32f), false);
            return fill;
        }

        private static void SetSpan(RectTransform fill, float from, float to)
        {
            var min = new Vector2(from, 0f);
            var max = new Vector2(to, 1f);
            if (fill.anchorMin != min) fill.anchorMin = min;
            if (fill.anchorMax != max) fill.anchorMax = max;
        }

        private HudBurst BuildHudBurst(RectTransform parent)
        {
            var rect = Box("Pixel flash", parent, Vector2.one * 0.5f, Vector2.zero,
                new Vector2(HudFrameWidth, HudFrameHeight) * 1.65f);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = hudPixels;
            image.raycastTarget = false;
            image.enabled = false;
            return new HudBurst { Image = image };
        }

        private Rect AtlasRect(int x, int y, int width, int height) => new(
            (float)x / hudPixels.width, 1f - (float)(y + height) / hudPixels.height,
            (float)width / hudPixels.width, (float)height / hudPixels.height);

        private void PlayHudBurst(HudBurst burst, Vector2 position, Color tint)
        {
            if (hudPixels == null) return;
            burst.StartTime = Time.unscaledTime;
            burst.Image.rectTransform.anchoredPosition = position;
            burst.Image.rectTransform.SetAsLastSibling();
            burst.Image.color = tint;
            burst.Image.enabled = true;
            UpdateHudBurst(burst);
        }

        private void UpdateHudBurst(HudBurst burst)
        {
            if (!burst.Image.enabled) return;
            int frame = Mathf.FloorToInt((Time.unscaledTime - burst.StartTime) * HudBurstFps);
            burst.Image.enabled = frame < HudBurstFrames;
            if (burst.Image.enabled)
                burst.Image.uvRect = AtlasRect(frame % 4 * HudFrameWidth, (1 + frame / 4) * HudFrameHeight,
                    HudFrameWidth, HudFrameHeight);
        }

        private void UpdateMeters()
        {
            vitalsPanel.localScale = Vector3.one * Mathf.Min(1f, root.rect.width / 1536f, root.rect.height / 720f);
            var vitals = batch.Vitals;
            UpdateEnergyMeter(vitals);

            damageVignette.Update(vitals.Health < previousHealth, vitals.Health * 10d < vitals.MaxHealth);
            UpdateVital(healthMeter, vitals.Health, previousHealth, vitals.MaxHealth, 0d);
            bool blockLimitChanged = previousBlockLimit != vitals.MaxBlock;
            if (blockLimitChanged)
            {
                blockMeter.LossStart = 0f;
                blockMeter.DisplayedFraction = vitals.MaxBlock > 0 ? (float)(vitals.Block / vitals.MaxBlock) : 0f;
            }
            double oldBlock = blockLimitChanged ? vitals.Block : previousBlock;
            UpdateVital(blockMeter, vitals.Block, oldBlock, vitals.MaxBlock,
                Time.deltaTime * vitals.BlockDecayPerSecond);
            previousHealth = vitals.Health;
            previousBlock = vitals.Block;
            previousBlockLimit = vitals.MaxBlock;
            UpdateHudBurst(queueBurst);
        }

        private void UpdateEnergyMeter(CombatVitals vitals)
        {
            float now = Time.unscaledTime;
            float remainingFeedback = energyDeniedUntil - now;
            float progress = CardHoverVisual.RejectionProgress(remainingFeedback);
            float deniedPulse = remainingFeedback > 0f ? .55f + .45f * Mathf.Cos(progress * 4f * Mathf.PI) : 0f;
            int maximum = vitals.MaxEnergy;
            float energy = Mathf.Clamp01(vitals.Energy / maximum);
            int accentDirection = 0;
            if (vitals.Energy < previousEnergy - .001f)
            {
                energySpendFrom = Mathf.Clamp01(previousEnergy / maximum);
                lastEnergySpend = now;
                accentDirection = -1;
            }
            if (previousEnergy < maximum && vitals.Energy >= maximum)
            {
                energyFullAt = now;
                accentDirection = 1;
            }
            previousEnergy = vitals.Energy;

            float fullPulse = 1f - Mathf.SmoothStep(0f, 1f, (now - energyFullAt) / .32f);
            float settle = Mathf.SmoothStep(0f, 1f, (now - lastEnergySpend - .055f) / .24f);
            float trail = Mathf.Lerp(Mathf.Max(energy, energySpendFrom), energy, settle);
            int reservedEnergy = batch.ReservedEnergy;
            if (previousReservedEnergy != reservedEnergy)
            {
                previousReservedEnergy = reservedEnergy;
                reservedEnergyChangedAt = now;
            }
            float available = Mathf.Clamp01((vitals.Energy - reservedEnergy) / maximum);
            float reservationGlow = reservedEnergy > 0 ? 1f - Mathf.SmoothStep(0f, 1f, (now - reservedEnergyChangedAt) / .18f) : 0f;
            energyGauge.rectTransform.anchoredPosition = new Vector2(4f * CardHoverVisual.RejectionShake(progress), EnergyBarY);
            energyGauge.SetCharge(maximum, energy, available, trail, reservationGlow, fullPulse, deniedPulse);
            if (accentDirection != 0) energyAccent.Play(energyGauge.ChargeEdgeOnTrack(), accentDirection);
            energyAccent.Tick();

            energyAmount.color = Color.Lerp(InterfaceText, EnergyFlash, deniedPulse);
            float rounded = Mathf.Round(vitals.Energy * 10f) * .1f;
            if (displayedEnergy == rounded && displayedMaxEnergy == maximum) return;
            displayedEnergy = rounded;
            displayedMaxEnergy = maximum;
            energyAmount.text = $"{rounded:0.0}<color=#8CA6B8> / {maximum}</color>";
        }

        private void UpdateVital(HudMeter meter, double value, double previous, int maximum, double decay)
        {
            float fraction = maximum > 0 ? (float)(value / maximum) : 0f;
            bool damaged = previous - value > decay + 0.00001;
            if (damaged || value > previous)
            {
                meter.LastImpact = Time.unscaledTime;
                var position = new Vector2(meter.Track.rect.width * (fraction - 0.5f), 0f);
                PlayHudBurst(meter.Burst, position, Color.Lerp(meter.RestFrame, Color.white, 0.65f));
            }
            if (damaged && maximum > 0)
            {
                meter.LossTime = Time.unscaledTime;
                meter.LossStart = (float)(previous / maximum);
            }
            float phase = Mathf.Clamp01((Time.unscaledTime - meter.LossTime - 0.09f) / 0.31f);
            float trail = meter.LossStart > fraction && phase < 1f
                ? Mathf.Lerp(meter.LossStart, fraction, Mathf.SmoothStep(0f, 1f, phase)) : fraction;
            // Apply losses immediately so current health stays truthful. Ease only recovery and block gains.
            meter.DisplayedFraction = Mathf.Min(fraction,
                Mathf.MoveTowards(meter.DisplayedFraction, fraction, Time.unscaledDeltaTime * 6f));
            SetSpan(meter.Fill, 0f, meter.DisplayedFraction);
            SetSpan(meter.Lost, fraction, trail);
            float flash = 1f - Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - meter.LastImpact) / .19f);
            meter.Frame.color = Color.Lerp(meter.RestFrame, Color.white, flash * .19f);
            UpdateReadout(meter, value, maximum, "0.#");
            UpdateHudBurst(meter.Burst);
        }

        private void UpdateCombatHudMotion()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < slots.Length; i++)
            {
                var card = i < batch.Queue.Count ? batch.Queue[i] : null;
                if (animatedQueue[i] != card)
                {
                    animatedQueue[i] = card;
                    queueChangedAt[i] = now;
                }
                float settle = Mathf.SmoothStep(0f, 1f, (now - queueChangedAt[i]) / .19f);
                Color frame = i == 0 ? InterfaceCyan : OccupiedSocket;
                slotFrames[i].color = card == null ? EmptySocket : Color.Lerp(InterfaceText, frame, settle);
                slots[i].localScale = Vector3.one * (card == null ? 1f : Mathf.Lerp(1.085f, 1f, settle));
            }
            for (int i = 0; i < previewSlots.Length; i++)
            {
                var card = batch.Upcoming(i);
                if (animatedPreview[i] != card)
                {
                    animatedPreview[i] = card;
                    previewChangedAt[i] = now + i * .024f;
                }
                float settle = Mathf.SmoothStep(0f, 1f, (now - previewChangedAt[i]) / .15f);
                previewSlots[i].localScale = Vector3.one * (card == null ? 1f : Mathf.Lerp(.91f, 1f, settle));
            }

            if (animatedPile != shownPile)
            {
                animatedPile = shownPile;
                pileOpenedAt = now;
            }
            if (shownPile >= 0)
            {
                float reveal = Mathf.SmoothStep(0f, 1f, (now - pileOpenedAt) / .14f);
                pilePanel.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, reveal);
                pilePanel.anchoredPosition = new Vector2(pilePanel.anchoredPosition.x, Mathf.Lerp(319f, 325f, reveal));
                pileFade.alpha = reveal;
            }

            bool ready = actionButton.interactable;
            if (ready && !executeWasReady) executeReadyAt = now;
            executeWasReady = ready;
            float state = ready ? Mathf.SmoothStep(0f, 1f, (now - executeReadyAt) / .24f) : 0f;
            float progress = batch.Queue.Count / (float)CardBatch.Size;
            executeRail.rectTransform.anchorMax = new Vector2(Mathf.Lerp(.12f, 1f, progress), 0f);
            executeRail.color = ready ? Color.Lerp(InterfaceText, InterfaceCyan, state)
                : batch.Busy ? OccupiedSocket : EmptySocket;
            actionButton.targetGraphic.color = ready ? Color.Lerp(InterfaceText, OccupiedSocket, state) : QueueFrame;
            actionText.color = ready ? InterfaceText : InterfaceMuted;
            float statusRemaining = statusUntil - now;
            bool showStatus = statusRemaining > 0f;
            if (statusPlate.gameObject.activeSelf != showStatus) statusPlate.gameObject.SetActive(showStatus);
            if (showStatus)
            {
                float entered = Mathf.SmoothStep(0f, 1f, (1.5f - statusRemaining) / .1f);
                statusFade.alpha = entered * Mathf.Clamp01(statusRemaining / .22f);
                statusPlate.anchoredPosition = new Vector2(0f, Mathf.Lerp(17f, 22f, entered));
            }
        }

        private static void UpdateReadout(HudMeter meter, double value, int maximum, string format)
        {
            double rounded = System.Math.Round(value, 1);
            if (meter.DisplayedValue == rounded && meter.DisplayedMaximum == maximum) return;
            meter.DisplayedValue = rounded;
            meter.DisplayedMaximum = maximum;
            meter.Amount.text = $"{rounded.ToString(format)}<color=#A2B1BD> / {maximum}</color>";
        }
    }
}
