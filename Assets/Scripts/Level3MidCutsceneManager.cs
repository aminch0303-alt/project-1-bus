using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;

/// <summary>
/// Level 3 Middle Animation Cutscene Manager:
/// - Attached to Level 3's 'Middle anim trigger'.
/// - Preserves Music state: Only pauses and resumes if music was ON before trigger. If paused by user, stays paused!
/// - Smooth natural bus braking & deceleration (Bridge Style).
/// - Smooth Fade Out to black before cutscene plays.
/// - Switches camera cleanly to Timeline.
/// - Once Timeline finishes:
///     1. First, RCC Camera turns ON and screen smoothly Fades In to reveal gameplay.
///     2. Waits 1 second delay (as requested).
///     3. Then Driving UI turns ON and controls are restored.
///     4. Music resumes seamlessly.
/// </summary>
public class Level3MidCutsceneManager : MonoBehaviour
{
    [Header("Timeline Reference")]
    [Tooltip("Hierarchy se 'Middle animation timeline' GameObject drag karein (jis par PlayableDirector laga hai)")]
    [SerializeField] private PlayableDirector playableDirector;

    [Header("Optional Resume Exit Point")]
    [Tooltip("Optional: Cutscene khatam hone par bus ko kisi specific point par set karna ho to yahan Transform drag karein, warna bus wahi se continue karegi jahan ruki thi.")]
    [SerializeField] private Transform resumeExitPoint;

    [Header("--- Screen Fade Reference ---")]
    [Tooltip("Hierarchy se ScreenFadeCanvas drag karein")]
    [SerializeField] private ScreenFadeManager screenFadeManager;

    [Header("Transition Settings")]
    [Tooltip("Trigger lagne par bus kitne seconds mein naturally glide ho kar rukay")]
    [SerializeField] private float busStopDuration = 1.0f;

    [Tooltip("Black screen fade out duration")]
    [SerializeField] private float fadeOutDuration = 0.45f;

    [Tooltip("Black screen fade in duration")]
    [SerializeField] private float fadeInDuration = 0.45f;

    [Tooltip("Animation khatam hone ke baad RCC Camera on hone ke kitne sec baad UI on ho (Default: 1.0s)")]
    [SerializeField] private float uiRestoreDelay = 1.0f;

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
    private bool _wasMusicPlayingBeforeCutscene = false;

    // Fullscreen Fade Overlay

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

        // Ensure Middle Animation Timeline is initially OFF so gameplay camera runs without conflict
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
            // Auto-detect if user hasn't dragged it in Inspector
            GameObject timelineGo = GameObject.Find("Middle animation timeline");
            if (timelineGo != null)
            {
                playableDirector = timelineGo.GetComponent<PlayableDirector>();
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
            StartCoroutine(RunLevel3CutsceneRoutine());
        }
    }

    private IEnumerator RunLevel3CutsceneRoutine()
    {
        CacheSceneReferences();

        // 1. MUSIC CHECK: Agar music ON hai to sirf temporary pause karein. Agar pehle se Pause tha to state yaad rakhein!
        _wasMusicPlayingBeforeCutscene = (GameManager.Instance != null && GameManager.Instance.IsMusicPlaying);
        if (_wasMusicPlayingBeforeCutscene)
        {
            GameManager.Instance.PauseBGMusicForCutscene();
        }

        // 2. Hide driving UI buttons & dashboard
        SetDrivingUIActive(false);

        // 3. Smoothly apply brakes & slow bus down naturally (Bridge Style)
        if (_activePlayerBus != null)
        {
            _activePlayerBus.overrideInputs = true;
            _activePlayerBus.canControl = false;
            _activePlayerBus.throttleInput = 0f;
            _activePlayerBus.brakeInput = 1f;
            _activePlayerBus.handbrakeInput = 1f;

            _busRigidbody = _activePlayerBus.GetComponent<Rigidbody>();
        }

        // Wait for the bus to naturally glide and come to a smooth stop
        yield return new WaitForSeconds(busStopDuration);

        // Freeze bus physics completely once stopped
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

        // 4. Smooth Fade Out to black before cutscene
        yield return StartCoroutine(FadeRoutine(0f, 1f, fadeOutDuration));

        // 5. Disable RCC Camera so timeline cameras take over cleanly
        if (_rccCamera != null)
        {
            _savedCameraState = _rccCamera.enabled;
            _rccCamera.enabled = false;
        }

        // 6. Activate & Play Timeline
        if (playableDirector != null)
        {
            playableDirector.gameObject.SetActive(true);
            playableDirector.time = 0;
            playableDirector.Play();
        }

        // Fade in from black to reveal timeline smoothly
        yield return StartCoroutine(FadeRoutine(1f, 0f, fadeInDuration));

        // Wait until timeline completes
        if (playableDirector != null)
        {
            while (playableDirector != null && playableDirector.state == PlayState.Playing)
            {
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(4f);
        }

        // 7. Timeline finished: Fade out to black for smooth transition back to gameplay
        yield return StartCoroutine(FadeRoutine(0f, 1f, fadeOutDuration));

        if (playableDirector != null)
        {
            playableDirector.gameObject.SetActive(false);
        }

        // Reposition / unfreeze bus while screen is black
        if (_activePlayerBus != null)
        {
            if (resumeExitPoint != null)
            {
                _activePlayerBus.transform.position = resumeExitPoint.position;
                _activePlayerBus.transform.rotation = resumeExitPoint.rotation;
            }

            if (_busRigidbody != null)
            {
                _busRigidbody.isKinematic = false;
                _busRigidbody.linearVelocity = Vector3.zero;
                _busRigidbody.angularVelocity = Vector3.zero;
            }

            // Re-enable player controls & release brakes
            _activePlayerBus.overrideInputs = false;
            _activePlayerBus.canControl = true;
            _activePlayerBus.handbrakeInput = 0f;
            _activePlayerBus.brakeInput = 0f;
            _activePlayerBus.throttleInput = 0f;

            // Re-register with RCC to guarantee clean driving response
            RCC.RegisterPlayerVehicle(_activePlayerBus, true, true);
        }

        // 8. PEHLE RCC CAMERA ON HO! (As requested)
        if (_rccCamera != null)
        {
            _rccCamera.enabled = _savedCameraState;
        }

        // Fade in to reveal normal gameplay camera
        yield return StartCoroutine(FadeRoutine(1f, 0f, fadeInDuration));

        // 9. AIK SECOND KA WAIT (As requested: "aik sec bad ui on ho")
        yield return new WaitForSeconds(uiRestoreDelay);

        // 10. Ab Driving UI ON ho!
        SetDrivingUIActive(true);

        // 11. MUSIC RESUME: Sirf tab resume hoga agar cutscene se pehle play ho raha tha! Agar user ne pause kiya hua tha to off hi rahega.
        if (_wasMusicPlayingBeforeCutscene && GameManager.Instance != null)
        {
            GameManager.Instance.ResumeBGMusic();
        }

        // 12. Destroy trigger so it never fires again
        Destroy(gameObject);
    }

    #region Fullscreen Fade Helpers

    private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration)
    {
        ScreenFadeManager fade = screenFadeManager != null ? screenFadeManager : ScreenFadeManager.Instance;
        if (fade != null)
        {
            if (toAlpha > fromAlpha)
            {
                yield return fade.FadeOutRoutine(duration);
            }
            else
            {
                yield return fade.FadeInRoutine(duration);
            }
        }
    }

    #endregion

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

    /// <summary>
    /// Level 3 Middle Animation ko foran skip karne ke liye (UI Skip Button OnClick)
    /// </summary>
    public void SkipCutscene()
    {
        if (!_hasTriggered) return;

        // 1. Running coroutines band karein
        StopAllCoroutines();

        // 2. Timeline director stop karein aur timeline GameObject OFF karein
        if (playableDirector != null)
        {
            playableDirector.Stop();
            playableDirector.gameObject.SetActive(false);
        }

        // 3. Screen fade reset karein
        ScreenFadeManager fade = screenFadeManager != null ? screenFadeManager : ScreenFadeManager.Instance;
        if (fade != null)
        {
            fade.SetAlphaImmediate(0f);
        }

        // 4. Player bus ko resume point par set karein aur physics unfreeze karein
        if (_activePlayerBus != null)
        {
            if (resumeExitPoint != null)
            {
                _activePlayerBus.transform.position = resumeExitPoint.position;
                _activePlayerBus.transform.rotation = resumeExitPoint.rotation;
            }

            if (_busRigidbody != null)
            {
                _busRigidbody.isKinematic = false;
                _busRigidbody.linearVelocity = Vector3.zero;
                _busRigidbody.angularVelocity = Vector3.zero;
            }

            // Controls & engine release karein
            _activePlayerBus.overrideInputs = false;
            _activePlayerBus.canControl = true;
            _activePlayerBus.handbrakeInput = 0f;
            _activePlayerBus.brakeInput = 0f;
            _activePlayerBus.throttleInput = 0f;
            _activePlayerBus.StartEngine();

            // Re-register with RCC
            RCC.RegisterPlayerVehicle(_activePlayerBus, true, true);
        }

        // 5. RCC Camera restore karein
        if (_rccCamera != null)
        {
            _rccCamera.enabled = _savedCameraState;
        }

        // 6. Driving UI restore karein
        SetDrivingUIActive(true);

        // 7. Background music resume karein agar pehle chal rahi thi
        if (_wasMusicPlayingBeforeCutscene && GameManager.Instance != null)
        {
            GameManager.Instance.ResumeBGMusic();
        }

        // 8. Trigger destroy karein
        Destroy(gameObject);
    }
}
