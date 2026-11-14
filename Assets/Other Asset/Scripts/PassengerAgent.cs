using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PassengerAgent : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 1.6f;
    [SerializeField] private float turnSpeed = 8.0f;
    [SerializeField] private float arrivalThreshold = 0.15f;
    [SerializeField] private Vector3 sitOffset = Vector3.zero;

    [Header("Ground Snapping")]
    [Tooltip("Layer mask used to detect road/terrain surface.")]
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform originalParent;
    [SerializeField] private CapsuleCollider agentCollider;

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsSittingHash = Animator.StringToHash("IsSitting");
    private static readonly RaycastHit[] _raycastBuffer = new RaycastHit[16];

    public bool IsSeated { get; private set; }
    public Transform AssignedSeat { get; private set; }

    private void Awake()
    {
        if (originalParent == null && transform.parent != null)
            originalParent = transform.parent;

        InitAnimator();
        SetupCollider();
        SnapToGround();
    }

    private void OnEnable()
    {
        InitAnimator();
        SetupCollider();
        SnapToGround();
    }

    private void SetupCollider()
    {
        if (agentCollider == null)
            agentCollider = GetComponent<CapsuleCollider>();

        if (agentCollider == null)
        {
            agentCollider = gameObject.AddComponent<CapsuleCollider>();
            agentCollider.height = 1.75f;
            agentCollider.radius = 0.25f;
            agentCollider.center = new Vector3(0f, 0.88f, 0f);
        }
    }

    private void InitAnimator()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    public void SnapToGround()
    {
        float surfaceY = GetGroundSurfaceHeight(transform.position);
        if (surfaceY > float.MinValue)
        {
            Vector3 pos = transform.position;
            pos.y = surfaceY;
            transform.position = pos;
        }
    }

    private float GetGroundSurfaceHeight(Vector3 samplePos)
    {
        Vector3 origin = samplePos + Vector3.up * 5.0f;
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, _raycastBuffer, 20.0f, groundLayer, QueryTriggerInteraction.Ignore);

        float highestY = float.MinValue;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _raycastBuffer[i];

            // Ignore vehicle (RCC layer) and self/child objects
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("RCC")) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            if (hit.point.y > highestY)
            {
                highestY = hit.point.y;
                found = true;
            }
        }

        return found ? highestY : float.MinValue;
    }

    public void StartBoarding(List<Vector3> pathWaypoints, Transform targetSeat, float startDelay, Action onSeatedCallback, Action onDoorEnteredCallback = null)
    {
        StartCoroutine(BoardingRoutine(pathWaypoints, targetSeat, startDelay, onSeatedCallback, onDoorEnteredCallback));
    }

    private IEnumerator BoardingRoutine(List<Vector3> pathWaypoints, Transform targetSeat, float startDelay, Action onSeatedCallback, Action onDoorEnteredCallback = null)
    {
        AssignedSeat = targetSeat;

        // Snap to ground before waiting / walking
        SnapToGround();

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        PlayWalkingAnimation();

        bool doorTriggered = false;

        // Follow path points (outside detour, front detour, door entry, aisle)
        for (int i = 0; i < pathWaypoints.Count; i++)
        {
            Vector3 wp = pathWaypoints[i];
            // Snap ground for outside points, but maintain precise step heights inside bus
            bool isOutside = (i < pathWaypoints.Count - 5);

            // Door threshold: when reaching the first inside/door step
            if (!isOutside && !doorTriggered)
            {
                doorTriggered = true;
                onDoorEnteredCallback?.Invoke();
            }

            yield return MoveTowardsPoint(wp, isOutside);
        }

        // Move to final seat position
        if (targetSeat != null)
        {
            yield return MoveTowardsPoint(targetSeat.position + sitOffset, false);

            // Rotate towards seat orientation
            yield return RotateTowards(targetSeat.rotation, 0.4f);

            // Reparent to seat/bus so passenger moves with the bus
            transform.SetParent(targetSeat);
            transform.localPosition = sitOffset;
            transform.localRotation = Quaternion.identity;
        }

        PlaySittingAnimation();
        IsSeated = true;

        if (agentCollider != null)
        {
            agentCollider.enabled = false; // Disable collider once seated to avoid car physics collision
        }

        onSeatedCallback?.Invoke();
    }

    public void StartAlighting(List<Transform> exitWaypoints, Vector3 dispersalTarget, float startDelay, Action onEndpointReachedCallback, List<Vector3> outsideDetourWaypoints = null)
    {
        StartCoroutine(AlightingRoutine(exitWaypoints, dispersalTarget, startDelay, onEndpointReachedCallback, outsideDetourWaypoints));
    }

    private IEnumerator AlightingRoutine(List<Transform> exitWaypoints, Vector3 dispersalTarget, float startDelay, Action onEndpointReachedCallback, List<Vector3> outsideDetourWaypoints = null)
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        // Stand up from seat and detach from bus, reparenting to original container
        transform.SetParent(originalParent);
        IsSeated = false;

        if (agentCollider != null)
        {
            agentCollider.enabled = true;
        }

        PlayWalkingAnimation();

        // Follow aisle out through doorway down all steps
        for (int i = 0; i < exitWaypoints.Count; i++)
        {
            Transform wp = exitWaypoints[i];
            if (wp == null) continue;

            // Follow waypoints smoothly down each step
            yield return MoveTowardsPoint(wp.position, false);
        }

        // If dropping on right side / using detour points around front bumper
        if (outsideDetourWaypoints != null && outsideDetourWaypoints.Count > 0)
        {
            for (int i = 0; i < outsideDetourWaypoints.Count; i++)
            {
                yield return MoveTowardsPoint(outsideDetourWaypoints[i], true);
            }
        }

        // Disperse away from the bus in random direction to endpoint
        yield return MoveTowardsPoint(dispersalTarget, true);

        // Stand idle after moving away
        PlayStandingAnimation();

        // Passenger has reached their final endpoint
        onEndpointReachedCallback?.Invoke();
    }

    private IEnumerator MoveTowardsPoint(Vector3 targetPos, bool snapGround)
    {
        if (snapGround)
        {
            float targetGroundY = GetGroundSurfaceHeight(targetPos);
            if (targetGroundY > float.MinValue)
            {
                targetPos.y = targetGroundY;
            }
        }

        float groundCheckTimer = 0f;

        while (true)
        {
            float dist = snapGround
                ? Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(targetPos.x, targetPos.z))
                : Vector3.Distance(transform.position, targetPos);

            if (dist <= arrivalThreshold)
                break;

            Vector3 direction = (targetPos - transform.position);
            Vector3 flatDir = new Vector3(direction.x, 0f, direction.z);

            if (flatDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(flatDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }

            Vector3 nextPos = Vector3.MoveTowards(transform.position, targetPos, walkSpeed * Time.deltaTime);

            if (snapGround)
            {
                groundCheckTimer += Time.deltaTime;
                if (groundCheckTimer >= 0.1f)
                {
                    groundCheckTimer = 0f;
                    float groundY = GetGroundSurfaceHeight(nextPos);
                    if (groundY > float.MinValue)
                    {
                        targetPos.y = groundY;
                    }
                }
            }

            transform.position = nextPos;
            yield return null;
        }
    }

    private IEnumerator RotateTowards(Quaternion targetRotation, float duration)
    {
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(startRot, targetRotation, elapsed / duration);
            yield return null;
        }

        transform.rotation = targetRotation;
    }

    private void PlayWalkingAnimation()
    {
        if (animator != null)
        {
            animator.SetBool(IsWalkingHash, true);
            animator.SetBool(IsSittingHash, false);
            animator.CrossFade("Walking", 0.15f);
        }
    }

    private void PlaySittingAnimation()
    {
        if (animator != null)
        {
            animator.SetBool(IsWalkingHash, false);
            animator.SetBool(IsSittingHash, true);
            animator.CrossFade("SittingIdle", 0.25f);
        }
    }

    private void PlayStandingAnimation()
    {
        if (animator != null)
        {
            animator.SetBool(IsWalkingHash, false);
            animator.SetBool(IsSittingHash, false);
            animator.CrossFade("StandingIdle", 0.2f);
        }
    }
}
