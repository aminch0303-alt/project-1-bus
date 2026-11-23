using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Bridge Cutscene Manager:
/// - Attached to Bridge's 'Middle Animation trigger'.
/// - Detects which bus the player is currently driving (Bus 1, Bus 2, or Bus 3).
/// - Temporarily hides real player bus and driving UI.
/// - Activates the matching dummy bus under 'Buses (1)' in the Timeline.
/// - Plays the Timeline (PlayableDirector).
/// - Once Timeline finishes, restores the player bus at the exit point, re-enables UI, and resumes gameplay smoothly.
/// </summary>
public class BridgeCutsceneManager : MonoBehaviour
{
    [Header("Timeline Reference")]
    [Tooltip("Hierarchy se 'Middle Animation' GameObject drag karein (jis par PlayableDirector laga hai)")]
    [SerializeField] private PlayableDirector playableDirector;

    [Header("Cutscene Buses (Under 'Buses (1)')")]
    [Tooltip("Buses (1) k andar jo Bus 1 hai")]
    [SerializeField] private GameObject cutsceneBus1;
    [Tooltip("Buses (1) k andar jo Bus 2 hai")]
    [SerializeField] private GameObject cutsceneBus2;
    [Tooltip("Buses (1) k andar jo Bus 3 hai")]
    [SerializeField] private GameObject cutsceneBus3;

    [Header("Bridge Exit Resume Point (Optional)")]
    [Tooltip("Cutscene khatam hone par real bus jahan khari honi chahiye (agar khali ho to jahan trigger laga tha wahan se aage continue karegi)")]
    [SerializeField] private Transform bridgeExitPoint;

    [Header("--- Screen Fade Reference ---")]
    [Tooltip("Hierarchy se ScreenFadeCanvas drag karein")]
    [SerializeField] private ScreenFadeManager screenFadeManager;

    [Header("Transition Settings")]
    [Tooltip("Trigger lagne par bus kitne seconds mein naturally stop hogi")]
    [SerializeField] private float busStopDuration = 0.8f;
    [Tooltip("Bus rukne ke baad black screen fade out hone ki duration")]
    [SerializeField] private float fadeOutDuration = 0.45f;

    [Header("Trigger Settings")]
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private bool disableTriggerMeshRenderer = true;

    private bool _hasTriggered = false;
    private RCC_CarControllerV3 _activePlayerBus;
    private Rigidbody _busRigidbody;
    private GameObject _rccCanvas;
    private GameObject _controllerButtonsRoot;
    private RCC_Camera _rccCamera;
    private bool _savedCameraState = true;
    private List<Renderer> _activePlayerBusRenderers = new List<Renderer>();

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (disableTriggerMeshRenderer)
        {
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
        }

        // Hide all cutscene dummy buses at startup so they don't sit on the bridge during gameplay
        SetCutsceneBusesActive(false, false, false);

        // Ensure Middle Animation (and its camera / audio listener) is OFF at startup so gameplay camera runs properly
        if (playableDirector != null && playableDirector.gameObject != this.gameObject)
        {
            playableDirector.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        CacheSceneReferences();
    }

    private void CacheSceneReferences()
    {
        if (playableDirector == null)
        {
            playableDirector = GetComponentInParent<PlayableDirector>();
            if (playableDirector == null)
            {
                GameObject midAnim = GameObject.Find("Middle Animation");
                if (midAnim != null) playableDirector = midAnim.GetComponent<PlayableDirector>();
            }
        }

        if (_rccCanvas == null)
        {
            _rccCanvas = GameObject.Find("RCCCanvas");
            if (_rccCanvas != null)
            {
                Transform btnTrans = _rccCanvas.transform.Find("Controller Buttons");
                if (btnTrans != null) _controllerButtonsRoot = btnTrans.gameObject;
            }
        }

        if (_rccCamera == null)
        {
            _rccCamera = FindAnyObjectByType<RCC_Camera>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger hit by: " + other.gameObject.name);
        if (_hasTriggered) return;

        bool isPlayer = false;
        RCC_CarControllerV3 car = other.GetComponentInParent<RCC_CarControllerV3>();
        if (car == null && other.transform.root != null)
        {
            car = other.transform.root.GetComponentInChildren<RCC_CarControllerV3>();
        }

        if (car != null) isPlayer = true;
        else if (other.CompareTag(targetTag) || (other.transform.root != null && other.transform.root.CompareTag(targetTag))) isPlayer = true;

        if (isPlayer)
        {
            _hasTriggered = true;
            _activePlayerBus = car;
            StartCoroutine(RunBridgeCutsceneRoutine());
        }
    }

    private IEnumerator RunBridgeCutsceneRoutine()
    {
        CacheSceneReferences();

        // 1. Immediately turn off driving UI & background music
        SetDrivingUIActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PauseBGMusicForCutscene();
        }

        // 2. Smoothly apply brakes & slow bus down naturally
        if (_activePlayerBus != null)
        {
            _activePlayerBus.overrideInputs = true;
            _activePlayerBus.canControl = false;
            _activePlayerBus.handbrakeInput = 1f;
            _activePlayerBus.brakeInput = 1f;
            _activePlayerBus.throttleInput = 0f;

            _busRigidbody = _activePlayerBus.GetComponent<Rigidbody>();
        }

        // Wait for the bus to naturally glide and come to a stop
        yield return new WaitForSeconds(busStopDuration);

        // Fully freeze bus physics after slowing down
        if (_activePlayerBus != null)
        {
            _activePlayerBus.speed = 0f;
            if (_busRigidbody != null)
            {
                _busRigidbody.linearVelocity = Vector3.zero;
                _busRigidbody.angularVelocity = Vector3.zero;
                _busRigidbody.isKinematic = true;
            }
        }

        // 3. Smooth Fade Out to Black
        yield return StartCoroutine(FadeOutScreenRoutine(fadeOutDuration));

        // 4. Identify which bus is active (Bus 1, Bus 2, or Bus 3)
        int busIndex = GetActiveBusIndex();

        // 5. Hide real player bus visual renderers (while screen is completely black)
        if (_activePlayerBus != null)
        {
            _activePlayerBusRenderers.Clear();
            Renderer[] allRenderers = _activePlayerBus.GetComponentsInChildren<Renderer>(true);
            foreach (var r in allRenderers)
            {
                if (r == null) continue;

                string parentHierarchy = GetParentPath(r.transform).ToLower();
                if (parentHierarchy.Contains("way point") || parentHierarchy.Contains("waypoint") || parentHierarchy.Contains("passenger up"))
                {
                    r.enabled = false;
                    continue;
                }

                if (r.enabled)
                {
                    _activePlayerBusRenderers.Add(r);
                    r.enabled = false;
                }
            }
        }

        // 6. Disable RCC Camera while timeline cameras take over
        if (_rccCamera != null)
        {
            _savedCameraState = _rccCamera.enabled;
            _rccCamera.enabled = false;
        }

        // 7. Activate the matching cutscene dummy bus
        SetCutsceneBusesActive(busIndex == 1, busIndex == 2, busIndex == 3);

        // Remove the fade overlay right before timeline plays (Timeline has its own built-in fade-in at start)
        DestroyFadeOverlay();

        // 8. Play the Timeline
        if (playableDirector != null)
        {
            playableDirector.gameObject.SetActive(true);
            playableDirector.time = 0;
            playableDirector.Play();

            // Wait until timeline completes
            while (playableDirector.state == PlayState.Playing)
            {
                yield return null;
            }

            // Timeline finished: turn Middle Animation root OFF again
            playableDirector.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(5f);
        }

        // 9. Cutscene finished: Hide all cutscene dummy buses
        SetCutsceneBusesActive(false, false, false);

        // 10. Reposition real player bus at bridge exit (if assigned)
        if (_activePlayerBus != null)
        {
            if (bridgeExitPoint != null)
            {
                _activePlayerBus.transform.position = bridgeExitPoint.position;
                _activePlayerBus.transform.rotation = bridgeExitPoint.rotation;
            }

            if (_busRigidbody != null)
            {
                _busRigidbody.isKinematic = false;
                _busRigidbody.linearVelocity = Vector3.zero;
                _busRigidbody.angularVelocity = Vector3.zero;
            }

            // Make real bus visible again
            SetPlayerBusVisible(true);

            // Re-enable player controls
            _activePlayerBus.overrideInputs = false;
            _activePlayerBus.canControl = true;
            _activePlayerBus.handbrakeInput = 0f;
            _activePlayerBus.brakeInput = 0f;

            // Re-register with RCC to guarantee clean controls
            RCC.RegisterPlayerVehicle(_activePlayerBus, true, true);
        }

        // 9. Restore RCC Camera and Driving UI
        if (_rccCamera != null)
        {
            _rccCamera.enabled = _savedCameraState;
        }

        SetDrivingUIActive(true);

        // Resume BG music
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResumeBGMusic();
        }

        // Destroy trigger so it never fires twice
        Destroy(gameObject);
    }

    private int GetActiveBusIndex()
    {
        if (_activePlayerBus != null)
        {
            string busName = _activePlayerBus.gameObject.name.ToLower();
            if (busName.Contains("bus 1") || busName.Contains("bus1")) return 1;
            if (busName.Contains("bus 2") || busName.Contains("bus2")) return 2;
            if (busName.Contains("bus 3") || busName.Contains("bus3")) return 3;
        }

        // Check active buses under 'Players' root in Hierarchy
        GameObject playersRoot = GameObject.Find("Players");
        if (playersRoot != null)
        {
            for (int i = 0; i < playersRoot.transform.childCount; i++)
            {
                Transform child = playersRoot.transform.GetChild(i);
                if (child.gameObject.activeInHierarchy)
                {
                    string cName = child.name.ToLower();
                    if (cName.Contains("bus 1") || cName.Contains("bus1")) return 1;
                    if (cName.Contains("bus 2") || cName.Contains("bus2")) return 2;
                    if (cName.Contains("bus 3") || cName.Contains("bus3")) return 3;
                }
            }
        }

        return 1; // Default fallback to Bus 1
    }

    private void SetCutsceneBusesActive(bool b1, bool b2, bool b3)
    {
        if (cutsceneBus1 != null) cutsceneBus1.SetActive(b1);
        if (cutsceneBus2 != null) cutsceneBus2.SetActive(b2);
        if (cutsceneBus3 != null) cutsceneBus3.SetActive(b3);
    }

    private void SetPlayerBusVisible(bool visible)
    {
        if (_activePlayerBusRenderers != null)
        {
            for (int i = 0; i < _activePlayerBusRenderers.Count; i++)
            {
                if (_activePlayerBusRenderers[i] != null)
                {
                    _activePlayerBusRenderers[i].enabled = visible;
                }
            }
        }
    }

    private string GetParentPath(Transform current)
    {
        if (current == null) return string.Empty;
        string path = current.name;
        Transform p = current.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }

    private void SetDrivingUIActive(bool active)
    {
        ResetAllUIButtons();

        if (_controllerButtonsRoot != null)
        {
            _controllerButtonsRoot.SetActive(active);
        }

        RCC_UIDashboardDisplay dashboard = FindAnyObjectByType<RCC_UIDashboardDisplay>();
        if (dashboard != null)
        {
            dashboard.enabled = active;
            dashboard.SetDisplayType(active ? RCC_UIDashboardDisplay.DisplayType.Full : RCC_UIDashboardDisplay.DisplayType.Off);
        }

        RCC_MobileButtons mobileButtons = FindAnyObjectByType<RCC_MobileButtons>();
        if (mobileButtons != null)
        {
            mobileButtons.enabled = active;
        }

        ResetAllUIButtons();
    }

    private void ResetAllUIButtons()
    {
        if (_controllerButtonsRoot != null)
        {
            RCC_UIController[] uiButtons = _controllerButtonsRoot.GetComponentsInChildren<RCC_UIController>(true);
            for (int i = 0; i < uiButtons.Length; i++)
            {
                if (uiButtons[i] != null)
                {
                    uiButtons[i].pressing = false;
                    uiButtons[i].input = 0f;
                }
            }
        }

        if (RCC_MobileButtons.mobileInputs != null)
        {
            RCC_MobileButtons.mobileInputs.throttleInput = 0f;
            RCC_MobileButtons.mobileInputs.brakeInput = 0f;
            RCC_MobileButtons.mobileInputs.steerInput = 0f;
            RCC_MobileButtons.mobileInputs.handbrakeInput = 0f;
            RCC_MobileButtons.mobileInputs.boostInput = 0f;
        }

        if (RCC_InputManager.Instance != null && RCC_InputManager.Instance.inputs != null)
        {
            RCC_InputManager.Instance.inputs.throttleInput = 0f;
            RCC_InputManager.Instance.inputs.brakeInput = 0f;
            RCC_InputManager.Instance.inputs.steerInput = 0f;
            RCC_InputManager.Instance.inputs.handbrakeInput = 0f;
            RCC_InputManager.Instance.inputs.boostInput = 0f;
        }
    }

    private IEnumerator FadeOutScreenRoutine(float duration)
    {
        ScreenFadeManager fade = screenFadeManager != null ? screenFadeManager : ScreenFadeManager.Instance;
        if (fade != null)
        {
            yield return fade.FadeOutRoutine(duration);
        }
    }

    private void DestroyFadeOverlay()
    {
        ScreenFadeManager fade = screenFadeManager != null ? screenFadeManager : ScreenFadeManager.Instance;
        if (fade != null)
        {
            fade.SetAlphaImmediate(0f);
        }
    }
    /// <summary>
    /// Level 2 Middle Animation ko foran skip karne ke liye (UI Skip Button OnClick)
    /// </summary>
    public void SkipCutscene()
    {
        if (!_hasTriggered) return;

        // 1. Running cutscene coroutines band karein
        StopAllCoroutines();

        // 2. Timeline director stop karein aur Middle Animation GameObject OFF karein
        if (playableDirector != null)
        {
            playableDirector.Stop();
            playableDirector.gameObject.SetActive(false);
        }

        // 3. Fade overlay agar screen par hai to destroy karein
        DestroyFadeOverlay();

        // 4. Dummy cutscene buses ko disable karein
        SetCutsceneBusesActive(false, false, false);

        // 5. Asal player bus ko exit point par place karein aur controls restore karein
        if (_activePlayerBus != null)
        {
            if (bridgeExitPoint != null)
            {
                _activePlayerBus.transform.position = bridgeExitPoint.position;
                _activePlayerBus.transform.rotation = bridgeExitPoint.rotation;
            }

            if (_busRigidbody != null)
            {
                _busRigidbody.isKinematic = false;
                _busRigidbody.linearVelocity = Vector3.zero;
                _busRigidbody.angularVelocity = Vector3.zero;
            }

            // Real bus ke renderers wapis ON karein
            SetPlayerBusVisible(true);

            // Controls & engine release karein
            _activePlayerBus.overrideInputs = false;
            _activePlayerBus.canControl = true;
            _activePlayerBus.handbrakeInput = 0f;
            _activePlayerBus.brakeInput = 0f;
            _activePlayerBus.StartEngine();

            // RCC ke sath re-register karein
            RCC.RegisterPlayerVehicle(_activePlayerBus, true, true);
        }

        // 6. RCC Camera aur Driving UI restore karein
        if (_rccCamera != null)
        {
            _rccCamera.enabled = _savedCameraState;
        }

        SetDrivingUIActive(true);

        // 7. Background music resume karein
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResumeBGMusic();
        }

        // 8. Trigger destroy karein taake dobara trigger na ho
        Destroy(gameObject);
    }
}
