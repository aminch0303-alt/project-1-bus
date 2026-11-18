using UnityEngine;

/// <summary>
/// Auto Graphic Settings:
/// - Automatically runs before the first scene loads using [RuntimeInitializeOnLoadMethod].
/// - Detects device RAM (SystemInfo.systemMemorySize in MB).
/// - Sets Quality Level:
///     <= 2.5 GB RAM -> "Very Low" (Index 0), 30 FPS, LOD Bias 0.70
///     <= 4.5 GB RAM -> "Medium" (Index 2), 30 FPS, LOD Bias 0.85, 720p Render Scale (Balanced HD visuals + Smooth Lag-free 30 FPS)
///     >= 6.0 GB RAM -> "Very High" (Index 4), 60 FPS, LOD Bias 1.50
/// - Overrides QualitySettings.lodBias so fences and road props don't pop out / disappear at short distance.
/// - Optimizes render resolution on 4GB/2GB budget mobile devices (avoids 1080p GPU fillrate bottleneck).
/// </summary>
public static class AutoGraphicSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void InitializeGraphics()
    {
        // Device RAM in Megabytes (MB)
        int ramMB = SystemInfo.systemMemorySize;

        int targetQualityLevel;
        float targetLodBias;
        int targetFPS;
        float customShadowDistance = -1f;

        if (ramMB <= 2500) // ~2 GB or less
        {
            targetQualityLevel = GetQualityLevelByName("Very Low", 0);
            targetFPS = 30;
            targetLodBias = 0.70f;
            Debug.Log($"[AutoGraphicSettings] 2GB RAM detected ({ramMB} MB). Setting: VERY LOW (30 FPS, LOD Bias 0.70).");
        }
        else if (ramMB <= 4500) // ~3 GB to 4 GB
        {
            // Medium preset for better visuals, vehicle lighting, and soft ground shadows
            targetQualityLevel = GetQualityLevelByName("Medium", 2);
            targetFPS = 30; // 30 FPS ensures stable, lag-free gameplay without thermal throttling on 4GB devices
            targetLodBias = 0.85f; // Ensures fences and roadside props remain visible along the road
            customShadowDistance = 25f; // Lightweight shadow distance (bus shadow looks crisp without heavy GPU cost)
            Debug.Log($"[AutoGraphicSettings] 4GB RAM detected ({ramMB} MB). Setting: MEDIUM (30 FPS, 720p, LOD Bias 0.85).");
        }
        else // 6 GB or more
        {
            targetQualityLevel = GetQualityLevelByName("Very High", 4);
            targetFPS = 60;
            targetLodBias = 1.50f;
            Debug.Log($"[AutoGraphicSettings] 6GB+ RAM detected ({ramMB} MB). Setting: VERY HIGH (60 FPS, LOD Bias 1.50).");
        }

        // 1. Set Quality Preset Level
        QualitySettings.SetQualityLevel(targetQualityLevel, true);

        // 2. Override LOD Bias AFTER SetQualityLevel (so fences and road props stay visible naturally)
        QualitySettings.lodBias = targetLodBias;

        // 3. Override Shadow Distance if specified (keeps shadows crisp near the bus without distant shadow lag)
        if (customShadowDistance > 0f)
        {
            QualitySettings.shadowDistance = customShadowDistance;
        }

        // 4. Set Target Frame Rate (30 for budget devices, 60 for high-end)
        Application.targetFrameRate = targetFPS;

        // 5. Resolution scaling for budget mobile devices (reduces GPU fillrate burden by ~50% on 1080p budget screens)
#if UNITY_ANDROID && !UNITY_EDITOR
        if (ramMB <= 4500)
        {
            int minDim = Mathf.Min(Screen.width, Screen.height);
            int maxDim = Mathf.Max(Screen.width, Screen.height);

            // If the smaller dimension (height in landscape) is above 720p, scale to 720p
            if (minDim > 720)
            {
                float scale = 720f / minDim;
                int targetMin = 720;
                int targetMax = Mathf.RoundToInt(maxDim * scale);

                if (Screen.width >= Screen.height)
                {
                    Screen.SetResolution(targetMax, targetMin, true);
                }
                else
                {
                    Screen.SetResolution(targetMin, targetMax, true);
                }
                Debug.Log($"[AutoGraphicSettings] Scaled resolution to {Screen.width}x{Screen.height} for lag-free performance on 4GB device.");
            }
        }
#endif
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
