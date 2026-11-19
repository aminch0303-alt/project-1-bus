using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rain Weather Controller:
/// - Level 2 ke liye rain effect aur gloomy rainy atmosphere manage karta hai.
/// - 100% Direct References: Zero lag.
/// - Gameplay mein RCCCamera ko aur Cutscenes mein active Cutscene Camera ko follow karta hai.
/// </summary>
public class RainWeatherController : MonoBehaviour
{
    [Header("--- Target to Follow ---")]
    [Tooltip("Hierarchy se 'RCCCamera' drag karein")]
    [SerializeField] private Transform targetToFollow;

    [Tooltip("Target se kitni unchai (Height) par rain emitter rahe")]
    [SerializeField] private float heightOffset = 6f;

    [Tooltip("Bus/Camera ke kitna aage rain gire (Driving direction mein)")]
    [SerializeField] private float forwardOffset = 10f;

    [Tooltip("Instant Follow: Bus/Camera ke sath 100% locked rahega")]
    [SerializeField] private bool instantFollow = true;

    [Tooltip("Agar instantFollow off ho to follow speed")]
    [SerializeField] private float followSpeed = 25f;

    [Header("--- Particle System ---")]
    [Tooltip("Rain Particle System (Child GameObject par laga FX_Rain_01)")]
    [SerializeField] private ParticleSystem rainParticleSystem;

    [Header("--- Rainy Fog Settings ---")]
    [Tooltip("Kya rain fog on karni hai?")]
    [SerializeField] private bool enableRainFog = true;

    [Tooltip("Rain fog ka color (Dark Wet Grey / Charcoal)")]
    [SerializeField] private Color rainFogColor = new Color(0.55f, 0.60f, 0.68f, 1f);

    [Tooltip("Fog Density (Exponential Squared mode ke liye 0.005 - 0.007 recommended)")]
    [SerializeField] private float rainFogDensity = 0.0055f;

    [Header("--- Rainy Lighting ---")]
    [Tooltip("Kya sunlight ko gloomy dark rain tint mein adjust karna hai?")]
    [SerializeField] private bool adjustDirectionalLight = true;

    [Tooltip("Hierarchy se 'Directional Light' drag karein")]
    [SerializeField] private Light directionalLight;

    [Tooltip("Rain ke dauran sunlight ki intensity (Gloomy look ke liye 0.4 - 0.5)")]
    [SerializeField] private float rainLightIntensity = 0.45f;

    [Tooltip("Rain sunlight ka color (Cool greyish storm tint)")]
    [SerializeField] private Color rainLightColor = new Color(0.72f, 0.78f, 0.88f, 1f);

    [Header("--- Rainy Skybox (Optional) ---")]
    [Tooltip("Level 2 ke liye Overcast / Stormy Skybox Material. Agar assign karein to sirf Level 2 mein yeh lag jayega aur disable hone par normal wapis aa jayega.")]
    [SerializeField] private Material rainSkybox;

    // Original settings backup
    private Material _originalSkybox;
    private bool _originalFogEnabled;
    private Color _originalFogColor;
    private float _originalFogDensity;
    private FogMode _originalFogMode;

    private float _originalLightIntensity = 1f;
    private Color _originalLightColor = Color.white;
    private bool _hasLightBackup = false;

    private void OnEnable()
    {
        // 0. Backup & Apply Rainy Skybox
        _originalSkybox = RenderSettings.skybox;
        if (rainSkybox != null)
        {
            RenderSettings.skybox = rainSkybox;
            DynamicGI.UpdateEnvironment();
        }

        // 1. Backup & Apply Rainy Fog
        if (enableRainFog)
        {
            _originalFogEnabled = RenderSettings.fog;
            _originalFogColor = RenderSettings.fogColor;
            _originalFogDensity = RenderSettings.fogDensity;
            _originalFogMode = RenderSettings.fogMode;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = rainFogColor;
            RenderSettings.fogDensity = rainFogDensity;
        }

        // 2. Backup & Apply Rainy Lighting
        if (adjustDirectionalLight && directionalLight != null)
        {
            if (!_hasLightBackup)
            {
                _originalLightIntensity = directionalLight.intensity;
                _originalLightColor = directionalLight.color;
                _hasLightBackup = true;
            }

            directionalLight.intensity = rainLightIntensity;
            directionalLight.color = rainLightColor;
        }

        // 3. Play Rain Particles
        if (rainParticleSystem != null)
        {
            rainParticleSystem.gameObject.SetActive(true);
            if (!rainParticleSystem.isPlaying)
            {
                rainParticleSystem.Play();
            }
        }
    }

    private void OnDisable()
    {
        // 0. Restore Original Skybox
        if (_originalSkybox != null)
        {
            RenderSettings.skybox = _originalSkybox;
            DynamicGI.UpdateEnvironment();
        }

        // 1. Restore Fog
        if (enableRainFog)
        {
            RenderSettings.fog = _originalFogEnabled;
            RenderSettings.fogColor = _originalFogColor;
            RenderSettings.fogDensity = _originalFogDensity;
            RenderSettings.fogMode = _originalFogMode;
        }

        // 2. Restore Lighting
        if (adjustDirectionalLight && directionalLight != null && _hasLightBackup)
        {
            directionalLight.intensity = _originalLightIntensity;
            directionalLight.color = _originalLightColor;
        }

        // 3. Stop Rain Particles
        if (rainParticleSystem != null)
        {
            rainParticleSystem.Stop();
        }
    }

    private void LateUpdate()
    {
        Transform activeTarget = GetActiveTargetTransform();
        if (activeTarget == null) return;

        // Target ke upar position calculate karein
        Vector3 targetPos = activeTarget.position + (Vector3.up * heightOffset) + (activeTarget.forward * forwardOffset);

        if (instantFollow)
        {
            transform.position = targetPos;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);
        }

        // Rotation hamesha locked straight down taake barish naturally seedhi gire
        transform.rotation = Quaternion.identity;
    }

    /// <summary>
    /// Gameplay mein RCCCamera ko follow karta hai.
    /// Cutscenes ke dauran (jab RCCCamera band ya disabled ho) automatically active Cutscene Camera ko follow karta hai.
    /// </summary>
    private Transform GetActiveTargetTransform()
    {
        bool isRCCCameraActive = false;

        // 1. Normal Gameplay Check: Assigned target (RCCCamera)
        if (targetToFollow != null && targetToFollow.gameObject.activeInHierarchy)
        {
            RCC_Camera rccCam = targetToFollow.GetComponent<RCC_Camera>();
            if (rccCam != null)
            {
                isRCCCameraActive = rccCam.enabled;
            }
            else
            {
                Camera childCam = targetToFollow.GetComponentInChildren<Camera>();
                isRCCCameraActive = (childCam == null || childCam.enabled);
            }
        }

        if (isRCCCameraActive)
        {
            return targetToFollow;
        }

        // 2. Cutscenes (Start Cutscene ya Bridge Mid Cutscene):
        // RCCCamera disabled hai! RCCCamera aur uske children ko exclude kar ke active cutscene camera lein.
        Camera[] allCams = Camera.allCameras;
        for (int i = 0; i < allCams.Length; i++)
        {
            Camera c = allCams[i];
            if (c != null && c.gameObject.activeInHierarchy && c.enabled)
            {
                if (targetToFollow != null && (c.transform == targetToFollow || c.transform.IsChildOf(targetToFollow)))
                {
                    continue;
                }

                return c.transform;
            }
        }

        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam.gameObject.activeInHierarchy && mainCam.enabled)
        {
            return mainCam.transform;
        }

        return targetToFollow;
    }
}
