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

    [Header("--- Cutscene Cameras (Optional Direct References) ---")]
    [Tooltip("Start Cutscene ka camera (e.g. Camera (1))")]
    [SerializeField] private Camera startCutsceneCamera;

    [Tooltip("Middle Cutscene ka camera (e.g. Camera (1))")]
    [SerializeField] private Camera midCutsceneCamera;

    [Tooltip("Target se kitni unchai (Height) par rain emitter rahe")]
    [SerializeField] private float heightOffset = 6f;

    [Tooltip("Bus/Camera ke kitna aage rain gire (Driving direction mein)")]
    [SerializeField] private float forwardOffset = 10f;

    [Tooltip("Instant Follow: Bus/Camera ke sath 100% locked rahega")]
    [SerializeField] private bool instantFollow = true;

    [Tooltip("Agar instantFollow off ho to follow speed")]
    [SerializeField] private float followSpeed = 25f;

    [Header("--- Dynamic Speed Lead (Taiz Speed par Particles Aage Rakhna) ---")]
    [Tooltip("Kya taiz driving ke waqt rain particles ko bus ke aage project karna hai taake barish peeche na chhootay?")]
    [SerializeField] private bool enableSpeedLead = true;

    [Tooltip("Har 10 km/h speed par emitter kitne meters aage shift ho (Recommended: 2.0 - 3.5)")]
    [SerializeField] private float speedLeadFactor = 2.5f;

    [Tooltip("Taiz speed par maximum kitna aage shift ho sakta hai (Clamp limit meters mein, e.g. 25m - 35m)")]
    [SerializeField] private float maxSpeedLeadOffset = 30f;

    [Tooltip("Speed lead ki smoothness (jitna ziada hoga utna fast react karega, e.g. 8 - 15)")]
    [SerializeField] private float speedLeadSmoothness = 10f;

    [Tooltip("Ground Splashes par kitna speed lead effect aaye (0.5 - 0.8 recommended)")]
    [SerializeField] private float splashSpeedLeadMultiplier = 0.65f;

    [Header("--- Particle System ---")]
    [Tooltip("Rain Particle System (Child GameObject par laga FX_Rain_01)")]
    [SerializeField] private ParticleSystem rainParticleSystem;

    [Header("--- Fake Ground Splashes (Zero Lag) ---")]
    [Tooltip("Ground Splashes Particle System (FX_Ground_Splashes)")]
    [SerializeField] private ParticleSystem groundSplashParticleSystem;

    [Tooltip("Target se kitna aage splashes center rahein")]
    [SerializeField] private float splashForwardOffset = 5f;

    [Tooltip("Road surface se splashes kitna upar rahein taake road ke sath flicker na karein")]
    [SerializeField] private float groundSplashYOffset = 0.05f;

    [Tooltip("Single Raycast down to keep splashes pinned to the road (Zero CPU overhead)")]
    [SerializeField] private bool snapSplashesToRoad = true;

    [Tooltip("Road/Terrain detection ke liye layers (Default aur Offroad layers)")]
    [SerializeField] private LayerMask roadLayers = (1 << 0) | (1 << 13);

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

    [Header("--- Wet Road & Puddles Effect (Option B) ---")]
    [Tooltip("Kya barish ke dauran road ko wet (glossy reflections & darker soaked look) banana hai?")]
    [SerializeField] private bool enableWetRoad = true;

    [Tooltip("Road materials jinko wet look deni hai (e.g. Roadd, Road, Crossing). Agar inspector se assign na karein to scene se auto-discover honge.")]
    [SerializeField] private Material[] roadMaterials;

    [Tooltip("Wet road ki glossiness/smoothness (0.80 - 0.95: road par real water sheen aur reflections aati hain)")]
    [Range(0f, 1f)]
    [SerializeField] private float wetSmoothness = 0.88f;

    [Tooltip("Wet road ka darkening multiplier (0.70 - 0.85: geeli asphalt naturally dark ho jati hai)")]
    [Range(0.4f, 1f)]
    [SerializeField] private float wetDarkening = 0.80f;

    [Tooltip("Kya wetness ko smoothly blend (gradual soak-in) karna hai jab rain start ho?")]
    [SerializeField] private bool smoothWetTransition = true;

    [Tooltip("Wet transition time in seconds")]
    [SerializeField] private float wetTransitionDuration = 1.8f;

    [Header("--- Lightning & Thunder Flash ---")]
    [Tooltip("Kya dynamic lightning flash effect on rakhna hai?")]
    [SerializeField] private bool enableLightning = true;

    [Tooltip("Kam se kam kitne seconds baad bijli chamke")]
    [SerializeField] private float minLightningInterval = 10f;

    [Tooltip("Zyada se zyada kitne seconds baad bijli chamke")]
    [SerializeField] private float maxLightningInterval = 22f;

    [Tooltip("Bijli chamakne par light ki peak intensity (e.g. 2.0 - 2.5)")]
    [SerializeField] private float lightningPeakIntensity = 2.2f;

    [Tooltip("Bijli chamakne par light ka color (Cool storm white/cyan)")]
    [SerializeField] private Color lightningColor = new Color(0.92f, 0.96f, 1f, 1f);

    [Tooltip("Kya lightning ke waqt horizon fog bhi briefly chamke? (Ultra-realistic look)")]
    [SerializeField] private bool flashFog = true;

    [Tooltip("Optional: Thunder audio play karne ke liye AudioSource (agar lagana ho)")]
    [SerializeField] private AudioSource thunderAudioSource;

    [Tooltip("Optional: Thunder sound clips (random sound play karega)")]
    [SerializeField] private AudioClip[] thunderAudioClips;

    [Tooltip("Flash hone ke kitne second baad thunder aawaz aaye (Natural delay)")]
    [SerializeField] private float thunderAudioDelay = 0.45f;

    // Coroutine tracking
    private Coroutine _lightningCoroutine;
    private Coroutine _strikeCoroutine;
    private Coroutine _wetTransitionCoroutine;

    // Wet Road Backups
    [System.Serializable]
    private class RoadMaterialBackup
    {
        public Material material;
        public float originalGlossiness;
        public float originalGlossiness2;
        public Color originalColor;
        public bool hasColor;
        public bool hasGlossiness;
        public bool hasGlossiness2;
    }

    private readonly List<RoadMaterialBackup> _roadBackups = new List<RoadMaterialBackup>();

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
    private float _originalRainEmission = -1f;
    private int _originalRainMaxParticles = -1;
    private float _originalSplashEmission = -1f;
    private int _originalSplashMaxParticles = -1;

    private void Awake()
    {
        // Agar roadLayers mein All (~0) set ho to specifically Default (0) aur Offroad (13) par lock karein
        // Taake raycast gaari ke apne colliders / wheels par hit na kare aur CPU overhead na ho
        if (roadLayers.value == ~0 || roadLayers.value == 0)
        {
            int defaultLayer = LayerMask.NameToLayer("Default");
            int offroadLayer = LayerMask.NameToLayer("Offroad");
            int mask = 0;
            if (defaultLayer >= 0) mask |= (1 << defaultLayer);
            if (offroadLayer >= 0) mask |= (1 << offroadLayer);
            roadLayers = (mask != 0) ? mask : (1 << 0);
        }
    }

    private void ApplyParticleOptimization()
    {
        bool isBudgetDevice = (AutoGraphicSettings.EffectiveRamMB <= 4500);

        // 1. Optimize Rain Particle System
        if (rainParticleSystem != null)
        {
            var main = rainParticleSystem.main;
            var emission = rainParticleSystem.emission;

            if (_originalRainEmission < 0f)
            {
                _originalRainEmission = emission.rateOverTime.constant;
                _originalRainMaxParticles = main.maxParticles;
            }

            if (isBudgetDevice)
            {
                // 4GB devices par particle count ~45% optimize karein taake GPU fillrate/overdraw choke na ho
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(_originalRainEmission * 0.55f);
                main.maxParticles = Mathf.Max(50, Mathf.RoundToInt(_originalRainMaxParticles * 0.55f));
            }
            else
            {
                // 6GB+ devices par 100% full quality
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(_originalRainEmission);
                main.maxParticles = _originalRainMaxParticles;
            }
        }

        // 2. Optimize Ground Splashes Particle System
        if (groundSplashParticleSystem != null)
        {
            var main = groundSplashParticleSystem.main;
            var emission = groundSplashParticleSystem.emission;

            if (_originalSplashEmission < 0f)
            {
                _originalSplashEmission = emission.rateOverTime.constant;
                _originalSplashMaxParticles = main.maxParticles;
            }

            if (isBudgetDevice)
            {
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(_originalSplashEmission * 0.55f);
                main.maxParticles = Mathf.Max(30, Mathf.RoundToInt(_originalSplashMaxParticles * 0.55f));
            }
            else
            {
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(_originalSplashEmission);
                main.maxParticles = _originalSplashMaxParticles;
            }
        }
    }

    private void OnEnable()
    {
        // 0. Backup & Apply Rainy Skybox
        _originalSkybox = RenderSettings.skybox;
        if (rainSkybox != null)
        {
            RenderSettings.skybox = rainSkybox;
#if UNITY_EDITOR
            DynamicGI.UpdateEnvironment();
#endif
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

        // Apply particle budget optimization for 4GB vs 6GB+
        ApplyParticleOptimization();

        // 3. Play Rain Particles
        if (rainParticleSystem != null)
        {
            rainParticleSystem.gameObject.SetActive(true);
            if (!rainParticleSystem.isPlaying)
            {
                rainParticleSystem.Play();
            }
        }

        // 4. Play Ground Splashes
        if (groundSplashParticleSystem != null)
        {
            groundSplashParticleSystem.gameObject.SetActive(true);
            if (!groundSplashParticleSystem.isPlaying)
            {
                groundSplashParticleSystem.Play();
            }
        }

        // 5. Start Lightning Flash Loop
        if (enableLightning && directionalLight != null)
        {
            _lightningCoroutine = StartCoroutine(LightningLoop());
        }

        // 6. Apply Wet Road Effect (Option B)
        if (enableWetRoad)
        {
            ApplyWetRoad();
        }
    }

    private void OnDisable()
    {
        // Stop all lightning coroutines
        if (_lightningCoroutine != null)
        {
            StopCoroutine(_lightningCoroutine);
            _lightningCoroutine = null;
        }
        if (_strikeCoroutine != null)
        {
            StopCoroutine(_strikeCoroutine);
            _strikeCoroutine = null;
        }

        // 0. Restore Original Skybox
        if (_originalSkybox != null)
        {
            RenderSettings.skybox = _originalSkybox;
#if UNITY_EDITOR
            DynamicGI.UpdateEnvironment();
#endif
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

        // 4. Stop Ground Splashes
        if (groundSplashParticleSystem != null)
        {
            groundSplashParticleSystem.Stop();
        }

        // 5. Restore Road Materials (Dry look)
        if (enableWetRoad)
        {
            RestoreRoadMaterials();
        }
    }

    private void OnDestroy()
    {
        RestoreRoadMaterials();
    }

    private void OnApplicationQuit()
    {
        RestoreRoadMaterials();
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
            transform.position = targetPos;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);
        }

        // Rotation hamesha locked straight down taake barish naturally seedhi gire
        transform.rotation = Quaternion.identity;

        // Ground Splashes ko road surface par pin karein (with proportional speed lead)
        if (groundSplashParticleSystem != null)
        {
            float effectiveSplashForward = splashForwardOffset + (_currentSpeedLead * splashSpeedLeadMultiplier);
            Vector3 splashPos = activeTarget.position + (activeTarget.forward * effectiveSplashForward);

            if (snapSplashesToRoad)
            {
                // Single lightweight raycast down (0 CPU impact) to find exact road height
                if (Physics.Raycast(splashPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 30f, roadLayers))
                {
                    splashPos.y = hit.point.y + groundSplashYOffset;
                }
                else
                {
                    splashPos.y = activeTarget.position.y + groundSplashYOffset;
                }
            }
            else
            {
                splashPos.y = activeTarget.position.y + groundSplashYOffset;
            }

            groundSplashParticleSystem.transform.position = splashPos;
            groundSplashParticleSystem.transform.rotation = Quaternion.Euler(0f, activeTarget.eulerAngles.y, 0f);
        }
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
    private static Camera[] _cameraBuffer = new Camera[32];

    /// <summary>
    /// Gameplay mein RCCCamera ko follow karta hai.
    /// Cutscenes ke dauran (jab RCCCamera band ya disabled ho) automatically active Cutscene Camera ko follow karta hai.
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

        // Fallback: Camera.main
        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam.gameObject.activeInHierarchy && mainCam.enabled)
        {
            return mainCam.transform;
        }

        return targetToFollow;
    }

    #region Lightning System
    private IEnumerator LightningLoop()
    {
        while (true)
        {
            float waitTime = Random.Range(minLightningInterval, maxLightningInterval);
            yield return new WaitForSeconds(waitTime);

            if (enableLightning && directionalLight != null)
            {
                if (_strikeCoroutine != null) StopCoroutine(_strikeCoroutine);
                _strikeCoroutine = StartCoroutine(DoLightningStrike());
            }
        }
    }

    private IEnumerator DoLightningStrike()
    {
        if (directionalLight == null) yield break;

        // Strike 1: Rapid pre-flash (chamak ka pehla jhatka)
        directionalLight.intensity = lightningPeakIntensity * 0.75f;
        directionalLight.color = lightningColor;
        if (flashFog && enableRainFog)
        {
            RenderSettings.fogColor = Color.Lerp(rainFogColor, Color.white, 0.55f);
        }
        yield return new WaitForSeconds(0.04f);

        // Micro-dip (halka sa flicker)
        directionalLight.intensity = rainLightIntensity * 1.3f;
        if (flashFog && enableRainFog)
        {
            RenderSettings.fogColor = rainFogColor;
        }
        yield return new WaitForSeconds(0.035f);

        // Strike 2: Main intense flash!
        directionalLight.intensity = lightningPeakIntensity;
        directionalLight.color = lightningColor;
        if (flashFog && enableRainFog)
        {
            RenderSettings.fogColor = Color.Lerp(rainFogColor, Color.white, 0.8f);
        }

        // Trigger delayed thunder sound
        if (thunderAudioSource != null && thunderAudioClips != null && thunderAudioClips.Length > 0)
        {
            StartCoroutine(PlayDelayedThunder());
        }

        yield return new WaitForSeconds(0.08f);

        // Smooth decay back to gloomy rain light
        float elapsed = 0f;
        float fadeDuration = 0.22f;
        float startIntensity = directionalLight.intensity;
        Color startFog = RenderSettings.fogColor;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;

            if (directionalLight != null)
            {
                directionalLight.intensity = Mathf.Lerp(startIntensity, rainLightIntensity, t);
                directionalLight.color = Color.Lerp(lightningColor, rainLightColor, t);
            }

            if (flashFog && enableRainFog)
            {
                RenderSettings.fogColor = Color.Lerp(startFog, rainFogColor, t);
            }

            yield return null;
        }

        // Ensure strictly set to base rain lighting
        if (directionalLight != null)
        {
            directionalLight.intensity = rainLightIntensity;
            directionalLight.color = rainLightColor;
        }
        if (flashFog && enableRainFog)
        {
            RenderSettings.fogColor = rainFogColor;
        }

        _strikeCoroutine = null;
    }

    private IEnumerator PlayDelayedThunder()
    {
        yield return new WaitForSeconds(thunderAudioDelay);

        if (thunderAudioSource != null && thunderAudioClips != null && thunderAudioClips.Length > 0)
        {
            int randomIndex = Random.Range(0, thunderAudioClips.Length);
            AudioClip clip = thunderAudioClips[randomIndex];
            if (clip != null)
            {
                thunderAudioSource.PlayOneShot(clip);
            }
        }
    }
    #endregion

    #region Wet Road System (Option B)
    private void CollectRoadMaterialsIfNeeded()
    {
        if (roadMaterials != null && roadMaterials.Length > 0)
        {
            List<Material> valid = new List<Material>();
            for (int i = 0; i < roadMaterials.Length; i++)
            {
                if (roadMaterials[i] != null && !valid.Contains(roadMaterials[i]))
                {
                    valid.Add(roadMaterials[i]);
                }
            }
            roadMaterials = valid.ToArray();
            if (roadMaterials.Length > 0) return;
        }

        // Auto-discover distinct road materials from active renderers in scene
        List<Material> foundMats = new List<Material>();
        Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer r = allRenderers[i];
            if (r == null) continue;
            Material[] shared = r.sharedMaterials;
            if (shared == null) continue;

            for (int m = 0; m < shared.Length; m++)
            {
                Material mat = shared[m];
                if (mat != null && !foundMats.Contains(mat))
                {
                    string mName = mat.name.ToLower();
                    if (mName.Contains("road") || mName.Contains("crossing") || mName.Contains("asphalt"))
                    {
                        foundMats.Add(mat);
                    }
                }
            }
        }

        roadMaterials = foundMats.ToArray();
    }

    private void ApplyWetRoad()
    {
        CollectRoadMaterialsIfNeeded();
        if (roadMaterials == null || roadMaterials.Length == 0) return;

        // Backup original properties
        if (_roadBackups.Count == 0)
        {
            for (int i = 0; i < roadMaterials.Length; i++)
            {
                Material mat = roadMaterials[i];
                if (mat == null) continue;

                RoadMaterialBackup backup = new RoadMaterialBackup();
                backup.material = mat;

                if (mat.HasProperty("_Glossiness"))
                {
                    backup.hasGlossiness = true;
                    backup.originalGlossiness = mat.GetFloat("_Glossiness");
                }
                if (mat.HasProperty("_Glossiness2"))
                {
                    backup.hasGlossiness2 = true;
                    backup.originalGlossiness2 = mat.GetFloat("_Glossiness2");
                }
                if (mat.HasProperty("_Color"))
                {
                    backup.hasColor = true;
                    backup.originalColor = mat.GetColor("_Color");
                }

                _roadBackups.Add(backup);
            }
        }

        if (smoothWetTransition && Application.isPlaying)
        {
            if (_wetTransitionCoroutine != null) StopCoroutine(_wetTransitionCoroutine);
            _wetTransitionCoroutine = StartCoroutine(SmoothWetTransitionRoutine(true));
        }
        else
        {
            SetRoadWetStateImmediate(1f);
        }
    }

    private void RestoreRoadMaterials()
    {
        if (_wetTransitionCoroutine != null)
        {
            StopCoroutine(_wetTransitionCoroutine);
            _wetTransitionCoroutine = null;
        }

        for (int i = 0; i < _roadBackups.Count; i++)
        {
            RoadMaterialBackup b = _roadBackups[i];
            if (b != null && b.material != null)
            {
                if (b.hasGlossiness)
                {
                    b.material.SetFloat("_Glossiness", b.originalGlossiness);
                }
                if (b.hasGlossiness2)
                {
                    b.material.SetFloat("_Glossiness2", b.originalGlossiness2);
                }
                if (b.hasColor)
                {
                    b.material.SetColor("_Color", b.originalColor);
                }
            }
        }

        _roadBackups.Clear();
    }

    private IEnumerator SmoothWetTransitionRoutine(bool toWet)
    {
        float elapsed = 0f;
        while (elapsed < wetTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / wetTransitionDuration);
            float weight = toWet ? t : (1f - t);
            SetRoadWetStateImmediate(weight);
            yield return null;
        }

        SetRoadWetStateImmediate(toWet ? 1f : 0f);
        _wetTransitionCoroutine = null;
    }

    private void SetRoadWetStateImmediate(float wetWeight)
    {
        bool isBudgetDevice = (AutoGraphicSettings.EffectiveRamMB <= 4500);

        for (int i = 0; i < _roadBackups.Count; i++)
        {
            RoadMaterialBackup b = _roadBackups[i];
            if (b == null || b.material == null) continue;

            // 4GB devices: Smoothness moderate rakhein taake dynamic cubemap reflections freeze na de
            // 6GB+ devices: Full 0.88 wet smoothness
            float targetSmoothness = isBudgetDevice ? Mathf.Min(0.40f, wetSmoothness * 0.45f) : wetSmoothness;

            if (b.hasGlossiness)
            {
                float targetGloss = Mathf.Lerp(b.originalGlossiness, targetSmoothness, wetWeight);
                b.material.SetFloat("_Glossiness", targetGloss);
            }
            if (b.hasGlossiness2)
            {
                float targetGloss2 = Mathf.Lerp(b.originalGlossiness2, targetSmoothness, wetWeight);
                b.material.SetFloat("_Glossiness2", targetGloss2);
            }
            if (b.hasColor)
            {
                Color targetColor = Color.Lerp(b.originalColor, b.originalColor * wetDarkening, wetWeight);
                b.material.SetColor("_Color", targetColor);
            }

            // 4GB Devices: Turn OFF heavy cubemap specular highlights and glossy reflections (Zero shader overhead)
            // 6GB+ Devices: Keep crisp specular reflections and glossy highlights enabled (1f)
            if (b.material.HasProperty("_SpecularHighlights"))
            {
                b.material.SetFloat("_SpecularHighlights", isBudgetDevice ? 0f : 1f);
            }
            if (b.material.HasProperty("_GlossyReflections"))
            {
                b.material.SetFloat("_GlossyReflections", isBudgetDevice ? 0f : 1f);
            }
        }
    }
    #endregion
}
