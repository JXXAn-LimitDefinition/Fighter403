using UnityEngine;

namespace Fighter403
{
    internal sealed class DungeonAbyss : MonoBehaviour
    {
        private static readonly int RoomBounds = Shader.PropertyToID("_RoomBounds");
        private static readonly int AbyssTime = Shader.PropertyToID("_AbyssTime");
        private static readonly int SeedPhase = Shader.PropertyToID("_SeedPhase");
        private static readonly int RoomSeed = Shader.PropertyToID("_RoomSeed");
        private static readonly int CodeAtlas = Shader.PropertyToID("_CodeAtlas");
        private static readonly int AbyssInk = Shader.PropertyToID("_AbyssInk");
        private static readonly int WallInk = Shader.PropertyToID("_WallInk");
        private static readonly (string Atlas, Vector3 DeepInk, Vector3 WallInk)[] FloorLooks =
        {
            ("CodeAbyss", new Vector3(.016f, .024f, .045f), new Vector3(.027f, .049f, .084f)),
            ("CodeAbyssFirewall", new Vector3(.035f, .022f, .015f), new Vector3(.075f, .043f, .023f)),
            ("CodeAbyssCore", new Vector3(.040f, .016f, .023f), new Vector3(.086f, .029f, .039f)),
        };
        private static readonly Material[] Materials = new Material[FloorLooks.Length];
        private static Sprite quad;

        private SpriteRenderer surface;
        private MaterialPropertyBlock properties;
        private int lastFrame = -1;

        internal static void Attach(Transform room, Vector2 halfSize, int artSeed, int floor)
        {
            var material = MaterialFor(floor);
            if (material == null) return;
            if (quad == null)
            {
                var white = Texture2D.whiteTexture;
                quad = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height), Vector2.one * .5f,
                    white.width, 0, SpriteMeshType.FullRect);
                quad.hideFlags = HideFlags.HideAndDontSave;
            }

            // The camera can frame the whole boss arena and pan past its large outer walls.
            var background = new GameObject("Code abyss");
            background.transform.SetParent(room, false);
            background.transform.localScale = halfSize * 2f + Vector2.one * 160f;
            var sprite = background.AddComponent<SpriteRenderer>();
            sprite.sprite = quad;
            sprite.sharedMaterial = material;
            sprite.sortingOrder = -100;

            var abyss = background.AddComponent<DungeonAbyss>();
            abyss.surface = sprite;
            abyss.properties = new MaterialPropertyBlock();
            abyss.properties.SetVector(RoomBounds, new Vector4(room.position.x, room.position.y, halfSize.x, halfSize.y));
            abyss.properties.SetFloat(SeedPhase, (artSeed & 1023) * .047f);
            abyss.properties.SetVector(RoomSeed, new Vector4(artSeed & 255, (artSeed >> 8) & 255,
                (artSeed >> 16) & 255, (artSeed >> 24) & 255));
            abyss.LateUpdate();
        }

        private static Material MaterialFor(int floor)
        {
            int index = Mathf.Clamp(floor, 0, FloorLooks.Length - 1);
            var material = Materials[index];
            if (material != null) return material;

            var look = FloorLooks[index];
            var shader = Resources.Load<Shader>("Fighter403DungeonAbyss");
            var atlas = Resources.Load<Texture2D>($"{DungeonRoomArt.ResourceFolder}/{look.Atlas}");
            if (shader == null || atlas == null) return null;

            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetTexture(CodeAtlas, atlas);
            material.SetVector(AbyssInk, look.DeepInk);
            material.SetVector(WallInk, look.WallInk);
            Materials[index] = material;
            return material;
        }

        private void LateUpdate()
        {
            // Keep the abyss running in planning mode and update on the PixelFX atlas cadence.
            int frame = Mathf.FloorToInt(Time.unscaledTime * 10f);
            if (frame == lastFrame || surface == null) return;
            lastFrame = frame;
            properties.SetFloat(AbyssTime, frame * .1f);
            surface.SetPropertyBlock(properties);
        }
    }
}
