using System;
using UnityEngine;

namespace Staredown
{
    public static class StareAvatarCatalog
    {
        // Preserve original IDs, especially Meta=4, in preferences and room messages.
        public const int PresetCount = 8, Meta = 4, ChoiceCount = 9;
        public static readonly int[] PresetIds = { 0, 1, 2, 3, 5, 6, 7, 8 };
        public static readonly string[] Names = { "FROST", "EMBER", "OBSIDIAN", "AURORA", "META AVATAR", "KNIGHT", "MAGE", "ROGUE", "BARBARIAN" };
        public static readonly string[] Assets = { "Frost", "Ember", "Obsidian", "Aurora", "Frost", "Knight", "Mage", "Rogue", "Barbarian" };
        public static readonly string[] Descriptions = { "THE CIRCUIT FOX", "THE SUNFIRE DRAKE", "THE STONE TITAN", "THE ASTRAL SENTINEL", "YOUR META IDENTITY", "THE STEADFAST GUARDIAN", "THE STARGAZING SPELLCASTER", "THE QUICK-WITTED CHALLENGER", "THE BOLD ARENA CHAMPION" };
        public static readonly string[] Bios = { "Cool under pressure.\nA gaze like ice.", "A warm heart.\nA fearless stare.", "Stand your ground.\nNever look away.", "Quiet confidence.\nCosmic focus.", "A familiar face.\nEntirely yours.", "Brave heart.\nUnbroken focus.", "A little magic.\nA lot of composure.", "Quick hands.\nAn unwavering gaze.", "Big confidence.\nAn even bigger stare." };
        public static readonly string[] ShortDescriptions = { "Arctic composure", "A fiery challenger", "Unshakable focus", "A celestial gaze", "Your own identity", "A fearless guardian", "Magical composure", "Quick wit. Steady eyes.", "Bold under pressure" };
        public static readonly Color[] Colors = { new Color(.18f,.84f,1), new Color(1,.40f,.17f), new Color(1,.73f,.32f), new Color(.69f,.46f,1), new Color(.18f,.84f,1), new Color(.45f,.75f,1), new Color(.77f,.55f,1), new Color(.35f,.91f,.67f), new Color(1,.65f,.29f) };
        public static bool IsPreset(int kind) => kind >= 0 && kind < ChoiceCount && kind != Meta;
        public static int PresetId(int slot) => PresetIds[(slot % PresetCount + PresetCount) % PresetCount];
        public static int PresetSlot(int kind) => Array.IndexOf(PresetIds, kind);
        public static bool IsValid(int kind, string userId)
        {
            if (IsPreset(kind)) return true;
            ulong value;
            return kind == Meta && userId != null && userId.Length <= 20 && ulong.TryParse(userId, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value) && value > 0;
        }
        public static string AppearanceKey(int kind, string userId) => kind + ":" + (kind == Meta ? userId : "");
    }

    [Serializable] public sealed class StareAvatarMaterials { public StareAvatarMaterial[] materials; }
    [Serializable] public sealed class StareAvatarMaterial
    {
        public string name;
        public float[] baseColor, emission;
        public float metallic, roughness, emissionStrength;
        public string albedoTexture, normalTexture, occlusionTexture;
        public float normalScale = 1;
        public float occlusionStrength = .62f;
    }
}
