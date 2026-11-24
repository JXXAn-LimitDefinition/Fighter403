using System;
using System.Collections.Generic;
using Fighter403.Audio;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fighter403
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(25)]
    internal sealed class DungeonBreakableProp : MonoBehaviour
    {
        private const float RecoilDuration = .22f;
        private const float BreakDuration = .52f;
        private const float Pixel = 1f / DungeonRoomArt.PixelsPerUnit;
        private const int DebrisOrder = -17;

        private sealed class Appearance
        {
            internal readonly RectInt[] Chips;
            internal readonly Color Signal;
            internal readonly Sfx HitSound, BreakSound;
            internal readonly float Spread;
            internal readonly Vector2? Lamp;

            internal Appearance(RectInt[] chips, Color signal, Sfx hitSound, Sfx breakSound, float spread,
                Vector2? lamp = null)
            {
                Chips = chips;
                Signal = signal;
                HitSound = hitSound;
                BreakSound = breakSound;
                Spread = spread;
                Lamp = lamp;
            }
        }

        private static readonly Appearance Crate = new(
            new RectInt[] { new(12, 1, 19, 9), new(24, 12, 20, 10), new(3, 18, 18, 9), new(25, 23, 16, 7) },
            new Color32(244, 179, 103, 255), Sfx.ProjectileWall, Sfx.ClusterBreak, 1f, new Vector2(-4f, 10f));
        private static readonly Appearance Capacitor = new(
            new RectInt[] { new(5, 6, 8, 15), new(22, 6, 8, 15), new(2, 26, 15, 8), new(19, 27, 15, 7) },
            new Color32(110, 230, 224, 255), Sfx.ClusterHit, Sfx.EnemyShieldBreak, 1f, new Vector2(0f, 13f));
        private static readonly Appearance Box = new(
            new RectInt[] { new(4, 8, 9, 8), new(16, 7, 12, 9), new(6, 19, 9, 7), new(18, 19, 9, 7) },
            new Color32(236, 183, 121, 255), Sfx.ProjectileWall, Sfx.ClusterBreak, .68f);
        private static readonly Appearance Bottle = new(
            new RectInt[] { new(13, 5, 6, 6), new(10, 12, 5, 8), new(16, 12, 6, 8), new(13, 21, 7, 6) },
            new Color32(121, 241, 228, 255), Sfx.ProjectileWall, Sfx.Fracture, .82f);
        private static readonly Appearance Canister = new(
            new RectInt[] { new(8, 7, 9, 6), new(17, 8, 8, 8), new(8, 16, 8, 9), new(16, 18, 8, 9) },
            new Color32(238, 156, 103, 255), Sfx.ClusterHit, Sfx.ProjectileWall, .73f);
        private static readonly Appearance Vial = new(
            new RectInt[] { new(10, 6, 5, 8), new(10, 15, 5, 8), new(16, 15, 6, 8), new(11, 23, 10, 5) },
            new Color32(192, 151, 241, 255), Sfx.ProjectileWall, Sfx.Fracture, .83f);
        private static readonly Appearance Battery = new(
            new RectInt[] { new(7, 12, 10, 7), new(18, 12, 9, 7), new(6, 20, 9, 7), new(17, 20, 9, 7) },
            new Color32(242, 205, 122, 255), Sfx.ClusterHit, Sfx.ClusterBreak, .68f);
        private static readonly Appearance Jar = new(
            new RectInt[] { new(11, 7, 10, 7), new(8, 14, 8, 8), new(16, 14, 8, 8), new(11, 22, 10, 6) },
            new Color32(224, 213, 186, 255), Sfx.ProjectileWall, Sfx.Fracture, .72f);
        private static readonly Appearance Case = new(
            new RectInt[] { new(8, 12, 9, 8), new(17, 12, 10, 8), new(5, 20, 10, 7), new(17, 20, 10, 7) },
            new Color32(174, 205, 179, 255), Sfx.ClusterHit, Sfx.ProjectileWall, .65f);
        private static readonly Appearance Cartridge = new(
            new RectInt[] { new(8, 10, 9, 8), new(17, 11, 9, 8), new(7, 20, 9, 7), new(16, 20, 9, 7) },
            new Color32(233, 155, 185, 255), Sfx.ProjectileWall, Sfx.ClusterBreak, .78f);
        private static readonly Appearance Gauge = new(
            new RectInt[] { new(9, 7, 9, 8), new(17, 9, 8, 9), new(8, 15, 9, 8), new(13, 22, 10, 6) },
            new Color32(140, 208, 245, 255), Sfx.ClusterHit, Sfx.ClusterBreak, .77f);

        private static readonly Vector2[] ChipScatter = { new(-20f, 5f), new(20f, 5f), new(-12f, -10f), new(12f, -11f) };
        private static readonly float[] ChipAngles = { -32f, 38f, 18f, -24f };
        private static readonly Vector2[] SparkScatter =
        {
            new(.22f, .04f), new(.36f, .18f), new(.29f, -.21f), new(.49f, -.07f),
            new(.33f, .32f), new(-.16f, -.18f), new(-.12f, .20f),
        };
        internal static readonly List<DungeonBreakableProp> Active = new();
        private static Sprite pixel;

        private SpriteRenderer source, body, status;
        private SortingGroup casingOrder;
        private SpriteRenderer[] chips, sparks;
        private int durability, maximumDurability;
        private Appearance appearance;
        private Action<int> durabilityChanged;
        private Vector2 recoilDirection;
        private float hitAt = float.NegativeInfinity;

        internal bool IsIntact => isActiveAndEnabled && durability > 0;
        internal BoxCollider2D Body { get; private set; }

        internal void Initialize(SpriteRenderer sprite, BoxCollider2D solid, int durability,
            Action<int> onDurabilityChanged, int maximumDurability = 0)
        {
            source = sprite;
            Body = solid;
            this.durability = Mathf.Max(0, durability);
            this.maximumDurability = Mathf.Max(this.durability, maximumDurability);
            durabilityChanged = onDurabilityChanged;
            appearance = sprite.sprite.name switch
            {
                "CapacitorTower" => Capacitor,
                "SupplyBox" => Box,
                "GlassBottle" => Bottle,
                "PartsCanister" => Canister,
                "SampleVial" => Vial,
                "BatteryCell" => Battery,
                "CeramicJar" => Jar,
                "ToolCase" => Case,
                "DataCartridge" => Cartridge,
                "SignalGauge" => Gauge,
                _ => Crate,
            };

            body = CreatePart("Breakable casing", sprite.sprite, transform);
            casingOrder = body.gameObject.AddComponent<SortingGroup>();
            body.sortingOrder = 0;
            if (appearance.Lamp is Vector2 lamp)
            {
                status = CreatePart("Casing status light", PixelSprite(), body.transform);
                status.sortingOrder = 1;
                status.transform.localPosition = lamp * Pixel;
                status.transform.localScale = new Vector3(6f * Pixel, 3f * Pixel, 1f);
            }
            source.enabled = false;
            if (this.durability == 0)
            {
                DisableCasing();
                CreateChips();
            }
            else if (isActiveAndEnabled)
            {
                Active.Add(this);
            }
            LateUpdate();
        }

        private void OnEnable()
        {
            if (IsIntact) Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);

        internal void GetHurtCircle(out Vector2 center, out float radius)
        {
            center = transform.TransformPoint(Body.offset);
            radius = ((Vector2)transform.TransformVector(Body.size * .5f)).magnitude;
        }

        internal bool Hit(int damage, Vector2 direction)
        {
            if (!IsIntact || damage <= 0) return false;
            durability = Mathf.Max(0, durability - damage);
            hitAt = Time.unscaledTime;
            Vector2 away = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.up;
            recoilDirection = transform.InverseTransformDirection(away);
            if (durability == 0)
            {
                Active.Remove(this);
                DisableCasing();
                CreateChips();
                GameSfx.Play(appearance.BreakSound, .66f);
            }
            else
            {
                GameSfx.Play(appearance.HitSound, .52f);
            }

            CreateSparks();
            LateUpdate();
            durabilityChanged?.Invoke(durability);
            return true;
        }

        private void DisableCasing()
        {
            Body.enabled = false;
            body.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (body == null) return;
            float elapsed = Time.unscaledTime - hitAt;
            bool intact = durability > 0;
            if (intact)
            {
                float settle = 1f - Mathf.Clamp01(elapsed / RecoilDuration);
                float recoil = settle > 0f ? settle * Mathf.Cos(19f * elapsed) : 0f;
                body.transform.localPosition = recoilDirection * (recoil * 3.5f * Pixel);
                body.transform.localRotation = Quaternion.Euler(0f, 0f, recoil * recoilDirection.x * -5f);
                Color casing = durability < maximumDurability ? new Color(.67f, .72f, .76f) : Color.white;
                float light = Mathf.Pow(settle, 3f);
                body.color = Color.Lerp(casing, appearance.Signal, light * .68f);
                if (status != null)
                {
                    float flicker = .78f + .22f * Mathf.Sin(Time.unscaledTime * 4f + transform.position.x);
                    status.color = durability < maximumDurability
                        ? new Color(1f, .53f, .32f, .7f * flicker) : Color.Lerp(appearance.Signal, Color.white, .17f * flicker);
                }
                casingOrder.sortingLayerID = source.sortingLayerID;
                casingOrder.sortingOrder = source.sortingOrder;
            }
            else
            {
                MoveChips(Mathf.Clamp01(elapsed / BreakDuration));
                if (elapsed >= BreakDuration) enabled = false;
            }

            if (sparks != null) MoveSparks(elapsed / (intact ? .22f : .39f), intact);
        }

        private void CreateChips()
        {
            RectInt[] crops = appearance.Chips;
            chips = new SpriteRenderer[crops.Length];
            for (int i = 0; i < chips.Length; i++)
                chips[i] = CreatePart("Casing debris", DungeonRoomArt.CropSprite(source.sprite, crops[i]), transform);
        }

        private void MoveChips(float progress)
        {
            float ease = 1f - Mathf.Pow(1f - progress, 3f);
            float rise = Mathf.Sin(Mathf.PI * progress);
            var origin = Body.offset - Vector2.up * (3f * Pixel);
            RectInt[] crops = appearance.Chips;
            Sprite sprite = source.sprite;
            for (int i = 0; i < chips.Length; i++)
            {
                var chip = chips[i];
                // A brief outward kick settles into the same floor arrangement when the room reloads.
                Vector2 crop = crops[i].center;
                Vector2 start = new Vector2(crop.x - sprite.pivot.x, sprite.rect.height - sprite.pivot.y - crop.y) * Pixel;
                Vector2 destination = origin + ChipScatter[i] * (Pixel * appearance.Spread);
                chip.transform.localPosition = Vector2.LerpUnclamped(start, destination, ease)
                    + recoilDirection * (rise * 10f * Pixel * appearance.Spread)
                    + Vector2.up * (rise * (7f + i * 2f) * Pixel * appearance.Spread);
                chip.transform.localRotation = Quaternion.Euler(0f, 0f, ChipAngles[i] * ease + 55f * rise);
                chip.color = Color.Lerp(Color.white, new Color(.58f, .64f, .71f), progress);
                SetOrder(chip, progress < .82f ? source.sortingOrder : DebrisOrder);
            }
        }

        private void CreateSparks()
        {
            if (sparks != null) return;
            sparks = new SpriteRenderer[SparkScatter.Length];
            for (int i = 0; i < sparks.Length; i++)
                sparks[i] = CreatePart("Impact spark", PixelSprite(), transform);
        }

        private void MoveSparks(float progress, bool intact)
        {
            Vector2 tangent = new(-recoilDirection.y, recoilDirection.x);
            float life = Mathf.Clamp01(1f - progress);
            float ease = 1f - .88f * life * life;
            Vector2 origin = Body.offset + Vector2.up * (10f * Pixel) - recoilDirection * (5f * Pixel);
            SetSparksVisible(life > 0f);
            if (life == 0f) return;

            for (int i = 0; i < sparks.Length; i++)
            {
                var spark = sparks[i];
                Vector2 travel = recoilDirection * SparkScatter[i].x + tangent * SparkScatter[i].y;
                float reach = intact ? .75f : 1.65f;
                spark.transform.localPosition = origin + travel * (ease * reach * appearance.Spread);
                spark.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg);
                spark.transform.localScale = new Vector3((2f + 3f * life) * Pixel, (i % 3 == 0 ? 2f : 1f) * Pixel, 1f);
                Color tint = Color.Lerp(appearance.Signal, Color.white, life * .8f);
                tint.a = life;
                spark.color = tint;
                SetOrder(spark, source.sortingOrder + 2);
            }
        }

        private void SetSparksVisible(bool visible)
        {
            foreach (var spark in sparks) spark.enabled = visible;
        }

        private SpriteRenderer CreatePart(string label, Sprite sprite, Transform parent)
        {
            var part = new GameObject(label);
            part.layer = gameObject.layer;
            part.transform.SetParent(parent, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sprite = sprite;
            SetOrder(renderer, source.sortingOrder);
            return renderer;
        }

        private void SetOrder(SpriteRenderer renderer, int order)
        {
            if (renderer.sortingLayerID != source.sortingLayerID) renderer.sortingLayerID = source.sortingLayerID;
            if (renderer.sortingOrder != order) renderer.sortingOrder = order;
        }

        private static Sprite PixelSprite()
        {
            if (pixel != null) return pixel;
            var texture = Texture2D.whiteTexture;
            pixel = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.one * .5f,
                texture.width, 0, SpriteMeshType.FullRect);
            pixel.hideFlags = HideFlags.HideAndDontSave;
            return pixel;
        }
    }
}
