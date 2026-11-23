using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Night Weather Controller:
/// - Level 4 ke liye dynamic starry night / moonlit atmosphere manage karta hai.
/// - 100% Direct References: Zero lag, mobile optimized.
/// - Starry Night Skybox swap karta hai (Moonburst / Night Skybox).
/// - Sunlight ko cool dark moonlight (0.2 - 0.25 intensity) mein convert karta hai.
/// - Deep midnight blue fog apply karta hai taake horizon dark aur atmospheric lage.
/// - Level 4 khatam hone par daytime settings automatically restore karta hai.
/// - Optional: Bus headlights ko automatically ON karta hai aur Night Lights group ko toggle karta hai.
/// </summary>
public class NightWeatherController : MonoBehaviour
{
    [Header("--- Night Skybox ---")]
    [Tooltip("Hierarchy se 'Night Moon Burst' ya koi bhi Night Skybox Material drag karein")]
    [SerializeField] private Material nightSkybox;

    [Header("--- Moonlit Directional Light ---")]
    [Tooltip("Hierarchy se 'Directional Light' drag karein")]
    [SerializeField] private Light directionalLight;

    [Tooltip("Night mein moonlight ki intensity (0.12 - 0.20 recommended for authentic night)")]
    [SerializeField] private float nightLightIntensity = 0.15f;

    [Tooltip("Moonlight ka color (Cool starry cyan-blue tint)")]
    [SerializeField] private Color nightLightColor = new Color(0.55f, 0.68f, 0.88f, 1f);

    [Header("--- Ambient & Road/Terrain Darkness ---")]
    [Tooltip("Kya road aur terrain ki ambient roshni ko dark night mein adjust karna hai?")]
    [SerializeField] private bool adjustAmbientLighting = true;

    [Tooltip("Ambient Intensity (0.15 - 0.25: Terrain aur ghaas naturally dark raat jesi lagegi)")]
    [Range(0.05f, 1f)]
    [SerializeField] private float nightAmbientIntensity = 0.20f;

    [Tooltip("Reflection Intensity (0.10 - 0.22: Road par skybox ki bright reflection khatam kar ke asli kaali road look dega)")]
    [Range(0.0f, 1f)]
    [SerializeField] private float nightReflectionIntensity = 0.15f;

    [Tooltip("Midnight Ambient Color tint")]
    [SerializeField] private Color nightAmbientColor = new Color(0.10f, 0.14f, 0.24f, 1f);

    [Header("--- Night Fog Settings ---")]
    [Tooltip("Kya night fog on karni hai?")]
    [SerializeField] private bool enableNightFog = true;

    [Tooltip("Night fog ka color (Deep midnight navy blue)")]
    [SerializeField] private Color nightFogColor = new Color(0.06f, 0.09f, 0.16f, 1f);

    [Tooltip("Fog Density (0.003 - 0.005 recommended)")]
    [SerializeField] private float nightFogDensity = 0.004f;

    [Header("--- Headlights & Night Lights (Optional) ---")]
    [Tooltip("Kya raat ko bus ke headlights automatically ON karne hain?")]
    [SerializeField] private bool autoTurnOnHeadlights = true;

    [Tooltip("Active Player Bus (RCC_CarControllerV3)")]
    [SerializeField] private RCC_CarControllerV3 playerBus;

    [Tooltip("Raat ke waqt jo street lamps, petrol pump lights ya neon lights ON karni hon")]
    [SerializeField] private GameObject[] nightLightsToEnable;

    [Header("--- Night Ambience Audio (Optional) ---")]
    [Tooltip("Crickets / Night breeze audio source")]
    [SerializeField] private AudioSource nightAmbienceAudioSource;

    // Original settings backup
    private Material _originalSkybox;
    private bool _originalFogEnabled;
    private Color _originalFogColor;
    private float _originalFogDensity;
    private FogMode _originalFogMode;

    private float _originalLightIntensity = 1f;
    private Color _originalLightColor = Color.white;
    private bool _hasLightBackup = false;

    // Ambient & Reflection backup
    private float _originalAmbientIntensity = 1f;
    private float _originalReflectionIntensity = 1f;
    private Color _originalAmbientLight = Color.white;
    private Color _originalAmbientSkyColor = Color.white;
    private Color _originalAmbientEquatorColor = Color.white;
    private Color _originalAmbientGroundColor = Color.white;
    private bool _hasAmbientBackup = false;

    private void OnEnable()
    {
        // 0. Backup & Apply Night Skybox
        _originalSkybox = RenderSettings.skybox;
        if (nightSkybox != null)
        {
            RenderSettings.skybox = nightSkybox;
#if UNITY_EDITOR
            DynamicGI.UpdateEnvironment();
#endif
        }

        // 1. Backup & Apply Night Fog
        if (enableNightFog)
        {
            _originalFogEnabled = RenderSettings.fog;
            _originalFogColor = RenderSettings.fogColor;
            _originalFogDensity = RenderSettings.fogDensity;
            _originalFogMode = RenderSettings.fogMode;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = nightFogColor;
            RenderSettings.fogDensity = nightFogDensity;
        }

        // 2. Backup & Apply Moonlight
        if (directionalLight != null)
        {
            if (!_hasLightBackup)
            {
                _originalLightIntensity = directionalLight.intensity;
                _originalLightColor = directionalLight.color;
                _hasLightBackup = true;
            }

            directionalLight.intensity = nightLightIntensity;
            directionalLight.color = nightLightColor;
        }

        // 3. Backup & Apply Ambient / Reflection Darkness
        if (adjustAmbientLighting)
        {
            if (!_hasAmbientBackup)
            {
                _originalAmbientIntensity = RenderSettings.ambientIntensity;
                _originalReflectionIntensity = RenderSettings.reflectionIntensity;
                _originalAmbientLight = RenderSettings.ambientLight;
                _originalAmbientSkyColor = RenderSettings.ambientSkyColor;
                _originalAmbientEquatorColor = RenderSettings.ambientEquatorColor;
                _originalAmbientGroundColor = RenderSettings.ambientGroundColor;
                _hasAmbientBackup = true;
            }

            RenderSettings.ambientIntensity = nightAmbientIntensity;
            RenderSettings.reflectionIntensity = nightReflectionIntensity;
            RenderSettings.ambientLight = nightAmbientColor;
            RenderSettings.ambientSkyColor = nightAmbientColor;
            RenderSettings.ambientEquatorColor = nightAmbientColor * 0.7f;
            RenderSettings.ambientGroundColor = nightAmbientColor * 0.35f;

#if UNITY_EDITOR
            DynamicGI.UpdateEnvironment();
#endif
        }

        // 4. Enable Night Lights (Street lamps, station neon, etc.)
        if (nightLightsToEnable != null)
        {
            for (int i = 0; i < nightLightsToEnable.Length; i++)
            {
                if (nightLightsToEnable[i] != null)
                {
                    nightLightsToEnable[i].SetActive(true);
                }
            }
        }

        // 5. Night Ambience Audio
        if (nightAmbienceAudioSource != null)
        {
            nightAmbienceAudioSource.gameObject.SetActive(true);
            if (!nightAmbienceAudioSource.isPlaying)
            {
                nightAmbienceAudioSource.Play();
            }
        }

        // 6. Auto Turn On Headlights
        if (autoTurnOnHeadlights)
        {
            StartCoroutine(TurnOnHeadlightsRoutine());
        }
    }

    private void OnDisable()
    {
        // 0. Restore Original Daytime Skybox
        if (_originalSkybox != null)
        {
            RenderSettings.skybox = _originalSkybox;
#if UNITY_EDITOR
            DynamicGI.UpdateEnvironment();
#endif
        }

        // 1. Restore Fog
        if (enableNightFog)
        {
            RenderSettings.fog = _originalFogEnabled;
            RenderSettings.fogColor = _originalFogColor;
            RenderSettings.fogDensity = _originalFogDensity;
            RenderSettings.fogMode = _originalFogMode;
        }

        // 2. Restore Daytime Sunlight
        if (directionalLight != null && _hasLightBackup)
        {
            directionalLight.intensity = _originalLightIntensity;
            directionalLight.color = _originalLightColor;
        }

        // 3. Restore Daytime Ambient & Reflection
        if (adjustAmbientLighting && _hasAmbientBackup)
        {
            RenderSettings.ambientIntensity = _originalAmbientIntensity;
            RenderSettings.reflectionIntensity = _originalReflectionIntensity;
            RenderSettings.ambientLight = _originalAmbientLight;
            RenderSettings.ambientSkyColor = _originalAmbientSkyColor;
            RenderSettings.ambientEquatorColor = _originalAmbientEquatorColor;
            RenderSettings.ambientGroundColor = _originalAmbientGroundColor;

#if UNITY_EDITOR
            DynamicGI.UpdateEnvironment();
#endif
        }

        // 4. Disable Night Lights
        if (nightLightsToEnable != null)
        {
            for (int i = 0; i < nightLightsToEnable.Length; i++)
            {
                if (nightLightsToEnable[i] != null)
                {
                    nightLightsToEnable[i].SetActive(false);
                }
            }
        }

        // 5. Stop Night Ambience Audio
        if (nightAmbienceAudioSource != null)
        {
            nightAmbienceAudioSource.Stop();
        }
    }

    private IEnumerator TurnOnHeadlightsRoutine()
    {
        // Wait 1 frame so bus is fully initialized
        yield return null;

        RCC_CarControllerV3 targetBus = playerBus;
        if (targetBus == null && GameManager.Instance != null)
        {
            targetBus = GameManager.Instance.GetCurrentPlayerCarController();
        }

        if (targetBus != null)
        {
            targetBus.lowBeamHeadLightsOn = true;
        }
    }
}
