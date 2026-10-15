using System;
using UnityEditor;
using UnityEngine;

// Baked character maps use explicit naming so a normal map is never sampled
// as sRGB color. Keep mipmaps for stable detail when viewed in a headset.
public sealed class StareAvatarTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Staredown/Resources/StaredownAvatars/", StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        bool normal = assetPath.EndsWith("Normal.png", StringComparison.OrdinalIgnoreCase);
        bool occlusion = assetPath.EndsWith("_Occlusion.png", StringComparison.OrdinalIgnoreCase);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.convertToNormalmap = false;
        importer.sRGBTexture = !normal && !occlusion;
        importer.mipmapEnabled = true;
        importer.isReadable = false;
        importer.maxTextureSize = 2048;
        importer.anisoLevel = 2;
        importer.filterMode = FilterMode.Trilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
