using System;
using UnityEditor;
using UnityEngine;

namespace Fighter403
{
    internal sealed class DungeonRoomArtWindow : EditorWindow
    {
        private static readonly string[] DoorLabels = { "墙", "开启", "锁定", "隐藏", "破开" };
        private static readonly string[] Directions = { "北", "东", "南", "西" };
        [SerializeField] private int theme;
        [SerializeField] private int room = 1;
        [SerializeField] private int columns = 14, rows = 8, floorSeed;
        [SerializeField] private DungeonDoorState[] doors =
            { DungeonDoorState.Open, DungeonDoorState.Locked, DungeonDoorState.Breached, DungeonDoorState.Concealed };
        private bool reloadPending;

        [MenuItem("Fighter403/地牢美术预览")]
        private static void Open()
        {
            var window = GetWindow<DungeonRoomArtWindow>("地牢美术");
            window.minSize = new Vector2(520, 360);
        }

        private void OnProjectChange() => reloadPending = true;

        private void OnInspectorUpdate()
        {
            if (reloadPending && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                reloadPending = false;
                DungeonRoomArt.Reload();
                Repaint();
            }
            if (Array.IndexOf(doors, DungeonDoorState.Locked) >= 0) Repaint();
        }

        private void OnGUI()
        {
            theme = EditorGUILayout.Popup("楼层", theme, DungeonGenerator.ThemeLabels);
            int previousRoom = room;
            room = EditorGUILayout.Popup("房型", room, DungeonGenerator.RoomLabels);
            bool wasBoss = previousRoom == (int)DungeonRoomKind.Boss;
            bool isBoss = room == (int)DungeonRoomKind.Boss;
            if (wasBoss != isBoss && columns == (wasBoss ? 28 : 14) && rows == (wasBoss ? 18 : 8))
            {
                columns = isBoss ? 28 : 14;
                rows = isBoss ? 18 : 8;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                columns = Mathf.Clamp(EditorGUILayout.DelayedIntField("列", columns), 12, 36) & ~1;
                rows = Mathf.Clamp(EditorGUILayout.DelayedIntField("行", rows), 8, 24) & ~1;
            }
            floorSeed = EditorGUILayout.DelayedIntField("地砖种子", floorSeed);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int side = 0; side < doors.Length; side++)
                {
                    GUILayout.Label(Directions[side], GUILayout.Width(14));
                    doors[side] = (DungeonDoorState)EditorGUILayout.Popup((int)doors[side], DoorLabels);
                }
            }

            var art = new DungeonRoomArt(theme, (DungeonRoomKind)room, columns, rows, floorSeed, doors);
            if (!art.Ready)
            {
                EditorGUILayout.HelpBox("房间图集尚未完成导入。", MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("将美术放入场景"))
                {
                    var root = art.Create($"{DungeonGenerator.ThemeLabels[theme]} · {DungeonGenerator.RoomLabels[room]}",
                        Vector2.zero, null, null);
                    Undo.RegisterCreatedObjectUndo(root, "Add dungeon room art");
                    Selection.activeGameObject = root;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }

            var view = GUILayoutUtility.GetRect(0, 10000, 0, 10000, GUILayout.ExpandHeight(true));
            if (Event.current.type != EventType.Repaint || view.width < 16 || view.height < 16)
                return;
            EditorGUI.DrawRect(view, new Color32(16, 21, 34, 255));
            var pixels = new Vector2(columns, rows) * DungeonRoomArt.Cell;
            float scale = Mathf.Min((view.width - 16) / pixels.x, (view.height - 16) / pixels.y);
            Vector2 origin = view.center - pixels * (scale * .5f);
            var signal = DungeonRoomArt.Signal;
            int frame = (int)(EditorApplication.timeSinceStartup * DungeonDoorSignal.FramesPerSecond % signal.Count);
            foreach (var piece in art.Layout())
            {
                Vector2 point = origin + piece.Center * scale;
                DrawSprite(piece.Sprite, point, scale, piece.Turn);
                if (piece.GateSide >= 0 && doors[piece.GateSide] == DungeonDoorState.Locked)
                    DrawSprite(signal[frame], point, scale, piece.Turn);
            }
        }

        private static void DrawSprite(Sprite sprite, Vector2 center, float scale, int turn)
        {
            Rect slice = sprite.rect;
            var size = slice.size * scale;
            var rect = new Rect(center - size * .5f, size);
            var texture = sprite.texture;
            var uv = new Rect(slice.x / texture.width, slice.y / texture.height,
                slice.width / texture.width, slice.height / texture.height);
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(turn * 90f, center);
            GUI.DrawTextureWithTexCoords(rect, texture, uv);
            GUI.matrix = matrix;
        }
    }
}
