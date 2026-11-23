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

    [Header("--- Cutscene Cameras (Optional Direct References) ---")]
    [Tooltip("Level 3 Start Cutscene ka camera (Level 3 start anim -> Camera (1))")]
    [SerializeField] private Camera startCutsceneCamera;

    [Tooltip("Level 3 Middle Cutscene ka camera (Middle animation timeline -> Camera (2))")]
    [SerializeField] private Camera midCutsceneCamera;

    [Tooltip("Target se kitni unchai (Height) par snow emitter rahe")]
    [SerializeField] private float heightOffset = 6f;

    [Tooltip("Bus/Camera ke kitna aage snow gire (Driving direction mein)")]
    [SerializeField] private float forwardOffset = 5f;

    [Tooltip("Instant Follow: Bus ke sath 100% chipak kar chale (Taiz speed mein peeche na chhootay)")]
    [SerializeField] private bool instantFollow = true;

    [Tooltip("Agar instantFollow off ho to follow speed")]
    [SerializeField] private float followSpeed = 25f;

    [Header("--- Dynamic Speed Lead (Taiz Speed par Particles Aage Rakhna) ---")]
    [Tooltip("Kya taiz driving ke waqt snow particles ko bus ke aage project karna hai taake particles peeche na chhootein?")]
    [SerializeField] private bool enableSpeedLead = true;

    [Tooltip("Har 10 km/h speed par emitter kitne meters aage shift ho (Recommended: 2.0 - 3.5)")]
    [SerializeField] private float speedLeadFactor = 2.5f;

    [Tooltip("Taiz speed par maximum kitna aage shift ho sakta hai (Clamp limit meters mein, e.g. 25m - 35m)")]
    [SerializeField] private float maxSpeedLeadOffset = 30f;

    [Tooltip("Speed lead ki smoothness (jitna ziada hoga utna fast react karega, e.g. 8 - 15)")]
    [SerializeField] private float speedLeadSmoothness = 10f;

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

    // Particle Optimization backups for 4GB vs 6GB+
    private float _originalSnowEmission = -1f;
    private int _originalSnowMaxParticles = -1;

    private void ApplyParticleOptimization()
    {
        bool isBudgetDevice = (AutoGraphicSettings.EffectiveRamMB <= 4500);

        if (snowParticleSystem != null)
        {
            var main = snowParticleSystem.main;
            var emission = snowParticleSystem.emission;

            if (_originalSnowEmission < 0f)
            {
                _originalSnowEmission = emission.rateOverTime.constant;
                _originalSnowMaxParticles = main.maxParticles;
            }

            if (isBudgetDevice)
            {
                // 4GB devices par snow count ~45% reduce karein taake overdraw GPU fillrate choke na ho
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(_originalSnowEmission * 0.55f);
                main.maxParticles = Mathf.Max(50, Mathf.RoundToInt(_originalSnowMaxParticles * 0.55f));
            }
            else
            {
                // 6GB+ devices par 100% full particle count
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(_originalSnowEmission);
                main.maxParticles = _originalSnowMaxParticles;
            }
        }
    }

    private void OnEnable()
    {
        // 0. Backup & Apply Winter Skybox
        _originalSkybox = RenderSettings.skybox;
        if (winterSkybox != null)
        {
            RenderSettings.skybox = winterSkybox;
#if UNITY_EDITOR
            DynamicGI.UpdateEnvironment();
#endif
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

        // Apply particle budget optimization for 4GB vs 6GB+
        ApplyParticleOptimization();

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
#if UNITY_EDITOR
            DynamicGI.UpdateEnvironment();
#endif
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

    private float _currentSpeedLead = 0f;
    private Vector3 _lastTargetPos;
    private bool _hasLastPos = false;
    private RCC_CarControllerV3 _cachedCarController;

    private void LateUpdate()
    {
        Transform activeTarget = GetActiveTargetTransform();
        if (activeTarget == null) return;

        // 1. Calculate dynamic speed lead
        float targetSpeedLead = 0f;
        if (enableSpeedLead)
        {
            float speedKmh = GetCurrentVehicleSpeedKmh(activeTarget);
            targetSpeedLead = Mathf.Clamp((speedKmh / 10f) * speedLeadFactor, 0f, maxSpeedLeadOffset);
        }

        if (instantFollow)
        {
            _currentSpeedLead = targetSpeedLead;
        }
        else
        {
            _currentSpeedLead = Mathf.Lerp(_currentSpeedLead, targetSpeedLead, Time.deltaTime * speedLeadSmoothness);
        }

        // 2. Target position with dynamic forward lead
        float effectiveForward = forwardOffset + _currentSpeedLead;
        Vector3 targetPos = activeTarget.position + (Vector3.up * heightOffset) + (activeTarget.forward * effectiveForward);

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

    private float GetCurrentVehicleSpeedKmh(Transform activeTarget)
    {
        // 1. Check direct RCC Car Controller via GameManager (Zero scene search)
        if (_cachedCarController == null || !_cachedCarController.gameObject.activeInHierarchy)
        {
            if (GameManager.Instance != null)
            {
                _cachedCarController = GameManager.Instance.GetCurrentPlayerCarController();
            }
        }

        if (_cachedCarController != null && _cachedCarController.gameObject.activeInHierarchy)
        {
            return Mathf.Abs(_cachedCarController.speed);
        }

        // 2. Fallback: Velocity via position delta
        if (!_hasLastPos)
        {
            _lastTargetPos = activeTarget.position;
            _hasLastPos = true;
            return 0f;
        }

        float dt = Time.deltaTime;
        if (dt > 0.0001f)
        {
            float dist = Vector3.Distance(activeTarget.position, _lastTargetPos);
            _lastTargetPos = activeTarget.position;
            return (dist / dt) * 3.6f; // m/s to km/h
        }

        return 0f;
    }

    private Camera _cachedCutsceneCamera;
    private static Camera[] _cameraBuffer = new Camera[16];

    /// <summary>
    /// Gameplay mein RCCCamera ko follow karta hai.
    /// Cutscenes ke dauran (jab RCCCamera band ya disabled ho) automatically active Cutscene Camera (e.g. Camera (2)) ko follow karta hai.
    /// 100% Zero GC Allocations on mobile!
    /// </summary>
    private Transform GetActiveTargetTransform()
    {
        // 1. Normal Gameplay Check: Assigned target (RCCCamera)
        if (targetToFollow != null && targetToFollow.gameObject.activeInHierarchy)
        {
            RCC_Camera rccCam = targetToFollow.GetComponent<RCC_Camera>();
            if (rccCam != null)
            {
                if (rccCam.enabled) return targetToFollow;
            }
            else
            {
                Camera childCam = targetToFollow.GetComponentInChildren<Camera>();
                if (childCam == null || childCam.enabled) return targetToFollow;
            }
        }

        // 2. Direct Assigned Cutscene Cameras (Instant, 0 CPU Cost)
        if (startCutsceneCamera != null && startCutsceneCamera.gameObject.activeInHierarchy && startCutsceneCamera.enabled)
        {
            return startCutsceneCamera.transform;
        }

        if (midCutsceneCamera != null && midCutsceneCamera.gameObject.activeInHierarchy && midCutsceneCamera.enabled)
        {
            return midCutsceneCamera.transform;
        }

        // 3. Fast Cached Cutscene Camera (Zero allocation)
        if (_cachedCutsceneCamera != null && _cachedCutsceneCamera.gameObject.activeInHierarchy && _cachedCutsceneCamera.enabled)
        {
            return _cachedCutsceneCamera.transform;
        }

        // 4. Zero-GC Camera Search (Reuses pre-allocated buffer without heap allocation)
        int camCount = Camera.GetAllCameras(_cameraBuffer);

        for (int i = 0; i < camCount; i++)
        {
            Camera c = _cameraBuffer[i];
            if (c != null && c.gameObject.activeInHierarchy && c.enabled)
            {
                // Exclude RCCCamera and all of its children!
                if (targetToFollow != null && (c.transform == targetToFollow || c.transform.IsChildOf(targetToFollow)))
                {
                    continue;
                }

                _cachedCutsceneCamera = c;
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
