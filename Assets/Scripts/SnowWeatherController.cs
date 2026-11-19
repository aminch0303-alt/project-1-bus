using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Snow Weather Controller:
/// - Level 3 ke liye snow effect aur winter atmosphere manage karta hai.
/// - 100% Direct References: Koi Find / FindObjectByType call nahi, direct drag and drop.
/// - Zero performance overhead for mobile.
/// </summary>
public class SnowWeatherController : MonoBehaviour
{
    [Header("--- Target to Follow ---")]
    [Tooltip("Hierarchy se 'RCCCamera' ya Bus drag karein")]
    [SerializeField] private Transform targetToFollow;

    [Tooltip("Target se kitni unchai (Height) par snow emitter rahe")]
    [SerializeField] private float heightOffset = 6f;

    [Tooltip("Bus/Camera ke kitna aage snow gire (Driving direction mein)")]
    [SerializeField] private float forwardOffset = 5f;

    [Tooltip("Instant Follow: Bus ke sath 100% chipak kar chale (Taiz speed mein peeche na chhootay)")]
    [SerializeField] private bool instantFollow = true;

    [Tooltip("Agar instantFollow off ho to follow speed")]
    [SerializeField] private float followSpeed = 25f;

    [Header("--- Particle System ---")]
    [Tooltip("Snow Particle System")]
    [SerializeField] private ParticleSystem snowParticleSystem;

    [Header("--- Winter Fog Settings ---")]
    [Tooltip("Kya winter fog on karni hai?")]
    [SerializeField] private bool enableWinterFog = true;

    [Tooltip("Snow fog ka color (Soft Ice Blue / Light Grey)")]
    [SerializeField] private Color snowFogColor = new Color(0.82f, 0.88f, 0.93f, 1f);

    [Tooltip("Fog Density (Exponential Squared mode ke liye 0.005 - 0.009 recommended)")]
    [SerializeField] private float snowFogDensity = 0.007f;

    [Header("--- Winter Lighting ---")]
    [Tooltip("Kya sunlight ko winter cool tint mein adjust karna hai?")]
    [SerializeField] private bool adjustDirectionalLight = true;

    [Tooltip("Hierarchy se 'Directional Light' drag karein")]
    [SerializeField] private Light directionalLight;

    [Tooltip("Winter sun ki intensity")]
    [SerializeField] private float winterLightIntensity = 0.7f;

    [Tooltip("Winter sunlight ka color")]
    [SerializeField] private Color winterLightColor = new Color(0.88f, 0.92f, 1f, 1f);

    [Header("--- Winter Skybox (Optional) ---")]
    [Tooltip("Level 3 ke liye Cloudy / Winter Skybox Material. Agar assign karein to sirf Level 3 mein yeh lag jayega aur disable hone par normal wapis aa jayega.")]
    [SerializeField] private Material winterSkybox;

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
        // 0. Backup & Apply Winter Skybox
        _originalSkybox = RenderSettings.skybox;
        if (winterSkybox != null)
        {
            RenderSettings.skybox = winterSkybox;
            DynamicGI.UpdateEnvironment();
        }

        // 1. Backup & Apply Winter Fog
        if (enableWinterFog)
        {
            _originalFogEnabled = RenderSettings.fog;
            _originalFogColor = RenderSettings.fogColor;
            _originalFogDensity = RenderSettings.fogDensity;
            _originalFogMode = RenderSettings.fogMode;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = snowFogColor;
            RenderSettings.fogDensity = snowFogDensity;
        }

        // 2. Backup & Apply Winter Lighting
        if (adjustDirectionalLight && directionalLight != null)
        {
            if (!_hasLightBackup)
            {
                _originalLightIntensity = directionalLight.intensity;
                _originalLightColor = directionalLight.color;
                _hasLightBackup = true;
            }

            directionalLight.intensity = winterLightIntensity;
            directionalLight.color = winterLightColor;
        }

        // 3. Play Snow Particles
        if (snowParticleSystem != null)
        {
            snowParticleSystem.gameObject.SetActive(true);
            if (!snowParticleSystem.isPlaying)
            {
                snowParticleSystem.Play();
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
        if (enableWinterFog)
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

        // 3. Stop Snow Particles
        if (snowParticleSystem != null)
        {
            snowParticleSystem.Stop();
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
            // Zero lag follow: Taiz raftaar bus / camera ke sath hamesha locked rahega
            transform.position = targetPos;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);
        }

        // Rotation hamesha locked straight down
        transform.rotation = Quaternion.identity;
    }

    /// <summary>
    /// Gameplay mein RCCCamera ko follow karta hai.
    /// Cutscenes ke dauran (jab RCCCamera band ya disabled ho) automatically active Cutscene Camera (e.g. Camera (2)) ko follow karta hai.
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

        // 2. Cutscenes (Start Cutscene ya Mid Cutscene):
        // RCCCamera disabled hai! RCCCamera aur uske tamam child cameras ko EXCLUDE karein aur sirf actual Cutscene Camera (jaise Camera (2)) dhoondein.
        Camera[] allCams = Camera.allCameras;
        for (int i = 0; i < allCams.Length; i++)
        {
            Camera c = allCams[i];
            if (c != null && c.gameObject.activeInHierarchy && c.enabled)
            {
                // Exclude RCCCamera and all of its children!
                if (targetToFollow != null && (c.transform == targetToFollow || c.transform.IsChildOf(targetToFollow)))
                {
                    continue;
                }

                return c.transform;
            }
        }

        // Fallback: Agar koi cutscene camera na mile to Camera.main
        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam.gameObject.activeInHierarchy && mainCam.enabled)
        {
            return mainCam.transform;
        }

        return targetToFollow;
    }
}
