using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fighter403.Player
{
    internal abstract class DungeonBoss : CombatTarget
    {
        private readonly List<DungeonBossSummon> summons = new();
        private readonly Dictionary<GameObject, Mesh> telegraphs = new();
        private Material markMaterial;
        private Action<string> announce;
        private bool running;

        protected abstract string ArtResource { get; }
        protected abstract int StartingHealth { get; }
        protected abstract float HurtRadius { get; }
        protected RoundPlayerMovement Player { get; private set; }
        protected CombatVitals Vitals { get; private set; }
        protected Vector2 ArenaCenter { get; private set; }
        protected Vector2 ArenaHalfSize { get; private set; }
        protected System.Random Random { get; private set; }
        protected TopDownCameraFollow ShotCamera { get; private set; }
        protected RectTransform Hud { get; private set; }
        protected Sprite SwarmSprite { get; private set; }
        protected SpriteRenderer Body { get; private set; }

        protected override void Awake()
        {
            Body = GetComponent<SpriteRenderer>();
            Body.sprite = Resources.Load<Sprite>(ArtResource);
            Body.sortingOrder = 2;
            base.Awake();
            BodyCollider.radius = HurtRadius;
            SetHealthCapacity(StartingHealth);
            markMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        internal void Initialize(RoundPlayerMovement player, CombatVitals vitals, Vector2 arenaCenter,
            Vector2 arenaHalfSize, int seed, Sprite swarm, TopDownCameraFollow camera, RectTransform hud,
            Action<string> status)
        {
            Player = player;
            Vitals = vitals;
            ArenaCenter = arenaCenter;
            ArenaHalfSize = arenaHalfSize;
            Random = new System.Random(seed);
            SwarmSprite = swarm;
            ShotCamera = camera;
            Hud = hud;
            announce = status;
            running = true;
            BeginEncounter();
        }

        protected abstract void BeginEncounter();
        protected abstract void TickEncounter(float dt);
        protected virtual void OnBossDefeated() { }
        protected virtual void OnEncounterStopped() { }
        protected void Announce(string message) => announce?.Invoke(message);

        private void FixedUpdate()
        {
            if (!running) return;
            if (Player == null || Vitals.Health <= 0)
            {
                StopEncounter();
                return;
            }

            TickEncounter(Time.fixedDeltaTime * BehaviorTimeScale);
        }

        protected override void OnDefeated()
        {
            BodyCollider.enabled = false;
            Body.enabled = false;
            OnBossDefeated();
            StopEncounter();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopEncounter();
        }

        private void OnDestroy()
        {
            if (markMaterial != null) Destroy(markMaterial);
        }

        private void StopEncounter()
        {
            if (!running) return;
            running = false;
            OnEncounterStopped();
            ClearTelegraphs();
            foreach (var summon in summons)
                if (summon != null) summon.Dismiss();
            summons.Clear();
        }

        protected DungeonBossSummon SpawnSummon(string label, Sprite sprite, Vector2 position, int health, float radius,
            bool anchored = false)
        {
            summons.RemoveAll(summon => summon == null);
            var instance = new GameObject(label);
            instance.transform.SetParent(transform.parent, false);
            instance.transform.position = position;
            var summon = instance.AddComponent<DungeonBossSummon>();
            summon.Initialize(label, sprite, health, radius, Body, anchored);
            summons.Add(summon);
            return summon;
        }

        protected GameObject Telegraph(string label, Vector2[] polygon, Color tint, float fillAlpha = .18f)
        {
            var mark = new GameObject(label);
            mark.transform.SetParent(transform.parent, false);
            Vector2 center = Vector2.zero;
            foreach (var vertex in polygon) center += vertex;
            center /= polygon.Length;
            mark.transform.position = center;

            var vertices = new Vector3[polygon.Length];
            var colors = new Color[polygon.Length];
            var triangles = new int[(polygon.Length - 2) * 3];
            Color fill = new(tint.r, tint.g, tint.b, tint.a * fillAlpha);
            for (int i = 0; i < polygon.Length; i++)
            {
                vertices[i] = polygon[i] - center;
                colors[i] = fill;
            }
            for (int i = 1; i < polygon.Length - 1; i++)
            {
                int first = (i - 1) * 3;
                triangles[first] = 0;
                triangles[first + 1] = i;
                triangles[first + 2] = i + 1;
            }

            var mesh = new Mesh { name = label, vertices = vertices, colors = colors, triangles = triangles };
            mark.AddComponent<MeshFilter>().sharedMesh = mesh;
            var render = mark.AddComponent<MeshRenderer>();
            render.sharedMaterial = markMaterial;
            render.sortingLayerID = Body.sortingLayerID;
            render.sortingOrder = -11;
            var outline = mark.AddComponent<LineRenderer>();
            outline.sharedMaterial = markMaterial;
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.positionCount = vertices.Length;
            outline.SetPositions(vertices);
            outline.sortingLayerID = Body.sortingLayerID;
            outline.sortingOrder = -10;
            outline.widthMultiplier = .065f;
            outline.startColor = outline.endColor = tint;
            telegraphs.Add(mark, mesh);
            return mark;
        }

        protected void RemoveTelegraph(GameObject mark)
        {
            if (ReferenceEquals(mark, null) || !telegraphs.Remove(mark, out var mesh)) return;
            DestroyTelegraph(mark, mesh);
        }

        protected void ClearTelegraphs()
        {
            foreach (var entry in telegraphs) DestroyTelegraph(entry.Key, entry.Value);
            telegraphs.Clear();
        }

        private static void DestroyTelegraph(GameObject mark, Mesh mesh)
        {
            if (mark != null)
            {
                mark.SetActive(false);
                Destroy(mark);
            }
            Destroy(mesh);
        }
    }
}
