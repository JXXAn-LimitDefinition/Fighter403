using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Fighter403
{
    internal sealed class DungeonArtImport : AssetPostprocessor
    {
        internal const string Directory = "Assets/Fighter403/Art/Dungeon/Resources/" + DungeonRoomArt.ResourceFolder;
        private const string LayoutPath = Directory + "/AtlasLayout.json";

        [Serializable]
        private sealed class Layout
        {
            public Slice[] tiles, roles, signals;
            public Slice[] pieces;
        }

        [Serializable]
        private sealed class Slice
        {
            public string name;
            public int x, y, w, h;
        }

        private void OnPreprocessTexture()
        {
            if (Path.GetDirectoryName(assetPath)?.Replace('\\', '/') != Directory)
                return;

            string atlas = Path.GetFileNameWithoutExtension(assetPath);
            if (atlas is not ("Cache" or "Firewall" or "Core" or "Landmarks" or "DoorSignal" or "RoomInteraction" or "RoomProps"))
                return;

            Slice[] slices;
            if (atlas == "RoomInteraction")
            {
                const int cell = 144;
                int count = DungeonRoomActor.FrameCount;
                string[] animations = { "Chest", "Merchant", "MerchantIdle" };
                slices = Enumerable.Range(0, count * animations.Length).Select(i => new Slice
                {
                    name = $"{animations[i / count]}{i % count:00}",
                    x = i % 6 * cell,
                    y = i / 6 * cell,
                    w = cell,
                    h = cell,
                }).ToArray();
            }
            else if (atlas == "RoomProps")
            {
                const string path = Directory + "/RoomPropLayout.json";
                context.DependsOnSourceAsset(path);
                slices = JsonUtility.FromJson<Layout>(File.ReadAllText(path)).pieces;
            }
            else
            {
                context.DependsOnSourceAsset(LayoutPath);
                var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
                slices = atlas == "Landmarks" ? layout.roles : atlas == "DoorSignal" ? layout.signals : layout.tiles;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = DungeonRoomArt.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);

            importer.GetSourceTextureWidthAndHeight(out _, out int height);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(slice => slice.name, slice => slice.spriteID);
            var rects = slices.Select(slice => new SpriteRect
            {
                name = slice.name,
                rect = new Rect(slice.x, height - slice.y - slice.h, slice.w, slice.h),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(.5f, .5f),
                spriteID = previous.TryGetValue(slice.name, out var id) ? id : GUID.Generate()
            }).ToArray();

            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
                .SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
        }
    }
}
