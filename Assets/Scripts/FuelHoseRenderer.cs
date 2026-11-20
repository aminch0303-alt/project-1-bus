using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FuelHoseRenderer:
/// Procedural flexible rubber hose connecting the Petrol Pump machine to the Nozzle in the worker's hand.
/// Uses a curved LineRenderer with natural gravity sag so it stretches and bends smoothly as the worker walks.
/// Works in both Edit Mode ([ExecuteAlways]) and Play Mode!
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class FuelHoseRenderer : MonoBehaviour
{
    [Header("Connection Points")]
    [Tooltip("Petrol pump machine ka outlet / pipe connection point")]
    [SerializeField] private Transform pumpOutletPoint;

    [Tooltip("Worker ke haath mein pakri hui Nozzle ka base point")]
    [SerializeField] private Transform nozzleBasePoint;

    [Header("Hose Appearance")]
    [Range(0.02f, 0.2f)]
    [SerializeField] private float hoseThickness = 0.065f;

    [SerializeField] private Material hoseMaterial;
    [SerializeField] private Color hoseColor = new Color(0.12f, 0.12f, 0.12f, 1f); // Dark rubber black

    [Header("Curvature & Gravity Sag")]
    [Range(10, 50)]
    [SerializeField] private int segmentCount = 25;

    [Tooltip("Kitna nichay ki taraf rubber pipe jhukay (Gravity sag)")]
    [Range(0.1f, 3.0f)]
    [SerializeField] private float maxSag = 0.85f;

    [Tooltip("Jab worker door jaye to pipe kitni tight/stretch ho")]
    [Range(0.0f, 1.0f)]
    [SerializeField] private float minSag = 0.15f;

    [Tooltip("Approximate max length of hose before fully stretched")]
    [SerializeField] private float maxHoseLength = 6.0f;

    private LineRenderer _lineRenderer;

    private void Awake()
    {
        SetupLineRenderer();
    }

    private void OnEnable()
    {
        SetupLineRenderer();
    }

    private void SetupLineRenderer()
    {
        if (_lineRenderer == null)
        {
            _lineRenderer = GetComponent<LineRenderer>();
        }

        if (_lineRenderer != null)
        {
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.startWidth = hoseThickness;
            _lineRenderer.endWidth = hoseThickness;
            _lineRenderer.numCapVertices = 4;
            _lineRenderer.numCornerVertices = 4;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _lineRenderer.receiveShadows = true;

            if (hoseMaterial != null)
            {
                _lineRenderer.sharedMaterial = hoseMaterial;
            }
            else if (_lineRenderer.sharedMaterial == null)
            {
                // Fallback standard shader
                Shader standardShader = Shader.Find("Standard");
                if (standardShader == null) standardShader = Shader.Find("Universal Render Pipeline/Lit");
                if (standardShader != null)
                {
                    Material mat = new Material(standardShader);
                    mat.color = hoseColor;
                    _lineRenderer.sharedMaterial = mat;
                }
            }
        }
    }

    private void LateUpdate()
    {
        UpdateHoseCurve();
    }

    public void SetPoints(Transform pump, Transform nozzle)
    {
        pumpOutletPoint = pump;
        nozzleBasePoint = nozzle;
    }

    private void UpdateHoseCurve()
    {
        if (pumpOutletPoint == null || nozzleBasePoint == null) return;
        if (_lineRenderer == null) _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null) return;

        Vector3 pStart = pumpOutletPoint.position;
        Vector3 pEnd = nozzleBasePoint.position;

        float distance = Vector3.Distance(pStart, pEnd);

        // Natural sag: distance zyada ho to sag kam (pipe tight), pass ho to sag zyada
        float sagFactor = Mathf.Clamp01(1f - (distance / Mathf.Max(0.01f, maxHoseLength)));
        float currentSag = Mathf.Lerp(minSag, maxSag, sagFactor);

        // Control point for quadratic curve: halfway + pulled down by gravity
        Vector3 midPoint = (pStart + pEnd) * 0.5f;
        Vector3 controlPoint = midPoint + Vector3.down * currentSag;

        if (_lineRenderer.positionCount != segmentCount)
        {
            _lineRenderer.positionCount = segmentCount;
        }

        for (int i = 0; i < segmentCount; i++)
        {
            float t = (float)i / (segmentCount - 1);
            // Quadratic Bezier interpolation: B(t) = (1-t)^2 * P0 + 2(1-t)t * P1 + t^2 * P2
            float u = 1f - t;
            Vector3 point = (u * u * pStart) + (2f * u * t * controlPoint) + (t * t * pEnd);
            _lineRenderer.SetPosition(i, point);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SetupLineRenderer();
        UpdateHoseCurve();
    }
#endif
}
