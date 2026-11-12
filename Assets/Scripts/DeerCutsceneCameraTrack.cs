using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the cinematic camera panning/tracking during the deer stampede.
/// Mimics the smooth roadside panning shot from the reference video.
/// </summary>
public class DeerCutsceneCameraTrack : MonoBehaviour
{
    [Header("Tracking Settings")]
    [SerializeField] private Transform targetFocusPoint;
    [SerializeField] private float smoothSpeed = 3.5f;
    [SerializeField] private Vector3 lookOffset = new Vector3(0, 1.2f, 0);

    private bool _active = false;
    private Quaternion _initialRotation;

    public void StartTracking(Transform focusPoint)
    {
        targetFocusPoint = focusPoint;
        _initialRotation = transform.rotation;
        _active = true;
    }

    public void StopTracking()
    {
        _active = false;
    }

    private void LateUpdate()
    {
        if (!_active || targetFocusPoint == null) return;

        Vector3 targetPos = targetFocusPoint.position + lookOffset;
        Vector3 dir = (targetPos - transform.position);
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, smoothSpeed * Time.deltaTime);
        }
    }
}
