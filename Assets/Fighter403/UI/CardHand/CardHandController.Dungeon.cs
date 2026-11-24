using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fighter403.Audio;
using Fighter403.Debuggers;
using Fighter403.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fighter403.UI
{
    public sealed partial class CardHandController
    {
        private static readonly Vector2 DungeonCenter = new(80f, 0f);
        private static readonly CardKind[] InitialDeck =
            { CardKind.QuickSlash, CardKind.QuickSlash, CardKind.PhaseDash, CardKind.PhaseDash, CardKind.MatrixGuard };

        [SerializeField, InspectorName("数据注入器")] private DebuggerDefinition dungeonDebugger;
        private DungeonRoom[][] dungeonFloors;
        private DungeonRoom dungeonRoom;
        private GameObject dungeonRoomObject;
        private DungeonRoomActor dungeonActor;
        private Transform dungeonEnemies;
        private DungeonGate[] roomGates;
        private ProgramEnemy rootBoss;
        private float rootSummonAt;
        private CardBatch trainingBatch;
        private DebuggerDefinition[] trainingDebuggers;
        private Vector2 beforeDungeon, roomHalfSize;
        private readonly List<GameObject> hiddenTrainingTargets = new();
        private int dungeonSeed, dungeonFloor, fragments;
        private bool dungeonFinished, dungeonLost, dungeonFreeHand;
        private Image dungeonRoomCurtain;

        private bool DungeonRoomTransitioning => dungeonRoomCurtain != null && dungeonRoomCurtain.gameObject.activeSelf;

        private bool NearBreakableProp()
        {
            const float revealDistance = 1.6f;
            Vector2 position = movement.Position;
            foreach (var prop in DungeonBreakableProp.Active)
            {
                if (!prop.IsIntact) continue;
                Vector2 offset = prop.Body.ClosestPoint(position) - position;
                if (offset.sqrMagnitude <= revealDistance * revealDistance) return true;
            }
            return false;
        }

        private CardDefinition DefinitionFor(CardKind kind) => Array.Find(cardPrefabs, card => card != null && card.Kind == kind);

        private IEnumerable<ProgramEnemy> DungeonPrefabs()
        {
            if (meleePrefab != null) yield return meleePrefab;
            foreach (var prefab in additionalEnemyPrefabs ?? Array.Empty<ProgramEnemy>())
                if (prefab != null) yield return prefab;
            foreach (var prefab in eliteEnemyPrefabs ?? Array.Empty<ProgramEnemy>())
                if (prefab != null) yield return prefab;
        }

        private bool BeginDungeon(string seedText) => BeginDungeonRun(seedText, playTraverseCue: true);

        private bool BeginDungeonRun(string seedText, bool playTraverseCue)
        {
            if (batch.Busy || dungeonFloors != null) return false;
            int seed = string.IsNullOrWhiteSpace(seedText) ? Environment.TickCount : DungeonGenerator.ParseSeed(seedText);
            var layout = Enumerable.Range(0, DungeonGenerator.FloorCount).Select(i => DungeonGenerator.Generate(seed, i)).ToArray();
            var deck = InitialDeck.Select(DefinitionFor).ToArray();
            var available = DungeonPrefabs().Select(prefab => prefab.Role).ToHashSet();
            if (deck.Any(card => card == null) || firewallBossPrefab == null ||
                layout.SelectMany(floor => floor).SelectMany(room => room.Enemies).Any(role => !available.Contains(role)) ||
                !available.Contains(EnemyRole.Swarm))
            {
                dungeonMenuText.text = "地牢缺少已配置的卡牌或敌人预制体。";
                GameSfx.Play(Sfx.DungeonDenied);
                return false;
            }
            for (int floor = 0; floor < layout.Length; floor++)
            {
                var art = new DungeonRoomArt(floor, DungeonRoomKind.Start, 14, 8, 0, new DungeonDoorState[4]);
                if (art.Ready) continue;
                dungeonMenuText.text = "地牢图集导入尚未完成。";
                GameSfx.Play(Sfx.DungeonDenied);
                return false;
            }

            ClearBetweenDungeonRooms();
            CloseFirewallTrial(restorePosition: true);
            beforeDungeon = movement.Position;
            foreach (var target in CombatTarget.Active.ToArray())
            {
                if (target == null) continue;
                hiddenTrainingTargets.Add(target.gameObject);
                target.gameObject.SetActive(false);
            }
            trainingBatch = batch;
            trainingDebuggers = DebuggersManager.Snapshot();
            var vitals = new CombatVitals(movement.BaseStats);
            ChangeBatch(new CardBatch(new System.Random(seed), cardPrefabs, vitals, deck));
            DebuggersManager.BeginRun();
            dungeonSeed = seed;
            dungeonFloor = fragments = 0;
            dungeonFinished = dungeonLost = dungeonFreeHand = false;
            dungeonFloors = layout;
            resetButton.gameObject.SetActive(false);
            dungeonMapVisible = true;
            CloseDungeonMenu();
            EnterDungeonRoom(layout[0][0], -1);
            PlayMusic(CombatMusicPath);
            if (playTraverseCue) GameSfx.Play(Sfx.DungeonTraverse);
            return true;
        }

        private void ChangeBatch(CardBatch next)
        {
            batch.Vitals.Impact -= playerImpact.Play;
            foreach (var view in views.Values)
            {
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }
            views.Clear();
            batch = next;
            CombatTarget.BattleVitals = batch.Vitals;
            batch.Vitals.Impact += playerImpact.Play;
            Refresh();
        }

        private void ClearBetweenDungeonRooms()
        {
            CancelDungeonActorAnimation();
            CancelCardDrag();
            queueDrag = null;
            if (dashPlans.Count > 0) ClearDashPredictions();
            SetSelectedTarget(null);
            batch.Clear();
            ExpireTemporaryCards();
            timeStopPostProcess?.ClearBigBang();
            SetPile(-1);
            playerImpact?.Clear();
            cameraFollow?.EndTrialFraming();
        }

        private void WarpToDungeon(Vector2 position)
        {
            Vector2 delta = position - movement.Position;
            movement.SetCardPosition(position);
            cameraFollow?.NotifyRoomWarp(movement.transform, delta);
            Physics2D.SyncTransforms();
        }

        private void EndDungeon()
        {
            if (dungeonFloors == null || batch.Busy) return;
            StopMusic();
            ClearBetweenDungeonRooms();
            DestroyDungeonRoom();
            dungeonFloors = null;
            dungeonRoom = null;
            ChangeBatch(trainingBatch);
            trainingBatch = null;
            DebuggersManager.Restore(trainingDebuggers);
            trainingDebuggers = null;
            WarpToDungeon(beforeDungeon);
            foreach (var target in hiddenTrainingTargets)
                if (target != null) target.SetActive(true);
            hiddenTrainingTargets.Clear();
            resetButton.gameObject.SetActive(true);
            CloseDungeonMenu();
            RefreshDungeonUi();
            GameSfx.Play(Sfx.DungeonTraverse, .70f);
        }

        private void DestroyDungeonRoom()
        {
            CancelDungeonActorAnimation();
            dungeonActor = null;
            if (dungeonRoomObject == null) return;
            SetBossHudTarget(null);
            foreach (var enemy in dungeonEnemies.GetComponentsInChildren<ProgramEnemy>(true))
                enemy.RemoveSpawnedFragments();
            dungeonRoomObject.SetActive(false);
            Destroy(dungeonRoomObject);
            dungeonRoomObject = null;
            rootBoss = null;
        }

        private void EnterDungeonRoom(DungeonRoom room, int entrance)
        {
            ClearBetweenDungeonRooms();
            DestroyDungeonRoom();
            dungeonRoom = room;
            room.Visited = true;
            bool bossRoom = room.Kind == DungeonRoomKind.Boss;
            if (room.Kind != DungeonRoomKind.Combat && room.Kind != DungeonRoomKind.Elite && !bossRoom)
                room.Cleared = true;
            var art = new DungeonRoomArt(dungeonFloor, room.Kind, bossRoom ? 28 : 14, bossRoom ? 18 : 8,
                room.ArtSeed, Enumerable.Range(0, 4).Select(DungeonGateState).ToArray());
            roomHalfSize = art.HalfSize;
            dungeonRoomObject = art.Create(DungeonGenerator.RoomLabels[(int)room.Kind], DungeonCenter, room, OnDungeonBreach,
                movement.GetComponent<SpriteRenderer>());
            DungeonAbyss.Attach(dungeonRoomObject.transform, roomHalfSize, room.ArtSeed, dungeonFloor);
            dungeonActor = dungeonRoomObject.GetComponentInChildren<DungeonRoomActor>();
            roomGates = dungeonRoomObject.GetComponentsInChildren<DungeonGate>();
            dungeonEnemies = new GameObject("Room enemies").transform;
            dungeonEnemies.SetParent(dungeonRoomObject.transform, false);
            Vector2 from = entrance < 0 ? Vector2.zero : DungeonGenerator.Directions[entrance];
            Vector2 spawn = Vector2.Scale(from, roomHalfSize - Vector2.one * 2.15f);
            WarpToDungeon(DungeonCenter + spawn);
            if (!room.Cleared) SpawnDungeonEncounter(spawn);
            Refresh();
            RefreshDungeonUi();
        }

        private void TransitionToDungeonRoom(DungeonRoom room, int entrance, int nextFloor)
        {
            if (DungeonRoomTransitioning) return;
            if (dungeonRoomCurtain == null)
            {
                var overlay = Box("Room blackout", root, Vector2.zero, Vector2.zero, Vector2.zero);
                overlay.anchorMax = Vector2.one;
                overlay.offsetMin = overlay.offsetMax = Vector2.zero;
                var overlayCanvas = overlay.gameObject.AddComponent<Canvas>();
                overlayCanvas.overrideSorting = true;
                overlayCanvas.sortingOrder = 100;
                overlay.gameObject.AddComponent<GraphicRaycaster>();
                dungeonRoomCurtain = Paint(overlay, Color.clear, true);
            }
            dungeonRoomCurtain.gameObject.SetActive(true);
            UpdatePlanningState();
            SetClock(0f);
            GameSfx.Play(nextFloor > dungeonFloor ? Sfx.DungeonDescent : Sfx.DungeonTraverse);
            StartCoroutine(PlayDungeonRoomTransition(room, entrance, nextFloor));
        }

        private IEnumerator PlayDungeonRoomTransition(DungeonRoom room, int entrance, int nextFloor)
        {
            yield return FadeDungeonRoomCurtain(0f, 1f, .12f);
            // Present a fully black frame before replacing the room and warping the camera.
            yield return null;
            if (nextFloor > dungeonFloor)
            {
                dungeonRoom.Claimed = true;
                batch.Vitals.HealPercent(.50f);
            }
            dungeonFloor = nextFloor;
            EnterDungeonRoom(room, entrance);
            yield return new WaitForSecondsRealtime(.05f);
            yield return FadeDungeonRoomCurtain(1f, 0f, .16f);
            HideDungeonRoomCurtain();
            UpdatePlanningState();
        }

        private IEnumerator FadeDungeonRoomCurtain(float from, float to, float duration)
        {
            float started = Time.unscaledTime;
            for (float elapsed = 0f; elapsed < duration; elapsed = Time.unscaledTime - started)
            {
                float alpha = Mathf.SmoothStep(from, to, elapsed / duration);
                dungeonRoomCurtain.color = new Color(0f, 0f, 0f, alpha);
                yield return null;
            }
            dungeonRoomCurtain.color = new Color(0f, 0f, 0f, to);
        }

        private void HideDungeonRoomCurtain()
        {
            if (dungeonRoomCurtain == null) return;
            dungeonRoomCurtain.color = Color.clear;
            dungeonRoomCurtain.gameObject.SetActive(false);
        }

        private bool DungeonActorAnimating => dungeonActor != null && dungeonActor.Playing;

        private bool TryAnimateDungeonActor(Action onComplete)
        {
            if (dungeonActor == null || !dungeonActor.TryPlay(() =>
            {
                onComplete();
                UpdatePlanningState();
            })) return false;

            CancelCardDrag();
            queueDrag = null;
            SetPile(-1);
            SetSelectedTarget(null);
            UpdatePlanningState();
            return true;
        }

        private void CancelDungeonActorAnimation()
        {
            if (dungeonActor != null) dungeonActor.Cancel();
        }

        private DungeonDoorState DungeonGateState(int side)
        {
            DungeonPassage link = dungeonRoom.Doors[side];
            if (link == null) return DungeonDoorState.Wall;
            if (link.Hidden && !link.Breached) return DungeonDoorState.Concealed;
            if (!dungeonRoom.Cleared) return DungeonDoorState.Locked;
            return link.Breached ? DungeonDoorState.Breached : DungeonDoorState.Open;
        }

        private void OnDungeonBreach()
        {
            GameSfx.Play(Sfx.DungeonBreach);
            Status("发现隐藏房");
            RefreshDungeonUi();
        }

        private void SpawnDungeonEncounter(Vector2 spawn)
        {
            if (dungeonRoom.Kind == DungeonRoomKind.Boss)
            {
                GameSfx.Play(Sfx.BossEnter);
                cameraFollow?.FrameTrialEntrance(DungeonCenter, roomHalfSize);
                if (dungeonFloor == 1)
                {
                    var boss = Instantiate(firewallBossPrefab, DungeonCenter, Quaternion.identity, dungeonEnemies);
                    boss.Initialize(movement, batch.Vitals, dashObstacleMask, cameraFollow, root, encloseTrial: false);
                    SetBossHudTarget(boss);
                    return;
                }
                var sentinel = SpawnDungeonEnemy(dungeonRoom.Enemies[0], Vector2.zero);
                if (dungeonFloor == 0)
                    sentinel.ConfigureDungeonBoss("防火墙守卫", Mathf.RoundToInt(batch.Vitals.MaxHealth * 1.5f), 30);
                else
                {
                    rootBoss = sentinel;
                    rootBoss.ConfigureDungeonBoss("Root", 420, 100);
                    rootSummonAt = Time.time + 3.5f;
                }
                SetBossHudTarget(sentinel);
                return;
            }

            Vector2 away = spawn.sqrMagnitude > 0.01f ? -spawn.normalized : Vector2.up;
            Vector2 tangent = new(-away.y, away.x);
            for (int i = 0; i < dungeonRoom.Enemies.Length; i++)
            {
                float offset = (i - (dungeonRoom.Enemies.Length - 1) * .5f) * 1.8f;
                SpawnDungeonEnemy(dungeonRoom.Enemies[i], away * (1.9f + .55f * (i % 2)) + tangent * offset);
            }
        }

        private ProgramEnemy SpawnDungeonEnemy(EnemyRole role, Vector2 offset)
        {
            ProgramEnemy prefab = DungeonPrefabs().First(item => item.Role == role);
            var enemy = Instantiate(prefab, DungeonCenter + offset, Quaternion.identity, dungeonEnemies);
            enemy.Initialize(movement, batch.Vitals, dashObstacleMask);
            return enemy;
        }

        private bool UpdateDungeon(Keyboard keyboard)
        {
            bool interactionInput = UpdateDungeonInteractionInput(keyboard);
            if (dungeonRoom == null) return interactionInput;
            if (!interactionInput && keyboard?.hKey.wasPressedThisFrame == true && !dungeonFinished)
                dungeonFreeHand = !dungeonFreeHand;
            if (!dungeonFinished && batch.Vitals.Health <= 0)
            {
                dungeonFinished = dungeonLost = true;
                ClearBetweenDungeonRooms();
                RefreshDungeonUi();
            }
            if (dungeonFinished)
            {
                if (!batch.Busy) movement.SetPlanningLock(true);
                return true;
            }
            if (rootBoss != null && rootBoss.Alive && Time.time >= rootSummonAt)
            {
                rootSummonAt = Time.time + 4f;
                if (CombatTarget.Active.Count(target => target.Alive && target is ProgramEnemy enemy && enemy.Role == EnemyRole.Swarm) < 4)
                {
                    float side = movement.Position.x < DungeonCenter.x ? 1f : -1f;
                    SpawnDungeonEnemy(EnemyRole.Swarm, new Vector2(5f * side, 2f));
                    GameSfx.Play(Sfx.BossDeploy, .8f);
                }
            }
            if (!dungeonRoom.Cleared && !CombatTarget.Active.Any(target => target.Alive))
            {
                dungeonRoom.Cleared = true;
                if (dungeonRoom.Kind is DungeonRoomKind.Combat or DungeonRoomKind.Elite) fragments += 50;
                foreach (var gate in roomGates) gate.Unlock();
                GameSfx.Play(Sfx.DungeonRoomCleared);
                RefreshDungeonUi();
            }
            if (interactionInput || DungeonInteractionOpen || DungeonActorAnimating || dungeonInteractionPending) return true;
            if (!dungeonRoom.Cleared || batch.Busy) return false;
            Vector2 position = movement.Position - DungeonCenter;
            for (int side = 0; side < 4; side++)
            {
                if (DungeonGateState(side) is not (DungeonDoorState.Open or DungeonDoorState.Breached)) continue;
                var outward = (Vector2)DungeonGenerator.Directions[side];
                float extent = (side & 1) == 0 ? roomHalfSize.y : roomHalfSize.x;
                if (Vector2.Dot(position, outward) < extent - .30f) continue;
                float lateral = Vector2.Dot(position, new Vector2(outward.y, -outward.x));
                if (Mathf.Abs(lateral) > DungeonGate.Aperture / 2f) continue;
                TransitionToDungeonRoom(dungeonRoom.Doors[side].Other(dungeonRoom), (side + 2) % 4, dungeonFloor);
                return true;
            }
            return false;
        }

        private void NextDungeonFloor()
        {
            if (DungeonRoomTransitioning || batch.Busy || dungeonFinished ||
                dungeonRoom.Kind != DungeonRoomKind.Boss || !dungeonRoom.Cleared) return;
            if (dungeonFloor + 1 >= dungeonFloors.Length)
            {
                dungeonRoom.Claimed = true;
                dungeonFinished = true;
                GameSfx.Play(Sfx.DungeonComplete);
                RefreshDungeonUi();
                return;
            }
            int nextFloor = dungeonFloor + 1;
            TransitionToDungeonRoom(dungeonFloors[nextFloor][0], -1, nextFloor);
        }
    }
}
