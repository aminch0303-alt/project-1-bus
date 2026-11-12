using System.Collections;
using UnityEngine;

public class StationGateController : MonoBehaviour
{
    public enum GateControlMode
    {
        Animator,   // Uses Animator parameters / clips
        RotateY,    // Smooth script rotation on Y axis (no clip required, guaranteed!)
        Both        // Fires Animator AND script rotation
    }

    [Header("Control Mode")]
    [Tooltip("Choose RotateY for smooth guaranteed 85-degree rotation, or Animator if using an animation clip.")]
    [SerializeField] private GateControlMode controlMode = GateControlMode.RotateY;

    [Header("Rotation Settings (RotateY Mode)")]
    [Tooltip("Target Y rotation offset when opened (e.g. 85 for outward open).")]
    [SerializeField] private float openYAngle = 85f;
    [Tooltip("Time in seconds to open.")]
    [SerializeField] private float openDuration = 1.2f;
    [Tooltip("Time in seconds to close.")]
    [SerializeField] private float closeDuration = 1.2f;
    [SerializeField] private AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Gate Animator (Animator Mode)")]
    [SerializeField] private Animator gateAnimator;
    [SerializeField] private string openTrigger = "Open";
    [SerializeField] private string closeTrigger = "Close";
    [SerializeField] private string boolParam = "IsOpen";
    [SerializeField] private string stateName = "Gate open";

    [Header("Behavior Settings")]
    [Tooltip("Automatically close gate after bus leaves triggers.")]
    [SerializeField] private bool autoClose = true;
    [Tooltip("Delay in seconds before gate starts closing after leaving triggers.")]
    [SerializeField] private float autoCloseDelay = 5.0f;

    [Header("Sound Settings (Optional)")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField] private AudioSource audioSource;

    public bool IsOpen { get; private set; }

    private int _activeTriggersCount = 0;
    private Coroutine _closeCoroutine;
    private Coroutine _motionCoroutine;

    private Quaternion _closedLocalRot;
    private Quaternion _openLocalRot;

    private void Awake()
    {
        if (gateAnimator == null)
            gateAnimator = GetComponent<Animator>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        _closedLocalRot = transform.localRotation;
        _openLocalRot = _closedLocalRot * Quaternion.Euler(0f, openYAngle, 0f);
    }

    public void SetOpenYAngle(float angle)
    {
        openYAngle = angle;
        _openLocalRot = _closedLocalRot * Quaternion.Euler(0f, openYAngle, 0f);
    }

    public void OnTriggerZoneEntered(StationGateTrigger trigger)
    {
        _activeTriggersCount++;

        if (_closeCoroutine != null)
        {
            StopCoroutine(_closeCoroutine);
            _closeCoroutine = null;
        }

        OpenGate();
    }

    public void OnTriggerZoneExited(StationGateTrigger trigger)
    {
        _activeTriggersCount = Mathf.Max(0, _activeTriggersCount - 1);

        if (_activeTriggersCount == 0 && autoClose)
        {
            if (_closeCoroutine != null)
                StopCoroutine(_closeCoroutine);

            _closeCoroutine = StartCoroutine(AutoCloseRoutine());
        }
    }

    public void OpenGate()
    {
        if (IsOpen) return;
        IsOpen = true;

        // 1. Animator execution
        if (controlMode == GateControlMode.Animator || controlMode == GateControlMode.Both)
        {
            if (gateAnimator != null)
            {
                if (!string.IsNullOrEmpty(openTrigger) && HasParameter(gateAnimator, openTrigger))
                    gateAnimator.SetTrigger(openTrigger);

                if (!string.IsNullOrEmpty(boolParam) && HasParameter(gateAnimator, boolParam))
                    gateAnimator.SetBool(boolParam, true);

                if (!string.IsNullOrEmpty(stateName))
                    gateAnimator.Play(stateName);
            }
        }

        // 2. Smooth Script Rotation (85 degree outward turn)
        if (controlMode == GateControlMode.RotateY || controlMode == GateControlMode.Both)
        {
            if (gateAnimator != null && controlMode == GateControlMode.RotateY)
            {
                gateAnimator.enabled = false; // Prevent empty Animator from resetting transform rotation
            }

            if (_motionCoroutine != null)
                StopCoroutine(_motionCoroutine);

            _motionCoroutine = StartCoroutine(RotateGateRoutine(transform.localRotation, _openLocalRot, openDuration));
        }

        PlaySound(openSound);
    }

    public void CloseGate()
    {
        if (!IsOpen) return;
        IsOpen = false;

        // 1. Animator execution
        if (controlMode == GateControlMode.Animator || controlMode == GateControlMode.Both)
        {
            if (gateAnimator != null)
            {
                if (!string.IsNullOrEmpty(closeTrigger) && HasParameter(gateAnimator, closeTrigger))
                    gateAnimator.SetTrigger(closeTrigger);

                if (!string.IsNullOrEmpty(boolParam) && HasParameter(gateAnimator, boolParam))
                    gateAnimator.SetBool(boolParam, false);
            }
        }

        // 2. Smooth Script Rotation back to closed
        if (controlMode == GateControlMode.RotateY || controlMode == GateControlMode.Both)
        {
            if (_motionCoroutine != null)
                StopCoroutine(_motionCoroutine);

            _motionCoroutine = StartCoroutine(RotateGateRoutine(transform.localRotation, _closedLocalRot, closeDuration));
        }

        PlaySound(closeSound);
    }

    private IEnumerator RotateGateRoutine(Quaternion fromRot, Quaternion toRot, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = motionCurve != null ? motionCurve.Evaluate(t) : t;

            transform.localRotation = Quaternion.Slerp(fromRot, toRot, curvedT);
            yield return null;
        }

        transform.localRotation = toRot;
        _motionCoroutine = null;
    }

    private bool HasParameter(Animator anim, string paramName)
    {
        if (anim == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (var p in anim.parameters)
        {
            if (p.name.Equals(paramName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private IEnumerator AutoCloseRoutine()
    {
        yield return new WaitForSeconds(autoCloseDelay);

        if (_activeTriggersCount == 0)
        {
            CloseGate();
        }

        _closeCoroutine = null;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
