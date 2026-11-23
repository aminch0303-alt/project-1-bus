using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Device Tier presets for testing and auto-configuration.
/// </summary>
public enum DeviceTier
{
    AutoDetect = 0,
    LowEnd_2GB = 1,
    MidRange_4GB = 2,
    HighEnd_6GB = 3
}

/// <summary>
/// Auto Graphic Settings & Device Simulation:
/// - Automatically detects real device RAM or applies user-selected simulated device tier.
/// - Allows instant switching between device tiers both in Inspector before Play and live in-game via debug UI.
/// - Quality Levels:
///     * LowEnd_2GB  -> "Very Low", 30 FPS, LOD 0.65, Shadows OFF, Tree Dist 200m, FarClip 300m
///     * MidRange_4GB -> "Medium", 30 FPS (VSync 0), LOD 0.80, Shadows 20m, 720p, Tree Dist 250m, FarClip 350m
///     * HighEnd_6GB -> "Very High", 60 FPS, LOD 1.50, Shadows 50m, Tree Dist 500m, FarClip 500m
/// </summary>
public static class AutoGraphicSettings
{
    private static int _deviceRamMB;
    public static DeviceTier ActiveTier { get; private set; } = DeviceTier.AutoDetect;
    public static int EffectiveRamMB => _deviceRamMB;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void InitializeGraphics()
    {
        // Check if an explicit device tier was saved in PlayerPrefs
        DeviceTier savedTier = DeviceTier.AutoDetect;
        if (PlayerPrefs.HasKey("SimulatedDeviceTier"))
        {
            savedTier = (DeviceTier)PlayerPrefs.GetInt("SimulatedDeviceTier", 0);
        }

        ApplyTier(savedTier, isStartup: true);

        // Register Scene Loaded listener to ensure terrain and cameras are optimized after every scene load
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeAfterScene()
    {
        ApplySceneOptimizations();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneOptimizations();
    }

    /// <summary>
    /// Applies graphics, frame rate, shadow, terrain, and camera settings for the selected device tier.
    /// Can be called at runtime for instant live testing.
    /// </summary>
    public static void ApplyTier(DeviceTier tier, bool isStartup = false, bool log = true)
    {
        ActiveTier = tier;

        // Determine effective RAM
        switch (tier)
        {
            case DeviceTier.LowEnd_2GB:
                _deviceRamMB = 2000;
                break;
            case DeviceTier.MidRange_4GB:
                _deviceRamMB = 4000;
                break;
            case DeviceTier.HighEnd_6GB:
                _deviceRamMB = 8000;
                break;
            case DeviceTier.AutoDetect:
            default:
                _deviceRamMB = SystemInfo.systemMemorySize;
                break;
        }

        int targetQualityLevel;
        float targetLodBias;
        int targetFPS;
        float customShadowDistance;

        if (_deviceRamMB <= 2500) // 2GB Low-end
        {
            targetQualityLevel = GetQualityLevelByName("Very Low", 0);
            targetFPS = 30;
            targetLodBias = 0.65f;
            customShadowDistance = 0f; // Shadows OFF for max fillrate
            if (log) Debug.Log($"[AutoGraphicSettings] Tier: 2GB Low-End (RAM: {_deviceRamMB} MB) -> Very Low, 30 FPS, Shadows OFF, LOD 0.65.");
        }
        else if (_deviceRamMB <= 4500) // 4GB Mid-range
        {
            targetQualityLevel = GetQualityLevelByName("Medium", 2);
            targetFPS = 60; // Target 60 FPS: Device can output as high as its hardware supports (up to 60 FPS)
            targetLodBias = 0.85f;
            customShadowDistance = 20f; // Crisp shadows around bus
            if (log) Debug.Log($"[AutoGraphicSettings] Tier: 4GB Mid-Range (RAM: {_deviceRamMB} MB) -> Medium, 60 FPS Target, Shadows 20m, 720p, LOD 0.85.");
        }
        else // 6GB+ High-end
        {
            targetQualityLevel = GetQualityLevelByName("Very High", 4);
            targetFPS = 60;
            targetLodBias = 1.50f;
            customShadowDistance = 50f;
            if (log) Debug.Log($"[AutoGraphicSettings] Tier: 6GB+ High-End (RAM: {_deviceRamMB} MB) -> Very High, 60 FPS, Shadows 50m, LOD 1.50.");
        }

        // 1. Set Quality Preset Level
        QualitySettings.SetQualityLevel(targetQualityLevel, true);

        // 2. VSync Fix: Disable VSync when targetFrameRate is 30 to prevent judder on 60/90/120Hz mobile screens
        QualitySettings.vSyncCount = 0;

        // 3. Anti-Aliasing (MSAA): Disabled on budget/4GB to save ~30-40% GPU fillrate
        if (_deviceRamMB <= 4500)
        {
            QualitySettings.antiAliasing = 0;
        }

        // 4. Override LOD Bias AFTER SetQualityLevel
        QualitySettings.lodBias = targetLodBias;

        // 5. Override Shadow Distance
        QualitySettings.shadowDistance = customShadowDistance;

        // 6. Set Target Frame Rate
        Application.targetFrameRate = targetFPS;

        // 7. Dynamic Resolution Scaling on Android
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_deviceRamMB <= 4500)
        {
            Apply720pResolution();
            ScalableBufferManager.ResizeBuffers(0.75f, 0.75f);
        }
#endif

        // 8. Apply live terrain and camera optimizations immediately
        ApplySceneOptimizations();
    }

    /// <summary>
    /// Live optimizations for Terrain and Scene Cameras based on active RAM tier.
    /// </summary>
    public static void ApplySceneOptimizations()
    {
        // A. Optimize Terrain
        Terrain activeTerrain = Terrain.activeTerrain;
        if (activeTerrain != null)
        {
            if (_deviceRamMB <= 2500) // 2GB
            {
                activeTerrain.treeDistance = 200f;
                activeTerrain.treeBillboardDistance = 30f;
                activeTerrain.treeMaximumFullLODCount = 15;
                activeTerrain.detailObjectDistance = 25f;
                activeTerrain.heightmapPixelError = 18f;
            }
            else if (_deviceRamMB <= 4500) // 4GB
            {
                activeTerrain.treeDistance = 350f;
                activeTerrain.treeBillboardDistance = 70f; // Doubled from 35m to 70m for full 3D trees!
                activeTerrain.treeMaximumFullLODCount = 30;
                activeTerrain.detailObjectDistance = 50f;
                activeTerrain.heightmapPixelError = 16f;
            }
            else // 6GB+
            {
                activeTerrain.treeDistance = 500f;
                activeTerrain.treeBillboardDistance = 50f;
                activeTerrain.treeMaximumFullLODCount = 50;
                activeTerrain.detailObjectDistance = 50f;
                activeTerrain.heightmapPixelError = 10f;
            }
            activeTerrain.drawTreesAndFoliage = true;
        }

        // B. Optimize Cameras (Far Clip Plane & MSAA)
        Camera[] allCams = Camera.allCameras;
        float targetFarClip = (_deviceRamMB <= 2500) ? 300f : ((_deviceRamMB <= 4500) ? 350f : 500f);

        for (int i = 0; i < allCams.Length; i++)
        {
            Camera c = allCams[i];
            if (c == null) continue;

            c.allowMSAA = (_deviceRamMB > 4500);
            c.allowHDR = false;

            if (c.farClipPlane > targetFarClip)
            {
                c.farClipPlane = targetFarClip;
            }
        }

        // C. Re-apply resolution on Android
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_deviceRamMB <= 4500)
        {
            Apply720pResolution();
        }
#endif
    }

    /// <summary>
    /// Allows manually toggling target FPS between 30 and 60 for live performance testing.
    /// </summary>
    public static void SetTargetFPS(int fps)
    {
        Application.targetFrameRate = fps;
        Debug.Log($"[AutoGraphicSettings] Target FPS set to {fps}.");
    }

    private static void Apply720pResolution()
    {
        int minDim = Mathf.Min(Screen.width, Screen.height);
        int maxDim = Mathf.Max(Screen.width, Screen.height);

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
            Debug.Log($"[AutoGraphicSettings] Scaled resolution to {targetMax}x{targetMin} for 4GB device.");
        }
    }

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
