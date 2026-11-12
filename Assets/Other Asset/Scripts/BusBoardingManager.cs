using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BusBoardingManager : MonoBehaviour
{
    public static BusBoardingManager Instance { get; private set; }

    [Header("Bus References")]
    [SerializeField] private RCC_CarControllerV3 rccController;
    [SerializeField] private Rigidbody busRigidbody;
    [SerializeField] private BusDoorController doorController;
    [SerializeField] private Transform waypointsRoot;
    [SerializeField] private Transform rightSideWaypointsRoot;
    [SerializeField] private Transform sittingPointsRoot;
    [SerializeField] private Transform passengersRoot;

    [Header("Cinematic Camera")]
    [SerializeField] private Transform cinematicCameraPoint;
    [SerializeField] private RCC_Camera rccCamera;
    [SerializeField] private float cameraTransitionDuration = 2.0f;
    [SerializeField] private AnimationCurve cameraTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("UI References")]
    [SerializeField] private Button doorOpenButton;
    [SerializeField] private Button doorCloseButton;
    [SerializeField] private GameObject controllerButtonsRoot;
    [SerializeField] private RCC_UIDashboardDisplay rccDashboardDisplay;
    [SerializeField] private RCC_MobileButtons rccMobileButtons;

    [Header("Settings")]
    [SerializeField] private float passengerStaggerDelay = 1.2f;
    [SerializeField] private float postBoardingWaitTime = 0.5f;
    [SerializeField] private bool hideMarkerVisualsAtStart = true;

    [Header("Editor Path Visualization")]
    [Tooltip("Draw pathline in Scene view (Editor only, not in gameplay).")]
    [SerializeField] private bool showEditorGizmoPath = true;
    [SerializeField] private Color editorPathColor = new Color(0f, 0.85f, 1f, 0.9f);
    [SerializeField] private Color editorSeatConnectionColor = new Color(1f, 0.85f, 0.1f, 0.8f);

    private List<Transform> _allWaypoints = new List<Transform>();
    private List<Transform> _rightSideWaypoints = new List<Transform>();
    private List<Transform> _sittingPoints = new List<Transform>();
    private List<PassengerAgent> _passengers = new List<PassengerAgent>();
    private List<PassengerAgent> _boardedPassengers = new List<PassengerAgent>();

    private RigidbodyConstraints _savedConstraints;
    private Transform _activeCameraTransform;
    private Vector3 _savedCamPos;
    private Quaternion _savedCamRot;

    private bool _boardingStarted;
    private bool _droppingStarted;
    private bool _doorOpenClicked;
    private bool _doorCloseClicked;
    private int _seatedCount;
    private int _droppedCount;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Destroy(gameObject);

        CacheReferences();
    }

    private void Start()
    {
        CacheReferences();

        if (hideMarkerVisualsAtStart)
        {
            HideRenderers(waypointsRoot);
            HideRenderers(rightSideWaypointsRoot);
            HideRenderers(sittingPointsRoot);
        }

        // Initially deactivate passengers and waypoints until trigger is reached
        // if (passengersRoot != null)
        //     passengersRoot.gameObject.SetActive(false);

        if (waypointsRoot != null)
            waypointsRoot.gameObject.SetActive(false);

        // Ensure door buttons are hidden during initial gameplay
        InitDoorButtons();
    }

    private void CacheReferences()
    {
        // Cache Waypoints
        if (waypointsRoot != null)
        {
            _allWaypoints.Clear();
            for (int i = 0; i < waypointsRoot.childCount; i++)
            {
                Transform child = waypointsRoot.GetChild(i);
                if (child.name.Equals("PathLine", System.StringComparison.OrdinalIgnoreCase)) continue;
                _allWaypoints.Add(child);
            }
        }

        // Cache Right Side Approach Waypoints
        if (rightSideWaypointsRoot != null)
        {
            _rightSideWaypoints.Clear();
            for (int i = 0; i < rightSideWaypointsRoot.childCount; i++)
            {
                Transform child = rightSideWaypointsRoot.GetChild(i);
                if (child.name.Equals("PathLine", System.StringComparison.OrdinalIgnoreCase)) continue;
                _rightSideWaypoints.Add(child);
            }
        }

        // Cache Seats
        if (sittingPointsRoot != null)
        {
            _sittingPoints.Clear();
            for (int i = 0; i < sittingPointsRoot.childCount; i++)
            {
                _sittingPoints.Add(sittingPointsRoot.GetChild(i));
            }
        }

        // Cache Passengers
        CachePassengers();

        // Camera transform
        if (rccCamera != null)
            _activeCameraTransform = rccCamera.transform;
        else if (Camera.main != null)
            _activeCameraTransform = Camera.main.transform;
    }

    private void CachePassengers()
    {
        _passengers.Clear();
        if (passengersRoot != null)
        {
            PassengerAgent[] agents = passengersRoot.GetComponentsInChildren<PassengerAgent>(true);
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i] != null && agents[i].gameObject.activeSelf)
                {
                    _passengers.Add(agents[i]);
                }
            }
        }
    }

    private void InitDoorButtons()
    {
        if (doorOpenButton != null)
        {
            doorOpenButton.onClick.RemoveAllListeners();
            doorOpenButton.onClick.AddListener(OnDoorOpenClicked);
            doorOpenButton.gameObject.SetActive(false);
        }

        if (doorCloseButton != null)
        {
            doorCloseButton.onClick.RemoveAllListeners();
            doorCloseButton.onClick.AddListener(OnDoorCloseClicked);
            doorCloseButton.gameObject.SetActive(false);
        }
    }

    // ==========================================
    // 1. PICKUP PASSENGERS (BOARDING)
    // ==========================================
    public void OnBusArrivedAtStop()
    {
        if (_boardingStarted) return;
        _boardingStarted = true;

        InitDoorButtons();
        CachePassengers();

        StartCoroutine(BoardingSequenceRoutine());
    }

    private IEnumerator BoardingSequenceRoutine()
    {
        // 1. Hard Stop Bus
        ApplyHardStop(true);

        // 2. Hide driving UI (controller buttons + dashboard)
        SetDrivingUIActive(false);

        // 3. Smooth Camera Transition to front-left cinematic angle (2 seconds)
        yield return MoveCameraToCinematic();

        // 4. Door Open Button turns ON -> wait for player click
        yield return WaitForDoorOpenClick();

        // 5. Door Open Button turns OFF -> Door opens smoothly (2 seconds)
        float openWait = 2.0f;
        if (doorController != null)
        {
            doorController.OpenDoor();
            openWait = doorController.TotalOpenDuration;
        }
        yield return new WaitForSeconds(openWait);

        // 6. Door open complete: ONLY NOW activate passengers and waypoints!
        // At this moment the door is fully open, so when passengers appear and start walking, it looks 100% intentional and natural!
        if (waypointsRoot != null)
            waypointsRoot.gameObject.SetActive(true);

        if (passengersRoot != null)
        {
            // AlignPassengersNearBus();
            passengersRoot.gameObject.SetActive(true);
            CachePassengers();

            for (int i = 0; i < _passengers.Count; i++)
            {
                if (_passengers[i] != null)
                    _passengers[i].SnapToGround();
            }
        }

        _seatedCount = 0;
        _boardedPassengers.Clear();
        int count = Mathf.Min(_passengers.Count, _sittingPoints.Count);

        for (int i = 0; i < count; i++)
        {
            PassengerAgent passenger = _passengers[i];
            Transform targetSeat = _sittingPoints[i];
            List<Vector3> path = BuildFullBoardingPath(passenger.transform.position, targetSeat);

            _boardedPassengers.Add(passenger);

            float delay = i * passengerStaggerDelay;
            passenger.StartBoarding(path, targetSeat, delay, OnPassengerSeated);
        }

        // Wait for all passengers to sit down
        while (_seatedCount < count)
        {
            yield return null;
        }

        yield return new WaitForSeconds(postBoardingWaitTime);

        // 7. Passengers seated: Door Close Button turns ON -> wait for player click
        yield return WaitForDoorCloseClick();

        // 8. Door Close Button turns OFF -> Door closes smoothly (2 seconds)
        float closeWait = 2.0f;
        if (doorController != null)
        {
            doorController.CloseDoor();
            closeWait = doorController.TotalCloseDuration;
        }
        yield return new WaitForSeconds(closeWait);

        if (waypointsRoot != null)
            waypointsRoot.gameObject.SetActive(false);

        // 9. Camera returns smoothly to gameplay position (2 seconds)
        yield return ReturnCameraToGameplay();

        // 10. Restore driving UI and bus controls
        SetDrivingUIActive(true);
        ApplyHardStop(false);
    }

    private void OnPassengerSeated()
    {
        _seatedCount++;
    }

    // ==========================================
    // 2. DROP PASSENGERS (DISEMBARKING)
    // ==========================================
    public void OnBusArrivedAtDropStop()
    {
        if (_droppingStarted) return;
        _droppingStarted = true;

        InitDoorButtons();

        StartCoroutine(DropPassengersSequenceRoutine());
    }

    private IEnumerator DropPassengersSequenceRoutine()
    {
        // 1. Hard Stop Bus
        ApplyHardStop(true);

        // 2. Hide driving UI (controller buttons + dashboard)
        SetDrivingUIActive(false);

        // 3. Smooth Camera Transition to front-left cinematic angle (2 seconds)
        yield return MoveCameraToCinematic();

        // 4. Door Open Button turns ON -> wait for player click
        yield return WaitForDoorOpenClick();

        // 5. Door Open Button turns OFF -> Door opens smoothly (2 seconds)
        float openWait = 2.0f;
        if (doorController != null)
        {
            doorController.OpenDoor();
            openWait = doorController.TotalOpenDuration;
        }
        yield return new WaitForSeconds(openWait);

        // 6. Door open complete: Activate waypoints and dispatch seated passengers
        if (waypointsRoot != null)
            waypointsRoot.gameObject.SetActive(true);

        List<PassengerAgent> seatedPassengers = new List<PassengerAgent>();
        if (_boardedPassengers.Count > 0)
        {
            seatedPassengers.AddRange(_boardedPassengers);
        }
        else
        {
            for (int i = 0; i < _sittingPoints.Count; i++)
            {
                Transform seat = _sittingPoints[i];
                PassengerAgent agent = seat.GetComponentInChildren<PassengerAgent>();
                if (agent != null)
                {
                    seatedPassengers.Add(agent);
                }
            }
        }

        if (seatedPassengers.Count == 0)
        {
            for (int i = 0; i < _passengers.Count; i++)
            {
                if (_passengers[i] != null && _passengers[i].IsSeated)
                    seatedPassengers.Add(_passengers[i]);
            }
        }

        _droppedCount = 0;
        int totalToDrop = seatedPassengers.Count;

        Vector3 wp0 = _allWaypoints.Count > 0 ? _allWaypoints[0].position : transform.position;
        Vector3 wp1 = _allWaypoints.Count > 1 ? _allWaypoints[1].position : wp0;
        Vector3 outwardDir = (wp0 - wp1).normalized;
        if (outwardDir.sqrMagnitude < 0.001f)
            outwardDir = -transform.forward;

        for (int i = 0; i < seatedPassengers.Count; i++)
        {
            PassengerAgent passenger = seatedPassengers[i];
            Transform currentSeat = passenger.AssignedSeat != null ? passenger.AssignedSeat : passenger.transform.parent;
            List<Transform> exitPath = BuildExitPathForSeat(currentSeat);

            float randomAngle = Random.Range(-65f, 65f);
            Vector3 spreadDir = Quaternion.Euler(0f, randomAngle, 0f) * outwardDir;
            float distance = Random.Range(5.0f, 8.5f);
            Vector3 dispersalTarget = wp0 + spreadDir * distance;

            float delay = i * passengerStaggerDelay;
            passenger.StartAlighting(exitPath, dispersalTarget, delay, OnPassengerReachedEndpoint);
        }

        // 7. Wait until all passengers reach their final endpoints!
        while (_droppedCount < totalToDrop)
        {
            yield return null;
        }

        yield return new WaitForSeconds(postBoardingWaitTime);

        // 8. Passengers at endpoints: Door Close Button turns ON -> wait for player click
        yield return WaitForDoorCloseClick();

        // 9. Door Close Button turns OFF -> Door closes smoothly (2 seconds)
        float closeWait = 2.0f;
        if (doorController != null)
        {
            doorController.CloseDoor();
            closeWait = doorController.TotalCloseDuration;
        }
        yield return new WaitForSeconds(closeWait);

        if (waypointsRoot != null)
            waypointsRoot.gameObject.SetActive(false);

        // 10. In Drop: Camera position does NOT change! Stays at cinematic view.
    }

    private void OnPassengerReachedEndpoint()
    {
        _droppedCount++;
    }

    // ==========================================
    // UI BUTTON WAITERS & HANDLERS
    // ==========================================
    private IEnumerator WaitForDoorOpenClick()
    {
        _doorOpenClicked = false;
        if (doorOpenButton != null)
        {
            doorOpenButton.gameObject.SetActive(true);
            while (!_doorOpenClicked)
                yield return null;

            doorOpenButton.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator WaitForDoorCloseClick()
    {
        _doorCloseClicked = false;
        if (doorCloseButton != null)
        {
            doorCloseButton.gameObject.SetActive(true);
            while (!_doorCloseClicked)
                yield return null;

            doorCloseButton.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void OnDoorOpenClicked()
    {
        _doorOpenClicked = true;
    }

    private void OnDoorCloseClicked()
    {
        _doorCloseClicked = true;
    }

    private void SetDrivingUIActive(bool active)
    {
        if (!active)
        {
            if (rccDashboardDisplay != null)
            {
                rccDashboardDisplay.SetDisplayType(RCC_UIDashboardDisplay.DisplayType.Off);
                rccDashboardDisplay.enabled = false;
            }

            if (rccMobileButtons != null)
            {
                rccMobileButtons.enabled = false;
            }

            if (controllerButtonsRoot != null)
            {
                controllerButtonsRoot.SetActive(false);
            }
        }
        else
        {
            if (controllerButtonsRoot != null)
            {
                controllerButtonsRoot.SetActive(true);
            }

            if (rccMobileButtons != null)
            {
                rccMobileButtons.enabled = true;
            }

            if (rccDashboardDisplay != null)
            {
                rccDashboardDisplay.enabled = true;
                rccDashboardDisplay.SetDisplayType(RCC_UIDashboardDisplay.DisplayType.Full);
            }
        }
    }

    // ==========================================
    // CINEMATIC CAMERA SYSTEM
    // ==========================================
    private IEnumerator MoveCameraToCinematic()
    {
        if (cinematicCameraPoint == null) yield break;

        if (_activeCameraTransform == null)
        {
            if (rccCamera != null)
                _activeCameraTransform = rccCamera.transform;
            else if (Camera.main != null)
                _activeCameraTransform = Camera.main.transform;
        }

        if (_activeCameraTransform != null)
        {
            _savedCamPos = _activeCameraTransform.position;
            _savedCamRot = _activeCameraTransform.rotation;

            if (rccCamera != null)
            {
                rccCamera.enabled = false;
                if (rccCamera.pivot != null)
                {
                    rccCamera.pivot.transform.localPosition = Vector3.zero;
                    rccCamera.pivot.transform.localRotation = Quaternion.identity;
                }
                if (rccCamera.actualCamera != null)
                {
                    rccCamera.actualCamera.transform.localPosition = Vector3.zero;
                    rccCamera.actualCamera.transform.localRotation = Quaternion.identity;
                }
            }

            yield return TransitionCameraRoutine(_savedCamPos, _savedCamRot, cinematicCameraPoint.position, cinematicCameraPoint.rotation, cameraTransitionDuration);
        }
    }

    private IEnumerator ReturnCameraToGameplay()
    {
        if (_activeCameraTransform != null && cinematicCameraPoint != null)
        {
            yield return TransitionCameraRoutine(_activeCameraTransform.position, _activeCameraTransform.rotation, _savedCamPos, _savedCamRot, cameraTransitionDuration);

            if (rccCamera != null)
            {
                rccCamera.enabled = true;
            }
        }
    }

    private IEnumerator TransitionCameraRoutine(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = cameraTransitionCurve != null ? cameraTransitionCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

            if (_activeCameraTransform != null)
            {
                _activeCameraTransform.position = Vector3.Lerp(fromPos, toPos, curvedT);
                _activeCameraTransform.rotation = Quaternion.Slerp(fromRot, toRot, curvedT);
            }

            yield return null;
        }

        if (_activeCameraTransform != null)
        {
            _activeCameraTransform.position = toPos;
            _activeCameraTransform.rotation = toRot;
        }
    }

    // ==========================================
    // HELPERS & PATH CALCULATIONS
    // ==========================================
    private void ApplyHardStop(bool enableStop)
    {
        if (enableStop)
        {
            if (rccController != null)
            {
                rccController.canControl = false;
                rccController.handbrakeInput = 1f;
                rccController.brakeInput = 1f;
                rccController.throttleInput = 0f;
                rccController.speed = 0f;
                rccController.enabled = false;
            }

            if (busRigidbody != null)
            {
                busRigidbody.linearVelocity = Vector3.zero;
                busRigidbody.angularVelocity = Vector3.zero;
                _savedConstraints = busRigidbody.constraints;
                busRigidbody.constraints = RigidbodyConstraints.FreezeAll;
                busRigidbody.isKinematic = true;
            }
        }
        else
        {
            if (busRigidbody != null)
            {
                busRigidbody.isKinematic = false;
                busRigidbody.constraints = _savedConstraints;
                busRigidbody.linearVelocity = Vector3.zero;
                busRigidbody.angularVelocity = Vector3.zero;
            }

            if (rccController != null)
            {
                rccController.enabled = true;
                rccController.canControl = true;
                rccController.handbrakeInput = 0f;
                rccController.brakeInput = 0f;
            }
        }
    }

    private void AlignPassengersNearBus()
    {
        if (passengersRoot == null) return;

        // Calculate bus dimensions
        float halfLength = 8.0f;
        float halfWidth = 2.0f;
        BoxCollider busBox = GetComponent<BoxCollider>();
        if (busBox != null)
        {
            halfLength = Mathf.Max(halfLength, busBox.size.z * 0.5f * transform.lossyScale.z);
            halfWidth = Mathf.Max(halfWidth, busBox.size.x * 0.5f * transform.lossyScale.x);
        }

        Vector3 busRight = transform.right;
        Vector3 busForward = transform.forward;
        Vector3 busPos = transform.position;

        // Determine which side the entrance door is on
        float doorSideSign = -1f; // default left
        if (_allWaypoints.Count > 0)
        {
            float dotDoor = Vector3.Dot(_allWaypoints[0].position - busPos, busRight);
            doorSideSign = dotDoor >= 0f ? 1f : -1f;
        }

        // Place passengers on the RIGHT side of the bus (opposite of door), but far enough to be off-screen
        float oppositeSideSign = -doorSideSign;
        float sideDistance = oppositeSideSign * (halfWidth + 12.0f); // 12 meters to the right: totally out of cinematic camera view frustum!
        float forwardPos = halfLength - 1.0f; // Aligned near the front side

        Vector3 targetRootPos = busPos + (busRight * sideDistance) + (busForward * forwardPos);
        targetRootPos.y = passengersRoot.position.y;

        passengersRoot.position = targetRootPos;
    }

    private List<Vector3> BuildFullBoardingPath(Vector3 passengerPos, Transform seat)
    {
        List<Vector3> path = new List<Vector3>();

        if (_allWaypoints.Count > 0)
        {
            Vector3 doorWp = _allWaypoints[0].position;

            // Check passenger side relative to bus forward & right
            Vector3 busForward = transform.forward;
            Vector3 busRight = transform.right;
            Vector3 toPassenger = passengerPos - transform.position;
            Vector3 toDoor = doorWp - transform.position;

            float passengerSide = Vector3.Dot(toPassenger, busRight);
            float doorSide = Vector3.Dot(toDoor, busRight);

            // If passenger is on the opposite side of the door (e.g. right side when door is on left)
            bool isOppositeSide = (passengerSide * doorSide < 0f && Mathf.Abs(passengerSide) > 1.0f);

            if (isOppositeSide)
            {
                // Visual waypoints set in Inspector (Cubes on right side / front bumper)
                if (_rightSideWaypoints != null && _rightSideWaypoints.Count > 0)
                {
                    for (int w = 0; w < _rightSideWaypoints.Count; w++)
                    {
                        if (_rightSideWaypoints[w] != null)
                            path.Add(_rightSideWaypoints[w].position);
                    }
                }
                else if (rightSideWaypointsRoot != null && rightSideWaypointsRoot.childCount > 0)
                {
                    for (int w = 0; w < rightSideWaypointsRoot.childCount; w++)
                    {
                        Transform child = rightSideWaypointsRoot.GetChild(w);
                        if (child != null && !child.name.Equals("PathLine", System.StringComparison.OrdinalIgnoreCase))
                            path.Add(child.position);
                    }
                }
                /*
                // Previous math-based calculation (commented out):
                // Calculate front bumper avoidance points
                float halfLength = 8.0f;
                float halfWidth = 2.0f;

                BoxCollider busBox = GetComponent<BoxCollider>();
                if (busBox != null)
                {
                    halfLength = Mathf.Max(halfLength, busBox.size.z * 0.5f * transform.lossyScale.z);
                    halfWidth = Mathf.Max(halfWidth, busBox.size.x * 0.5f * transform.lossyScale.x);
                }

                // Tight front clearance: only 0.8m ahead of bumper
                float frontZ = halfLength + 0.8f;
                float sideSign = Mathf.Sign(passengerSide);
                float doorSign = Mathf.Sign(doorSide);

                // Waypoint 1: Front corner on passenger's side (close to body)
                Vector3 currentSideFront = transform.position + busForward * (halfLength - 0.5f) + busRight * (sideSign * (halfWidth + 0.5f));
                path.Add(currentSideFront);

                // Waypoint 2: Smooth curve just around the front bumper
                Vector3 curvePointCurrent = transform.position + busForward * frontZ + busRight * (sideSign * (halfWidth * 0.5f));
                path.Add(curvePointCurrent);

                // Waypoint 3: Directly ahead of front bumper
                Vector3 directlyInFront = transform.position + busForward * frontZ;
                path.Add(directlyInFront);

                // Waypoint 4: Smooth curve towards door side
                Vector3 curvePointDoor = transform.position + busForward * frontZ + busRight * (doorSign * (halfWidth * 0.5f));
                path.Add(curvePointDoor);

                // Waypoint 5: Door side front corner
                Vector3 doorSideFront = transform.position + busForward * (halfLength - 0.5f) + busRight * (doorSign * (halfWidth + 0.5f));
                path.Add(doorSideFront);

                // Waypoint 6: Approach directly outside door
                Vector3 approachDoor = doorWp + busRight * (doorSign * 0.6f);
                path.Add(approachDoor);
                */
            }
        }

        // Add standard entrance and aisle waypoints
        List<Transform> seatWaypoints = BuildPathForSeat(seat);
        for (int i = 0; i < seatWaypoints.Count; i++)
        {
            if (seatWaypoints[i] != null)
                path.Add(seatWaypoints[i].position);
        }

        return path;
    }

    private List<Transform> BuildPathForSeat(Transform seat)
    {
        List<Transform> path = new List<Transform>();
        if (_allWaypoints.Count == 0) return path;

        int entranceCount = Mathf.Min(5, _allWaypoints.Count);
        for (int i = 0; i < entranceCount; i++)
        {
            path.Add(_allWaypoints[i]);
        }

        if (_allWaypoints.Count > 5 && seat != null)
        {
            Transform wp5 = _allWaypoints[5];
            Transform wpEnd = _allWaypoints[_allWaypoints.Count - 1];
            Vector3 aisleDir = (wpEnd.position - wp5.position).normalized;

            if (aisleDir.sqrMagnitude > 0.001f)
            {
                float seatDist = Vector3.Dot(seat.position - wp5.position, aisleDir);

                for (int i = 5; i < _allWaypoints.Count; i++)
                {
                    Transform aisleWp = _allWaypoints[i];
                    float wpDist = Vector3.Dot(aisleWp.position - wp5.position, aisleDir);

                    if (wpDist <= seatDist + 0.1f)
                    {
                        path.Add(aisleWp);
                    }
                }
            }
        }

        return path;
    }

    private List<Transform> BuildExitPathForSeat(Transform seat)
    {
        List<Transform> entryPath = BuildPathForSeat(seat);
        List<Transform> exitPath = new List<Transform>();

        for (int i = entryPath.Count - 1; i >= 0; i--)
        {
            exitPath.Add(entryPath[i]);
        }

        return exitPath;
    }

    private void HideRenderers(Transform parent)
    {
        if (parent == null) return;
        MeshRenderer[] renderers = parent.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in renderers)
        {
            r.enabled = false;
        }

        Collider[] colliders = parent.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            c.enabled = false;
        }
    }

#if UNITY_EDITOR
    // ==========================================
    // EDITOR-ONLY SCENE VIEW PATH VISUALIZATION
    // Only runs when selected in Hierarchy
    // ==========================================
    private void OnDrawGizmosSelected()
    {
        if (!showEditorGizmoPath || waypointsRoot == null) return;

        // Draw Right Side Approach Waypoints in Scene View
        if (rightSideWaypointsRoot != null && rightSideWaypointsRoot.childCount > 0)
        {
            Gizmos.color = editorPathColor;
            for (int i = 0; i < rightSideWaypointsRoot.childCount - 1; i++)
            {
                Transform a = rightSideWaypointsRoot.GetChild(i);
                Transform b = rightSideWaypointsRoot.GetChild(i + 1);
                if (a != null && b != null)
                {
                    Gizmos.DrawLine(a.position, b.position);
                    Gizmos.DrawSphere(a.position, 0.08f);
                }
            }

            Transform lastRight = rightSideWaypointsRoot.GetChild(rightSideWaypointsRoot.childCount - 1);
            if (lastRight != null)
            {
                Gizmos.DrawSphere(lastRight.position, 0.08f);
                if (waypointsRoot.childCount > 0)
                {
                    Transform firstDoor = waypointsRoot.GetChild(0);
                    if (firstDoor != null)
                        Gizmos.DrawLine(lastRight.position, firstDoor.position);
                }
            }
        }

        Gizmos.color = editorPathColor;
        for (int i = 0; i < waypointsRoot.childCount - 1; i++)
        {
            Transform a = waypointsRoot.GetChild(i);
            Transform b = waypointsRoot.GetChild(i + 1);
            if (a != null && b != null)
            {
                Gizmos.DrawLine(a.position, b.position);
                Gizmos.DrawSphere(a.position, 0.08f);
            }
        }

        if (waypointsRoot.childCount > 0)
        {
            Transform last = waypointsRoot.GetChild(waypointsRoot.childCount - 1);
            if (last != null)
                Gizmos.DrawSphere(last.position, 0.08f);
        }

        if (sittingPointsRoot != null)
        {
            Gizmos.color = editorSeatConnectionColor;
            for (int i = 0; i < sittingPointsRoot.childCount; i++)
            {
                Transform seat = sittingPointsRoot.GetChild(i);
                if (seat != null)
                {
                    Gizmos.DrawWireCube(seat.position, new Vector3(0.25f, 0.25f, 0.25f));

                    Transform nearestWp = null;
                    float nearestDist = float.MaxValue;
                    for (int w = 4; w < waypointsRoot.childCount; w++)
                    {
                        Transform wp = waypointsRoot.GetChild(w);
                        float dist = Vector3.Distance(seat.position, wp.position);
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            nearestWp = wp;
                        }
                    }

                    if (nearestWp != null)
                    {
                        Gizmos.DrawLine(nearestWp.position, seat.position);
                    }
                }
            }
        }
    }
#endif
}
