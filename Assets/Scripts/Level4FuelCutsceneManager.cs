using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Level 4 Fuel Cutscene Manager:
/// - Attached to the Fuel Station trigger GameObject.
/// - Smooth natural bus braking & deceleration.
/// - Aligns bus to 'Bus Parking Target' at the petrol pump.
/// - Switches off RCC Camera and plays Timeline.
/// - Preserves & resumes background music.
/// - Restores controls & camera when Timeline completes.
/// </summary>
public class Level4FuelCutsceneManager : MonoBehaviour
{
    [Header("Timeline Reference")]
    [Tooltip("Hierarchy se 'Timeline' GameObject drag karein (jis par PlayableDirector laga hai)")]
    [SerializeField] private PlayableDirector playableDirector;

    [Header("Bus Parking Alignment")]
    [Tooltip("Petrol pump ke samne exact spot jahan bus khari ho gi (bus Fuel stop pos)")]
    [SerializeField] private Transform busParkingTarget;

    [Tooltip("Optional: Cutscene ke baad bus kahan se continue kare (khali chhor dein to wahi se continue karegi)")]
    [SerializeField] private Transform resumeExitPoint;

    [Header("Transition Settings")]
    [Tooltip("Trigger lagne par bus kitne seconds mein naturally glide ho kar rukay")]
    [SerializeField] private float busStopDuration = 1.0f;

    [Tooltip("Cutscene khatam hone ke baad RCC Camera on hone ke kitne sec baad UI on ho (Default: 1.0s)")]
    [SerializeField] private float uiRestoreDelay = 1.0f;

    [Header("Optional Road Objectives")]
    [Tooltip("Cutscene complete hone par agla checkpoint / drop trigger activate karein")]
    [SerializeField] private GameObject nextObjectiveToActivate;

    [Tooltip("Current fuel station ka indicator / road arrow de-activate karein")]
    [SerializeField] private GameObject currentObjectiveToDeactivate;

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

        // Timeline initially OFF
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
            playableDirector = GetComponentInChildren<PlayableDirector>();
            if (playableDirector == null)
            {
                GameObject tlObj = GameObject.Find("Timeline");
                if (tlObj != null) playableDirector = tlObj.GetComponent<PlayableDirector>();
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
            StartCoroutine(RunLevel4CutsceneRoutine());
        }
    }

    private IEnumerator RunLevel4CutsceneRoutine()
    {
        CacheSceneReferences();

        // 1. Music check & pause
        _wasMusicPlayingBeforeCutscene = (GameManager.Instance != null && GameManager.Instance.IsMusicPlaying);
        if (_wasMusicPlayingBeforeCutscene)
        {
            GameManager.Instance.PauseBGMusicForCutscene();
        }

        // 2. Hide driving UI buttons
        SetDrivingUIActive(false);

        // 3. Smooth natural bus deceleration
        if (_activePlayerBus != null)
        {
            _activePlayerBus.overrideInputs = true;
            _activePlayerBus.canControl = false;
            _activePlayerBus.throttleInput = 0f;
            _activePlayerBus.brakeInput = 1f;
            _activePlayerBus.handbrakeInput = 1f;

            _busRigidbody = _activePlayerBus.GetComponent<Rigidbody>();
        }

        yield return new WaitForSeconds(busStopDuration);

        // Freeze bus physics completely
        if (_activePlayerBus != null)
        {
            _activePlayerBus.speed = 0f;

            // Snap/Align bus to parking spot at pump
            Transform targetPos = (busParkingTarget != null) ? busParkingTarget : resumeExitPoint;
            if (targetPos != null)
            {
                if (_busRigidbody != null)
                {
                    _busRigidbody.position = targetPos.position;
                    _busRigidbody.rotation = targetPos.rotation;
                }
                _activePlayerBus.transform.position = targetPos.position;
                _activePlayerBus.transform.rotation = targetPos.rotation;
                Physics.SyncTransforms();
            }

            if (_busRigidbody != null)
            {
                _busRigidbody.linearVelocity = Vector3.zero;
                _busRigidbody.angularVelocity = Vector3.zero;
                _busRigidbody.isKinematic = true;
            }
        }

        // 4. Disable RCC Camera
        if (_rccCamera != null)
        {
            _savedCameraState = _rccCamera.enabled;
            _rccCamera.enabled = false;
        }

        // 5. Activate & Play Timeline (Audio, animations & cameras are inside Timeline!)
        if (playableDirector != null)
        {
            playableDirector.gameObject.SetActive(true);
            playableDirector.time = 0;
            playableDirector.Play();

            // Wait until timeline completes
            while (playableDirector != null && playableDirector.state == PlayState.Playing)
            {
                yield return null;
            }

            playableDirector.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(5.0f);
        }

        // 6. Reposition bus if resume exit point is assigned
        if (_activePlayerBus != null)
        {
            Transform exitPoint = (resumeExitPoint != null) ? resumeExitPoint : busParkingTarget;

            if (exitPoint != null)
            {
                if (_busRigidbody != null)
                {
                    _busRigidbody.isKinematic = false;
                    _busRigidbody.linearVelocity = Vector3.zero;
                    _busRigidbody.angularVelocity = Vector3.zero;
                    _busRigidbody.position = exitPoint.position;
                    _busRigidbody.rotation = exitPoint.rotation;
                }

                _activePlayerBus.transform.position = exitPoint.position;
                _activePlayerBus.transform.rotation = exitPoint.rotation;

                // Sync Unity PhysX transforms immediately so physics acknowledges the new position
                Physics.SyncTransforms();

                if (_busRigidbody != null)
                {
                    _busRigidbody.linearVelocity = Vector3.zero;
                    _busRigidbody.angularVelocity = Vector3.zero;
                }
            }
            else if (_busRigidbody != null)
            {
                _busRigidbody.isKinematic = false;
                _busRigidbody.linearVelocity = Vector3.zero;
                _busRigidbody.angularVelocity = Vector3.zero;
            }

            _activePlayerBus.brakeInput = 0f;
            _activePlayerBus.handbrakeInput = 0f;
            _activePlayerBus.throttleInput = 0f;
            _activePlayerBus.speed = 0f;

            // Clear wheel colliders torque / velocity
            if (_activePlayerBus.AllWheelColliders != null)
            {
                for (int i = 0; i < _activePlayerBus.AllWheelColliders.Length; i++)
                {
                    var wc = _activePlayerBus.AllWheelColliders[i];
                    if (wc != null && wc.WheelCollider != null)
                    {
                        wc.WheelCollider.motorTorque = 0f;
                        wc.WheelCollider.brakeTorque = 0f;
                    }
                }
            }
        }

        // 7. Restore RCC Camera
        if (_rccCamera != null)
        {
            if (_activePlayerBus != null && resumeExitPoint != null)
            {
                // Align camera behind bus to prevent camera snapping/rubber-banding
                _rccCamera.transform.position = _activePlayerBus.transform.position - (_activePlayerBus.transform.forward * 8f) + (Vector3.up * 3f);
                _rccCamera.transform.rotation = _activePlayerBus.transform.rotation;
            }

            _rccCamera.enabled = _savedCameraState;
        }

        // 8. Wait delay before enabling UI buttons
        if (uiRestoreDelay > 0f)
        {
            yield return new WaitForSeconds(uiRestoreDelay);
        }

        // 9. Restore controls
        if (_activePlayerBus != null)
        {
            _activePlayerBus.canControl = true;
            _activePlayerBus.overrideInputs = false;
        }

        SetDrivingUIActive(true);

        // 10. Resume Music
        if (_wasMusicPlayingBeforeCutscene && GameManager.Instance != null)
        {
            GameManager.Instance.ResumeBGMusic();
        }

        // 11. Objective progression
        if (currentObjectiveToDeactivate != null)
        {
            currentObjectiveToDeactivate.SetActive(false);
        }
        if (nextObjectiveToActivate != null)
        {
            nextObjectiveToActivate.SetActive(true);
        }
    }

    private void SetDrivingUIActive(bool active)
    {
        if (_controllerButtonsRoot != null)
        {
            _controllerButtonsRoot.SetActive(active);
        }
        else if (_rccCanvas != null)
        {
            _rccCanvas.SetActive(active);
        }
    }
}
