using UnityEngine;

/// <summary>
/// Auto Graphic Settings:
/// - Automatically runs before the first scene loads using [RuntimeInitializeOnLoadMethod].
/// - Detects device RAM (SystemInfo.systemMemorySize in MB).
/// - Sets Quality Level:
///     <= 2 GB RAM -> Very Low Quality
///     <= 4 GB RAM -> Low Quality
///     >= 6 GB RAM -> Very High Quality
/// - Sets Target Frame Rate to 60 FPS for smooth mobile gameplay.
/// </summary>
public static class AutoGraphicSettings
{
    private const string QUALITY_PREF_KEY = "AutoGraphics_QualitySet";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void InitializeGraphics()
    {
        // Smooth frame rate on mobile
        Application.targetFrameRate = 60;

        // Device RAM in Megabytes (MB)
        int ramMB = SystemInfo.systemMemorySize;

        // Apply quality based on RAM
        int targetQualityLevel;

        if (ramMB <= 2500) // ~2 GB or less
        {
            targetQualityLevel = GetQualityLevelByName("Very Low", 0);
            Debug.Log($"[AutoGraphicSettings] 2GB RAM detected ({ramMB} MB). Setting graphics to VERY LOW.");
        }
        else if (ramMB <= 4500) // ~3 GB to 4 GB
        {
            targetQualityLevel = GetQualityLevelByName("Low", 1);
            Debug.Log($"[AutoGraphicSettings] 4GB RAM detected ({ramMB} MB). Setting graphics to LOW.");
        }
        else // 6 GB or more
        {
            targetQualityLevel = GetQualityLevelByName("Very High", 4);
            Debug.Log($"[AutoGraphicSettings] 6GB+ RAM detected ({ramMB} MB). Setting graphics to VERY HIGH.");
        }

        QualitySettings.SetQualityLevel(targetQualityLevel, true);
    }

    /// <summary>
    /// Finds matching quality level index by name in Project QualitySettings,
    /// falls back to fallbackIndex if name is not found.
    /// </summary>
    private static int GetQualityLevelByName(string name, int fallbackIndex)
    {
        string[] names = QualitySettings.names;
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i].ToLower().Contains(name.ToLower()))
            {
                return i;
            }
        }
        return Mathf.Clamp(fallbackIndex, 0, names.Length - 1);
    }
}
