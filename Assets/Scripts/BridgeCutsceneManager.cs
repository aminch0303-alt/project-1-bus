using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

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
    private Renderer[] _playerBusRenderers;

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

        // 1. Identify which bus is active (Bus 1, Bus 2, or Bus 3)
        int busIndex = GetActiveBusIndex();

        // 2. Stop and freeze real player bus
        if (_activePlayerBus != null)
        {
            _activePlayerBus.overrideInputs = true;
            _activePlayerBus.canControl = false;
            _activePlayerBus.handbrakeInput = 1f;
            _activePlayerBus.brakeInput = 1f;
            _activePlayerBus.throttleInput = 0f;
            _activePlayerBus.speed = 0f;

            _busRigidbody = _activePlayerBus.GetComponent<Rigidbody>();
            if (_busRigidbody != null)
            {
                _busRigidbody.linearVelocity = Vector3.zero;
                _busRigidbody.angularVelocity = Vector3.zero;
                _busRigidbody.isKinematic = true;
            }

            // Hide visual renderers of the real bus
            _playerBusRenderers = _activePlayerBus.GetComponentsInChildren<Renderer>(true);
            SetPlayerBusVisible(false);
        }

        // 3. Disable Driving UI
        SetDrivingUIActive(false);

        // Pause BG music during cutscene if GameManager exists
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PauseBGMusic();
        }

        // 4. Disable RCC Camera while timeline cameras take over
        if (_rccCamera != null)
        {
            _savedCameraState = _rccCamera.enabled;
            _rccCamera.enabled = false;
        }

        // 5. Activate the matching cutscene dummy bus
        SetCutsceneBusesActive(busIndex == 1, busIndex == 2, busIndex == 3);

        // 6. Play the Timeline
        if (playableDirector != null)
        {
            playableDirector.time = 0;
            playableDirector.Play();

            // Wait until timeline completes
            while (playableDirector.state == PlayState.Playing)
            {
                yield return null;
            }
        }
        else
        {
            // Fallback duration if no director assigned
            yield return new WaitForSeconds(5f);
        }

        // 7. Cutscene finished: Hide all cutscene dummy buses
        SetCutsceneBusesActive(false, false, false);

        // 8. Reposition real player bus at bridge exit (if assigned)
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
        if (_playerBusRenderers != null)
        {
            for (int i = 0; i < _playerBusRenderers.Length; i++)
            {
                if (_playerBusRenderers[i] != null)
                {
                    _playerBusRenderers[i].enabled = visible;
                }
            }
        }
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
}
