using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StationGateTrigger : MonoBehaviour
{
    public enum TriggerSide { RoadSide, StationSide }

    [Header("Settings")]
    [SerializeField] private TriggerSide side = TriggerSide.RoadSide;
    [SerializeField] private StationGateController gateController;
    [SerializeField] private string targetTag = "Player";

    private Collider _col;
    private int _insideCount = 0;

    private void Awake()
    {
        _col = GetComponent<Collider>();
        if (_col != null)
        {
            _col.isTrigger = true;
        }

        FindControllerIfNull();
    }

    private void Start()
    {
        FindControllerIfNull();
    }

    private void FindControllerIfNull()
    {
        if (gateController == null)
        {
            gateController = GetComponentInParent<StationGateController>();
            if (gateController == null)
            {
                gateController = Object.FindAnyObjectByType<StationGateController>();
            }
        }
    }

    public void SetGateController(StationGateController controller, TriggerSide triggerSide)
    {
        gateController = controller;
        side = triggerSide;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        _insideCount++;

        // Only fire on the first collider entering (e.g. wheels/body of the same bus)
        if (_insideCount == 1)
        {
            if (gateController == null) FindControllerIfNull();

            if (gateController != null)
            {
                gateController.OnTriggerZoneEntered(this);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        _insideCount = Mathf.Max(0, _insideCount - 1);

        // Only fire when the entire vehicle has exited
        if (_insideCount == 0)
        {
            if (gateController == null) FindControllerIfNull();

            if (gateController != null)
            {
                gateController.OnTriggerZoneExited(this);
            }
        }
    }

    private bool IsPlayer(Collider other)
    {
        // 1. Fast Tag check (direct)
        if (other.CompareTag(targetTag)) return true;

        // 2. Root Tag check (no expensive searches)
        Transform root = other.transform.root;
        if (root != null && root.CompareTag(targetTag)) return true;

        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = (side == TriggerSide.RoadSide)
            ? new Color(0.1f, 0.9f, 0.2f, 0.35f)
            : new Color(0.1f, 0.5f, 1.0f, 0.35f);

        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = (side == TriggerSide.RoadSide) ? Color.green : Color.cyan;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = oldMatrix;
        }
    }
#endif
}
