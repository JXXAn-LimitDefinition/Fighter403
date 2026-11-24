using Fighter403.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Fighter403.UI
{
    public sealed partial class CardHandController
    {
        private const float BossHudWidth = 720f, BossHudHeight = 74f, BossHudMargin = 16f;
        private const float BossHealthTrackHeight = 18f;
        private const float BossHealthTrackBottom = 18f;
        private const float BossTrackInset = 35f;
        private static readonly Color BossHealthColor = new Color32(219, 81, 99, 255);
        private static readonly Color BossHealthEdge = new Color32(255, 161, 145, 255);
        private static readonly Color32 BossReadout = new(250, 222, 222, 255);

        private CombatTarget bossHudTarget;
        private RectTransform bossHudPanel, bossHealthTrack, bossHealthFill, bossHealthTrail;
        private RectTransform bossShieldTrack, bossShieldFill;
        private Text bossName, bossHealthAmount, bossAttackWarning;
        private HudMeterAccent bossImpact, bossShieldImpact;
        private int displayedBossHealth, displayedBossMaximum, displayedBossShield;
        private float bossTrailFrom, bossTrailStarted;

        private void BuildBossHud()
        {
            var top = new Vector2(.5f, 1f);
            bossHudPanel = Box("Boss health HUD", root, top, Vector2.down * BossHudMargin,
                new Vector2(BossHudWidth, BossHudHeight));
            bossHudPanel.pivot = top;
            var group = bossHudPanel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            bossHudPanel.gameObject.AddComponent<BossHudGeometry>().raycastTarget = false;

            bossName = Label("Boss name", bossHudPanel, new Vector2(0f, 1f), new Vector2(64f, -19f),
                new Vector2(430f, 34f), 30);
            bossName.rectTransform.pivot = new Vector2(0f, .5f);
            bossName.alignment = TextAnchor.MiddleLeft;
            bossName.color = InterfaceText;
            bossName.fontStyle = FontStyle.Bold;

            bossAttackWarning = Label("Boss attack warning", bossHudPanel, new Vector2(0f, 1f),
                new Vector2(500f, -19f), new Vector2(226f, 28f), 22);
            bossAttackWarning.rectTransform.pivot = new Vector2(1f, .5f);
            bossAttackWarning.alignment = TextAnchor.MiddleRight;
            bossAttackWarning.color = new Color32(255, 193, 109, 255);
            bossAttackWarning.fontStyle = FontStyle.Bold;
            bossAttackWarning.text = "Stop-the-World";
            bossAttackWarning.gameObject.SetActive(false);

            bossHealthAmount = Label("Boss health amount", bossHudPanel, Vector2.one, new Vector2(-43f, -19f),
                new Vector2(155f, 30f), 24);
            bossHealthAmount.rectTransform.pivot = new Vector2(1f, .5f);
            bossHealthAmount.alignment = TextAnchor.MiddleRight;
            bossHealthAmount.color = BossReadout;
            bossHealthAmount.fontStyle = FontStyle.Bold;

            bossHealthTrack = BuildBossHudTrack("Boss health track", BossHealthTrackBottom, BossHealthTrackHeight,
                new Color32(53, 30, 43, 255));
            bossHealthFill = PaintFill("Boss health fill", bossHealthTrack, BossHealthColor, BossHealthEdge);
            bossHealthTrail = PaintFill("Boss damage trail", bossHealthTrack, new Color32(166, 104, 95, 255),
                new Color32(240, 171, 130, 255));
            var scale = Box("Boss health scale", bossHealthTrack, Vector2.zero, Vector2.zero, Vector2.zero);
            scale.anchorMax = Vector2.one;
            scale.offsetMin = scale.offsetMax = Vector2.zero;
            var scaleGeometry = scale.gameObject.AddComponent<BossHudGeometry>();
            scaleGeometry.DrawScale = true;
            scaleGeometry.raycastTarget = false;
            bossImpact = HudMeterAccent.Create(bossHealthTrack, new Color32(255, 120, 108, 255));

            bossShieldTrack = BuildBossHudTrack("Boss shield track", 8f, 5f, new Color32(44, 39, 62, 255));
            bossShieldFill = PaintFill("Boss shield fill", bossShieldTrack, new Color32(144, 120, 208, 255),
                new Color32(204, 183, 248, 255));
            var shieldOrigin = Box("Shield contact", bossShieldTrack, new Vector2(0f, .5f), new Vector2(-7f, 0f),
                new Vector2(4f, 4f));
            shieldOrigin.localEulerAngles = new Vector3(0f, 0f, 45f);
            Paint(shieldOrigin, new Color32(173, 151, 225, 255), false);
            bossShieldImpact = HudMeterAccent.Create(bossShieldTrack, new Color32(188, 151, 255, 255),
                new Vector2(32f, 14f));
            bossHudPanel.gameObject.SetActive(false);
        }

        private RectTransform BuildBossHudTrack(string name, float bottom, float height, Color color)
        {
            var track = Box(name, bossHudPanel, Vector2.zero, Vector2.zero, Vector2.zero);
            track.anchorMax = Vector2.right;
            track.offsetMin = new Vector2(BossTrackInset, bottom);
            track.offsetMax = new Vector2(-BossTrackInset, bottom + height);
            Paint(track, color, false);
            return track;
        }

        private void SetBossHudTarget(CombatTarget target)
        {
            if (ReferenceEquals(bossHudTarget, target)) return;
            if (bossHudTarget != null) bossHudTarget.SetWorldHealthVisible(true);
            bossHudTarget = target;
            bossImpact?.Hide();
            bossShieldImpact?.Hide();
            if (bossHudPanel != null) bossHudPanel.gameObject.SetActive(false);
            if (target == null) return;

            bossName.text = target.DisplayName;
            displayedBossHealth = displayedBossMaximum = -1;
            bool hasShield = target is ProgramEnemy enemy && enemy.ShieldCapacity > 0;
            bossShieldTrack.gameObject.SetActive(hasShield);
            float healthTrackBottom = hasShield ? BossHealthTrackBottom : BossHealthTrackBottom - 4f;
            bossHealthTrack.anchoredPosition = Vector2.up * (healthTrackBottom + BossHealthTrackHeight * .5f);
            UpdateBossHud();
        }

        private void UpdateBossHud()
        {
            CombatTarget boss = bossHudTarget;
            if (boss == null || !boss.Alive || (dungeonRoom != null && dungeonLost))
            {
                bossImpact.Hide();
                bossShieldImpact.Hide();
                bossHudPanel.gameObject.SetActive(false);
                return;
            }

            if (!bossHudPanel.gameObject.activeSelf)
            {
                boss.SetWorldHealthVisible(false);
                bossTrailFrom = (float)boss.Health / boss.MaxHealth;
                displayedBossHealth = displayedBossMaximum = displayedBossShield = -1;
                bossImpact.Hide();
                bossShieldImpact.Hide();
                bossHudPanel.gameObject.SetActive(true);
            }

            bossAttackWarning.gameObject.SetActive(boss is GarbageCollectorBoss { StopWarningActive: true });
            float fraction = (float)boss.Health / boss.MaxHealth;
            float settle = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - bossTrailStarted - .07f) / .25f);
            float trail = Mathf.Lerp(Mathf.Max(fraction, bossTrailFrom), fraction, settle);
            if (displayedBossHealth != boss.Health || displayedBossMaximum != boss.MaxHealth)
            {
                if (displayedBossMaximum == boss.MaxHealth && boss.Health < displayedBossHealth)
                {
                    trail = Mathf.Max(trail, (float)displayedBossHealth / boss.MaxHealth);
                    bossTrailFrom = trail;
                    bossTrailStarted = Time.unscaledTime;
                    bossImpact.Play(fraction, -1);
                }
                displayedBossHealth = boss.Health;
                displayedBossMaximum = boss.MaxHealth;
                bossHealthAmount.text = $"{boss.Health} / {boss.MaxHealth}";
            }

            SetSpan(bossHealthFill, 0f, fraction);
            SetSpan(bossHealthTrail, fraction, trail);
            bossImpact.Tick();
            if (boss is ProgramEnemy enemy && enemy.ShieldCapacity > 0)
            {
                int shield = enemy.CurrentShield;
                float shieldFraction = (float)shield / enemy.ShieldCapacity;
                SetSpan(bossShieldFill, 0f, shieldFraction);
                if (displayedBossShield >= 0 && shield < displayedBossShield)
                    bossShieldImpact.Play(shieldFraction, -1);
                displayedBossShield = shield;
                bossShieldImpact.Tick();
            }
            LayoutBossHud();
        }

        private void LayoutBossHud()
        {
            float width = root.rect.width;
            float preferredScale = Mathf.Clamp(width / 1280f, .55f, 1f);
            float scale = Mathf.Min(preferredScale, Mathf.Min(root.rect.height / 720f,
                width / (BossHudWidth + 2f * BossHudMargin)));
            float clearance = BossHudMargin * scale;
            float sideGap = (width - BossHudWidth * scale) * .5f;
            float top = clearance;

            bool dungeonHudVisible = dungeonHud.gameObject.activeSelf;
            RectTransform rightHud = dungeonHudVisible ? dungeonHud : dungeonEntry;
            float rightHudWidth = rightHud.rect.width * rightHud.pivot.x * rightHud.localScale.x
                - rightHud.anchoredPosition.x;
            if (rightHud.gameObject.activeSelf && sideGap < rightHudWidth + clearance)
            {
                float rightHudHeight = dungeonHudVisible && dungeonMap.gameObject.activeSelf
                    ? dungeonMap.rect.height - MapPanelTop
                    : rightHud.rect.height * rightHud.pivot.y;
                float rightHudBottom = -rightHud.anchoredPosition.y + rightHudHeight * rightHud.localScale.y;
                top = Mathf.Max(top, rightHudBottom + clearance);
            }
            bossHudPanel.anchoredPosition = Vector2.down * top;
            bossHudPanel.localScale = Vector3.one * scale;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class BossHudGeometry : MaskableGraphic
    {
        private static readonly Color32 Rim = new(115, 73, 95, 255);
        private static readonly Color32 Body = new(31, 35, 51, 255);
        private static readonly Color32 LowerFace = new(16, 20, 33, 255);
        private static readonly Color32 Shoulder = new(75, 56, 76, 255);
        private static readonly Color32 Contact = new(197, 88, 108, 255);
        private static readonly Color32 ContactLight = new(245, 148, 140, 255);
        private static readonly Color32 Recessed = new(9, 13, 22, 255);

        internal bool DrawScale { get; set; }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            if (DrawScale) PaintScale(mesh, bounds);
            else PaintChassis(mesh, bounds);
        }

        private static void PaintChassis(VertexHelper mesh, Rect bounds)
        {
            Vector2 origin = bounds.min;
            float width = bounds.width;
            float height = bounds.height;
            Polygon(mesh, origin, Rim, new(0f, 40f), new(28f, height), new(width - 28f, height),
                new(width, 40f), new(width, 22f), new(width - 24f, 0f), new(24f, 0f), new(0f, 22f));
            Polygon(mesh, origin, Body, new(2f, 39f), new(29f, height - 2f), new(width - 29f, height - 2f),
                new(width - 2f, 39f), new(width - 2f, 23f), new(width - 25f, 2f), new(25f, 2f), new(2f, 23f));
            Polygon(mesh, origin, LowerFace, new(4f, 39f), new(width - 4f, 39f), new(width - 4f, 23f),
                new(width - 26f, 3f), new(26f, 3f), new(4f, 23f));

            // The recessed rail and darker readout stay distinct from the text and live fill.
            Polygon(mesh, origin, Recessed, new(22f, 25f), new(33f, 39f), new(width - 33f, 39f),
                new(width - 22f, 25f), new(width - 22f, 17f), new(width - 33f, 7f), new(33f, 7f), new(22f, 17f));
            Polygon(mesh, origin, Shoulder, new(width - 211f, 47f), new(width - 199f, 66f),
                new(width - 43f, 66f), new(width - 27f, 49f), new(width - 33f, 43f), new(width - 205f, 43f));
            Polygon(mesh, origin, Recessed, new(width - 208f, 47f), new(width - 198f, 64f),
                new(width - 44f, 64f), new(width - 30f, 49f), new(width - 34f, 44f), new(width - 204f, 44f));
            Strip(mesh, origin, 64f, 39f, width - 218f, 41f, Shoulder);

            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                Vector2 shoulderOrigin = origin + Vector2.right * (left ? 4f : width - 34f);
                float slope = left ? 1f : -1f;
                float x = left ? 0f : 30f;
                Polygon(mesh, shoulderOrigin, Shoulder, new(x, 39f), new(x + slope * 13f, 54f),
                    new(x + slope * 29f, 54f), new(x + slope * 16f, 39f));
                Polygon(mesh, shoulderOrigin, Rim, new(x + slope * 4f, 39f), new(x + slope * 13f, 50f),
                    new(x + slope * 20f, 50f), new(x + slope * 11f, 39f));
                Polygon(mesh, shoulderOrigin, Shoulder, new(x, 39f), new(x + slope * 16f, 39f),
                    new(x + slope * 16f, 26f), new(x, 26f));
                Polygon(mesh, shoulderOrigin, Shoulder, new(x, 26f), new(x + slope * 16f, 26f),
                    new(x + slope * 30f, 12f), new(x + slope * 14f, 12f));
                for (int notch = 0; notch < 2; notch++)
                {
                    float notchX = left ? 9f + notch * 5f : width - 12f - notch * 5f;
                    Strip(mesh, origin, notchX, 28f, notchX + 2.5f, 35f, Contact);
                }
            }

            // A six-sided warning mark sits next to the boss name.
            Polygon(mesh, origin, Contact, new(36f, 45f), new(30f, 55f), new(36f, 65f),
                new(49f, 65f), new(55f, 55f), new(49f, 45f));
            Polygon(mesh, origin, LowerFace, new(37f, 47f), new(32f, 55f), new(37f, 63f),
                new(48f, 63f), new(53f, 55f), new(48f, 47f));
            Strip(mesh, origin, 41f, 54f, 44f, 60f, ContactLight);
            Strip(mesh, origin, 41f, 49f, 44f, 52f, ContactLight);
            Strip(mesh, origin, 31f, height - 4f, 86f, height, Contact);
            Strip(mesh, origin, width - 86f, height - 4f, width - 31f, height, Contact);
            Strip(mesh, origin, 41f, 2f, 87f, 5f, Shoulder);
            Strip(mesh, origin, width - 87f, 2f, width - 41f, 5f, Shoulder);
        }

        private static void PaintScale(VertexHelper mesh, Rect bounds)
        {
            const int divisions = 12;
            for (int i = 1; i < divisions; i++)
            {
                bool quarter = i % 3 == 0;
                float x = Mathf.Round(bounds.width * i / divisions);
                float thickness = quarter ? 2f : 1f;
                Strip(mesh, bounds.min, x - thickness * .5f, 0f, x + thickness * .5f, bounds.height,
                    quarter ? new Color32(19, 16, 26, 235) : new Color32(24, 18, 29, 170));
                if (quarter)
                    Strip(mesh, bounds.min, x - 1.5f, -2f, x + 1.5f, 0f, Rim);
            }
            Strip(mesh, bounds.min, 0f, bounds.height, bounds.width, bounds.height + 1f, Rim);
            Strip(mesh, bounds.min, 0f, -1f, bounds.width, 0f, Recessed);
        }

        private static void Strip(VertexHelper mesh, Vector2 origin, float left, float bottom, float right, float top,
            Color32 tint)
        {
            Polygon(mesh, origin, tint, new(left, bottom), new(left, top), new(right, top), new(right, bottom));
        }

        private static void Polygon(VertexHelper mesh, Vector2 origin, Color32 tint, params Vector2[] points)
        {
            int start = mesh.currentVertCount;
            foreach (var point in points)
                mesh.AddVert(origin + point, tint, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++)
                mesh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
