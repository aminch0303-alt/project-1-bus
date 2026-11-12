using System.Collections;
using UnityEngine;

public class BusDoorController : MonoBehaviour
{
    [Header("Plug-Slide Offsets (Local Space)")]
    [Tooltip("Local offset to pop outward away from the bus body.")]
    [SerializeField] private Vector3 outwardOffset = new Vector3(-0.18f, 0f, 0f);

    [Tooltip("Local offset to slide backward along the bus wall.")]
    [SerializeField] private Vector3 slideOffset = new Vector3(0f, 0f, -0.9f);

    [Header("Timing Settings")]
    [Tooltip("Duration for the outward pop stage.")]
    [SerializeField] private float outwardDuration = 0.6f;

    [Tooltip("Duration for the slide backward stage.")]
    [SerializeField] private float slideDuration = 1.4f;

    [Tooltip("Curve for smooth door motion.")]
    [SerializeField] private AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 _closedLocalPos;
    private Quaternion _closedLocalRot;
    private Vector3 _outwardLocalPos;
    private Vector3 _openLocalPos;
    private Coroutine _motionCoroutine;

    public bool IsOpen { get; private set; }
    public float TotalOpenDuration => outwardDuration + slideDuration;
    public float TotalCloseDuration => slideDuration + outwardDuration;

    private void Awake()
    {
        _closedLocalPos = transform.localPosition;
        _closedLocalRot = transform.localRotation;
        CalculateTargetPositions();
    }

    private void CalculateTargetPositions()
    {
        _outwardLocalPos = _closedLocalPos + outwardOffset;
        _openLocalPos = _outwardLocalPos + slideOffset;
    }

    public void OpenDoor()
    {
        if (IsOpen) return;
        IsOpen = true;

        if (_motionCoroutine != null)
            StopCoroutine(_motionCoroutine);

        _motionCoroutine = StartCoroutine(OpenRoutine());
    }

    public void CloseDoor()
    {
        if (!IsOpen) return;
        IsOpen = false;

        if (_motionCoroutine != null)
            StopCoroutine(_motionCoroutine);

        _motionCoroutine = StartCoroutine(CloseRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        CalculateTargetPositions();
        transform.localRotation = _closedLocalRot;

        // Stage 1: Move outward (pop out from bus body)
        yield return MoveToLocal(transform.localPosition, _outwardLocalPos, outwardDuration);

        // Stage 2: Slide backward along the side of the bus
        yield return MoveToLocal(_outwardLocalPos, _openLocalPos, slideDuration);

        transform.localPosition = _openLocalPos;
        transform.localRotation = _closedLocalRot;
        _motionCoroutine = null;
    }

    private IEnumerator CloseRoutine()
    {
        CalculateTargetPositions();
        transform.localRotation = _closedLocalRot;

        // Stage 1: Slide forward back to the doorway opening
        yield return MoveToLocal(transform.localPosition, _outwardLocalPos, slideDuration);

        // Stage 2: Move inward (flush with bus body)
        yield return MoveToLocal(_outwardLocalPos, _closedLocalPos, outwardDuration);

        transform.localPosition = _closedLocalPos;
        transform.localRotation = _closedLocalRot;
        _motionCoroutine = null;
    }

    private IEnumerator MoveToLocal(Vector3 fromPos, Vector3 toPos, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = motionCurve != null ? motionCurve.Evaluate(t) : t;

            transform.localPosition = Vector3.Lerp(fromPos, toPos, curvedT);
            transform.localRotation = _closedLocalRot; // Ensure no rotation
            yield return null;
        }

        transform.localPosition = toPos;
        transform.localRotation = _closedLocalRot;
    }
}
