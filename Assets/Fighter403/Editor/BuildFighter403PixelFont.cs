using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

[InitializeOnLoad]
internal static class BuildFighter403PixelFont
{
    private const string SourcePath = "Assets/Fighter403/Art/UI/Fonts/Fighter403MenuPixel.otf";
    private const string AssetPath = "Assets/Resources/Fighter403/MainMenu/MenuPixelFont.asset";

    static BuildFighter403PixelFont()
    {
        EditorApplication.update += Create;
    }

    private static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating || EditorApplication.isCompiling) return;
        EditorApplication.update -= Create;
        const string chinese = "设置制作人开始游戏魏旻顾（季闲）全屏关闭启声音响返回音乐效总量↑↓选择确认地牢缺少已配置的卡牌或敌预制体。图集导入尚未完成暂时无法，请重试停继续主界面";
        string characters = "";
        for (char ch = ' '; ch <= '~'; ch++) characters += ch;
        characters += chinese;

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        bool create = font == null;
        if (!create && font.HasCharacters(characters)) return;
        try
        {
            if (create)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
                if (source == null) throw new InvalidOperationException("Menu source font did not import.");
                font = TMP_FontAsset.CreateFontAsset(source, 12, 1, GlyphRenderMode.RASTER, 256, 256,
                    AtlasPopulationMode.Dynamic, false);
                if (font == null) throw new InvalidOperationException("TextMesh Pro could not create a bitmap font.");
            }

            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            try
            {
                if (!font.TryAddCharacters(characters, out var missing))
                    throw new InvalidOperationException("Missing menu glyphs: " + missing);
            }
            finally
            {
                font.atlasPopulationMode = AtlasPopulationMode.Static;
            }

            font.atlasTexture.filterMode = FilterMode.Point;
            font.atlasTexture.wrapMode = TextureWrapMode.Clamp;
            if (create)
            {
                font.name = "Menu Pixel Font";
                AssetDatabase.CreateAsset(font, AssetPath);
                AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
                AssetDatabase.AddObjectToAsset(font.material, font);
            }
            EditorUtility.SetDirty(font.atlasTexture);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
            File.WriteAllText("Temp/Fighter403PixelFont.result", "PASS " + font.characterTable.Count + " glyphs");
        }
        catch (Exception error)
        {
            File.WriteAllText("Temp/Fighter403PixelFont.result", error.ToString());
            Debug.LogException(error);
        }
    }
}

