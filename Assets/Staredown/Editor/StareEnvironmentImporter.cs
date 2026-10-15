using UnityEditor;
using UnityEngine;

public sealed class StareEnvironmentImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath != "Assets/Staredown/Resources/Environment/ChampionshipTwilight.png" &&
            assetPath != "Assets/Staredown/Resources/Environment/FestivalDaylight.png") return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;
        importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.isReadable=false;
        importer.maxTextureSize=4096;importer.filterMode=FilterMode.Trilinear;
        importer.wrapModeU=TextureWrapMode.Repeat;importer.wrapModeV=TextureWrapMode.Clamp;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {name="Android",overridden=true,maxTextureSize=4096,format=TextureImporterFormat.ASTC_6x6,compressionQuality=80});
    }
}
