using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cutscene controller exactly matching user's requested cinematic timing:
/// - Pre-instantiates/pools deer at Start() so ZERO game stutter/freeze occurs on trigger!
/// - Smooth cinematic camera swoop (2.0s) from behind the bus into 'Cutscene Camera Point'.
/// - Entire crossing pace is adjusted so cutscene lasts minimum 10+ seconds.
/// - When camera arrives, all 10 deers sprint smoothly across the road.
/// - Before last deer finishes crossing, camera smoothly swoops back towards the bus.
/// - Controls and UI seamlessly restore.
/// </summary>
public class DeerHerdStampedeCutscene : MonoBehaviour
{
    public static DeerHerdStampedeCutscene Instance { get; private set; }

    [Header("Trigger Collider")]
    [Tooltip("If not assigned, will use Collider on this GameObject.")]
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private bool disableTriggerMeshRenderer = true;

    [Header("Deer Herd Configuration")]
    [Tooltip("Stag prefab with Animator (e.g. DeerStag_PBR).")]
    [SerializeField] private GameObject deerStagPrefab;
    [Tooltip("Doe prefab with Animator (e.g. DeerDoe_PBR) for herd variety.")]
    [SerializeField] private GameObject deerDoePrefab;
    [Tooltip("Controller with default Run clip for Stag.")]
    [SerializeField] private RuntimeAnimatorController runAnimatorController;
    [Tooltip("Controller with default Run clip for Doe.")]
    [SerializeField] private RuntimeAnimatorController doeRunAnimatorController;
    [SerializeField] private int deerCount = 10;
    
    [Header("Cinematic Speed Settings (Adjusted for ~10-12s cutscene)")]
    [SerializeField] private float minSpeed = 6.8f;
    [SerializeField] private float maxSpeed = 8.5f;

    [Header("Customizable Crossing Path (Drag or Auto-detected)")]
    [Tooltip("Start point behind fence on grass (drag 'Deer Start Pos' here).")]
    public Transform spawnStartPoint;
    [Tooltip("End point across road (drag 'Deer End Pos' here).")]
    public Transform runEndPoint;
    [Tooltip("Width of the herd (left-to-right spread).")]
    [SerializeField] private float spawnSpreadWidth = 10f;
    [Tooltip("Depth of the herd (how far back trailing deers start).")]
    [SerializeField] private float herdFormationDepth = 22f;

    [Header("Customizable Roadside Camera")]
    [Tooltip("Agar TRUE hoga to camera hamesha Bus ke hisab se perfect side/front position lega (No clipping, best angle!)")]
    [SerializeField] private bool useBusRelativeCamera = true;
    [Tooltip("Bus ke relative camera kahan ho: X = Left(-)/Right(+), Y = Height, Z = Front")]
    [SerializeField] private Vector3 busRelativeOffset = new Vector3(-3.8f, 2.2f, 4.5f);

    [Tooltip("Optional: Agar useBusRelativeCamera false ho to yeh fix transform use hoga.")]
    public Transform cutsceneCameraPoint;
    [SerializeField] private float cameraFov = 42f;
    [SerializeField] private float cameraSwoopDuration = 2.0f;
    [SerializeField] private float cameraReturnDuration = 1.6f;
    [SerializeField] private float cameraPanFollowSpeed = 2.2f;

    [Header("Timing")]
    [Tooltip("Duration herd crosses the road before camera starts returning (Minimum 10s total).")]
    [SerializeField] private float herdCrossDuration = 8.0f;

    [Header("Stampede Audio (Drag and Drop AudioSources)")]
    [Tooltip("Drag 'deer running mp3' AudioSource here")]
    [SerializeField] private AudioSource deerRunningAudio;
    [Tooltip("Drag 'jungle bg sounds mp3' AudioSource here")]
    [SerializeField] private AudioSource jungleBgAudio;

    [Header("Fences / Obstacles To Hide During Cutscene")]
    [Tooltip("Road k darmiyan aane wali fences ko yahan drag karein, cutscene k waqt ye auto-disable hongi aur wapsi par enable")]
    [SerializeField] private GameObject[] fencesToDisable;

    [Header("Runtime References (Auto-detected if null)")]
    [SerializeField] private RCC_CarControllerV3 playerBus;
    [SerializeField] private GameObject rccCanvas;
    [SerializeField] private RCC_Camera rccCamera;

    private bool _hasTriggered = false;
    private DeerStampedePoolManager _poolManager;
    private bool _initialized = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (disableTriggerMeshRenderer)
        {
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
        }

        AutoFindPositionsAndMarkers();
    }

    private void OnEnable()
    {
        if (!_initialized)
        {
            AutoFindReferences();
            AutoFindPositionsAndMarkers();
            EnsurePool();
            _initialized = true;
        }
    }

    private void Start()
    {
        if (!_initialized)
        {
            AutoFindReferences();
            AutoFindPositionsAndMarkers();
            EnsurePool();
            _initialized = true;
        }
    }

    public void AutoFindPositionsAndMarkers()
    {
        if (spawnStartPoint == null)
        {
            GameObject startGo = GameObject.Find("Deer Start Pos") ?? GameObject.Find("deer start pos");
            if (startGo != null)
            {
                spawnStartPoint = startGo.transform;
            }
        }

        if (runEndPoint == null)
        {
            GameObject endGo = GameObject.Find("Deer End Pos") ?? GameObject.Find("deer end pos ") ?? GameObject.Find("deer end pos");
            if (endGo != null)
            {
                runEndPoint = endGo.transform;
            }
        }

        if (cutsceneCameraPoint == null)
        {
            GameObject camGo = GameObject.Find("Cutscene Camera Point") ?? GameObject.Find("cutscene camera point");
            if (camGo != null)
            {
                cutsceneCameraPoint = camGo.transform;
            }
        }

        if (Application.isPlaying)
        {
            if (spawnStartPoint != null) HideVisualMesh(spawnStartPoint.gameObject);
            if (runEndPoint != null) HideVisualMesh(runEndPoint.gameObject);
            if (cutsceneCameraPoint != null) HideVisualMesh(cutsceneCameraPoint.gameObject);
        }
    }

    private void HideVisualMesh(GameObject go)
    {
        if (go == null) return;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;
    }

    public void AutoFindReferences()
    {
        if (playerBus == null)
        {
            RCC_CarControllerV3[] cars = FindObjectsByType<RCC_CarControllerV3>(FindObjectsSortMode.None);
            foreach (var car in cars)
            {
                if (car.CompareTag(targetTag) || car.gameObject.activeInHierarchy)
                {
                    playerBus = car;
                    break;
                }
            }
        }

        if (rccCanvas == null)
        {
            GameObject canvasGo = GameObject.Find("RCCCanvas");
            if (canvasGo != null) rccCanvas = canvasGo;
        }

        if (rccCamera == null)
        {
            rccCamera = FindAnyObjectByType<RCC_Camera>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered) return;

        bool isPlayer = false;
        if (other.CompareTag(targetTag)) isPlayer = true;
        else if (other.transform.root != null && other.transform.root.CompareTag(targetTag)) isPlayer = true;
        else if (playerBus != null && (other.transform == playerBus.transform || other.transform.IsChildOf(playerBus.transform))) isPlayer = true;

        if (isPlayer)
        {
            _hasTriggered = true;
            StartGameplayCutsceneViaRunner();
        }
    }

    /// <summary>
    /// Called from Timeline Signal during Intro Cutscene.
    /// Runs ONLY the deer stampede for the camera view without touching UI or hijacking the camera.
    /// Leaves the in-game trigger fresh and active for Level 1 gameplay!
    /// </summary>
    public void TriggerCutscene()
    {
        EnsurePool();

        GameObject runnerGo = new GameObject("ActiveDeerStampedeRunner_Intro");
        var activeCutscene = runnerGo.AddComponent<ActiveStampedeExecutor>();
        activeCutscene.ExecuteIntro(this, _poolManager);
    }

    private void StartGameplayCutsceneViaRunner()
    {
        EnsurePool();

        GameObject runnerGo = new GameObject("ActiveDeerStampedeRunner_Gameplay");
        var activeCutscene = runnerGo.AddComponent<ActiveStampedeExecutor>();
        activeCutscene.ExecuteGameplay(this, _poolManager);

        gameObject.SetActive(false);
    }

    private void EnsurePool()
    {
        if (_poolManager == null)
        {
            GameObject poolGo = new GameObject($"{gameObject.name}_AnimalPool");
            _poolManager = poolGo.AddComponent<DeerStampedePoolManager>();
            _poolManager.InitializePool(deerCount, deerStagPrefab, deerDoePrefab, runAnimatorController, doeRunAnimatorController);
        }
    }

    public class ActiveStampedeExecutor : MonoBehaviour
    {
        public void ExecuteIntro(DeerHerdStampedeCutscene config, DeerStampedePoolManager pool)
        {
            StartCoroutine(RunIntroRoutine(config, pool));
        }

        public void ExecuteGameplay(DeerHerdStampedeCutscene config, DeerStampedePoolManager pool)
        {
            StartCoroutine(RunGameplayRoutine(config, pool));
        }

        private IEnumerator RunIntroRoutine(DeerHerdStampedeCutscene c, DeerStampedePoolManager pool)
        {
            c.AutoFindPositionsAndMarkers();

            Vector3 startCenter = c.spawnStartPoint != null ? c.spawnStartPoint.position : new Vector3(136.64f, 57.47f, -514.57f);
            Vector3 endCenter = c.runEndPoint != null ? c.runEndPoint.position : new Vector3(103.41f, 52.17f, -472.94f);

            DeerStampedeRunner herdTracker = null;
            if (pool != null)
            {
                pool.ActivateHerd(startCenter, endCenter, c.spawnSpreadWidth, c.herdFormationDepth, c.minSpeed, c.maxSpeed, out herdTracker);
                pool.StartHerdMovement();
            }

            yield return new WaitForSeconds(c.herdCrossDuration);

            if (pool != null)
            {
                pool.DeactivateAll();
            }

            Destroy(gameObject);
        }

        private IEnumerator RunGameplayRoutine(DeerHerdStampedeCutscene c, DeerStampedePoolManager pool)
        {
            Camera mainCam = c.rccCamera != null && c.rccCamera.actualCamera != null ? c.rccCamera.actualCamera : Camera.main;
            if (mainCam == null)
            {
                Camera[] allCams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                foreach (var cam in allCams)
                {
                    if (cam.enabled && cam.gameObject.activeInHierarchy)
                    {
                        mainCam = cam;
                        break;
                    }
                }
            }
            if (mainCam == null)
            {
                Debug.LogError("[DeerStampede] No main camera found!");
                yield break;
            }

            // 1. Freeze bus & input immediately
            c.StopBus();

            // 2. Hide UI
            c.SetGameplayUI(false);

            // 2.1 Pause BG Music
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PauseBGMusicForCutscene();
            }

            // 3. Disable RCC_Camera for direct cinematic control
            bool savedRccCamEnabled = false;
            if (c.rccCamera != null)
            {
                savedRccCamEnabled = c.rccCamera.enabled;
                c.rccCamera.enabled = false;
            }

            Vector3 startCamPos = mainCam.transform.position;
            Quaternion startCamRot = mainCam.transform.rotation;
            float startFov = mainCam.fieldOfView;

            // Target camera position & rotation
            Vector3 targetCamPos;
            Quaternion targetCamRot;

            Transform busTransform = c.playerBus != null ? c.playerBus.transform : (Camera.main != null ? Camera.main.transform : c.transform);

            // Deers ki crossing ka target point
            Vector3 deerLookPoint = c.spawnStartPoint != null ? c.spawnStartPoint.position : (busTransform.position + busTransform.forward * 20f);

            if (c.useBusRelativeCamera && busTransform != null)
            {
                // Bus ke orientation ke hisab se camera position (front-left angle outside the bus)
                targetCamPos = busTransform.position
                             + (busTransform.right * c.busRelativeOffset.x)
                             + (busTransform.up * c.busRelativeOffset.y)
                             + (busTransform.forward * c.busRelativeOffset.z);

                // Camera bus ke bahar khara ho kar seedha Animals / Crossing Point ki taraf dekhega!
                Vector3 toAnimals = (deerLookPoint - targetCamPos).normalized;
                targetCamRot = Quaternion.LookRotation(toAnimals, Vector3.up);
            }
            else if (c.cutsceneCameraPoint != null)
            {
                targetCamPos = c.cutsceneCameraPoint.position;
                targetCamRot = c.cutsceneCameraPoint.rotation;
            }
            else
            {
                targetCamPos = busTransform.position + new Vector3(-8f, 2.0f, 6f);
                targetCamPos = DeerStampedeRunner.SnapToGround(targetCamPos) + new Vector3(0, 1.8f, 0);

                Vector3 lookTarget = c.spawnStartPoint != null ? c.spawnStartPoint.position : (targetCamPos + new Vector3(30f, 3f, -15f));
                targetCamRot = Quaternion.LookRotation((lookTarget - targetCamPos).normalized, Vector3.up);
            }

            // 4. ACTIVATE PRE-POOLED HERD: Instant activation from pool, ZERO instantiate freeze!
            Transform herdFocus = null;
            DeerStampedeRunner herdTracker = null;

            Vector3 startCenter = c.spawnStartPoint != null ? c.spawnStartPoint.position : new Vector3(136.64f, 57.47f, -514.57f);
            Vector3 endCenter = c.runEndPoint != null ? c.runEndPoint.position : new Vector3(103.41f, 52.17f, -472.94f);

            if (pool != null)
            {
                herdFocus = pool.ActivateHerd(startCenter, endCenter, c.spawnSpreadWidth, c.herdFormationDepth, c.minSpeed, c.maxSpeed, out herdTracker);
                // Zebras start running IMMEDIATELY so they are already in full gallop as camera arrives!
                pool.StartHerdMovement();
            }

            // Jungle background audio plays right at cutscene start
            if (c.jungleBgAudio != null)
            {
                c.jungleBgAudio.Play();
            }

            // Disable road fences as camera starts rotating/swooping
            c.SetFencesActive(false);

            // 5. Smooth camera swoop forward past the bus
            float swoopTime = c.cameraSwoopDuration;
            float swoopElapsed = 0f;

            while (swoopElapsed < swoopTime)
            {
                swoopElapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, swoopElapsed / swoopTime);

                Vector3 currentPos = Vector3.Lerp(startCamPos, targetCamPos, t);
                currentPos.y += Mathf.Sin(t * Mathf.PI) * 1.5f;

                mainCam.transform.position = currentPos;
                mainCam.transform.rotation = Quaternion.Slerp(startCamRot, targetCamRot, t);
                mainCam.fieldOfView = Mathf.Lerp(startFov, c.cameraFov, t);

                yield return null;
            }

            mainCam.transform.position = targetCamPos;
            mainCam.transform.rotation = targetCamRot;
            mainCam.fieldOfView = c.cameraFov;

            // Running sound plays ONLY when camera has arrived at cinematic position!
            if (c.deerRunningAudio != null)
            {
                c.deerRunningAudio.Play();
            }

            // 6. Camera pans smoothly across with herd
            // Camera returns well before herd finishes crossing:
            // Swoop duration (2.3s) + Return duration (1.7s) + 1.0s safety margin subtracted
            float lookAtHerdDuration = Mathf.Max(2.0f, c.herdCrossDuration - c.cameraSwoopDuration - c.cameraReturnDuration - 1.0f);
            float elapsed = 0f;
            while (elapsed < lookAtHerdDuration)
            {
                elapsed += Time.deltaTime;

                if (mainCam != null && herdFocus != null)
                {
                    Vector3 lookTarget = herdFocus.position + new Vector3(0, 1.2f, 0);
                    Vector3 lookDir = (lookTarget - mainCam.transform.position).normalized;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        Quaternion panRot = Quaternion.LookRotation(lookDir, Vector3.up);
                        mainCam.transform.rotation = Quaternion.Slerp(mainCam.transform.rotation, panRot, c.cameraPanFollowSpeed * Time.deltaTime);
                    }
                }

                yield return null;
            }

            // 7. Camera smoothly swoops 100% back to bus gameplay position
            // Enable fences back as camera starts returning (pehle wala behavior)
            c.SetFencesActive(true);

            float returnTime = c.cameraReturnDuration;
            float returnElapsed = 0f;
            Vector3 endCamPos = mainCam.transform.position;
            Quaternion endCamRot = mainCam.transform.rotation;
            float endFov = mainCam.fieldOfView;

            while (returnElapsed < returnTime)
            {
                returnElapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, returnElapsed / returnTime);

                mainCam.transform.position = Vector3.Lerp(endCamPos, startCamPos, t);
                mainCam.transform.rotation = Quaternion.Slerp(endCamRot, startCamRot, t);
                mainCam.fieldOfView = Mathf.Lerp(endFov, startFov, t);

                yield return null;
            }

            // 8. 100% RETURNED: Camera is now fully back at bus view, looking away from zebras!
            // Clean up zebras and stop audio:
            if (pool != null) pool.DeactivateAll();
            if (c.deerRunningAudio != null) c.deerRunningAudio.Stop();
            if (c.jungleBgAudio != null) c.jungleBgAudio.Stop();

            // 9. Restore RCC Camera, Controls, and UI
            if (c.rccCamera != null)
            {
                c.rccCamera.enabled = savedRccCamEnabled;
            }
            mainCam.fieldOfView = startFov;

            c.RestoreBus();
            c.SetGameplayUI(true);

            // 10. Resume BG Music
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ResumeBGMusic();
            }

            Destroy(gameObject);
        }
    }

    public void StopBus()
    {
        if (playerBus != null)
        {
            playerBus.canControl = false;
            playerBus.handbrakeInput = 1f;

            Rigidbody rb = playerBus.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    public void RestoreBus()
    {
        if (playerBus != null)
        {
            playerBus.canControl = true;
            playerBus.handbrakeInput = 0f;
        }
    }

    public void SetGameplayUI(bool state)
    {
        if (rccCanvas != null)
        {
            rccCanvas.SetActive(state);
        }
    }

    public void SetFencesActive(bool active)
    {
        if (fencesToDisable == null) return;
        for (int i = 0; i < fencesToDisable.Length; i++)
        {
            if (fencesToDisable[i] != null)
            {
                fencesToDisable[i].SetActive(active);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Vector3 startCenter = spawnStartPoint != null ? spawnStartPoint.position : new Vector3(136.64f, 57.47f, -514.57f);
        Vector3 endCenter = runEndPoint != null ? runEndPoint.position : new Vector3(103.41f, 52.17f, -472.94f);

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(startCenter, 2f);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(endCenter, 2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(startCenter, endCenter);

        if (cutsceneCameraPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(cutsceneCameraPoint.position, 1.0f);
            Gizmos.DrawLine(cutsceneCameraPoint.position, (startCenter + endCenter) * 0.5f);
        }
    }
}
