using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BusStopTrigger : MonoBehaviour
{
    [Header("Manager Reference")]
    [Tooltip("Direct reference to BusBoardingManager on Bus.")]
    [SerializeField] private BusBoardingManager boardingManager;

    [Header("Trigger Settings")]
    [Tooltip("Direct reference to the trigger collider.")]
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private bool isDropTrigger;

    [Header("Deactivation Settings")]
    [Tooltip("Disable trigger GameObject immediately when entered.")]
    [SerializeField] private bool deactivateOnTrigger = true;

    private bool _triggered;

    private void Awake()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;

        // 1. Dynamically find the BusBoardingManager on the bus that actually entered the trigger
        BusBoardingManager manager = other.GetComponentInParent<BusBoardingManager>();
        if (manager == null && other.transform.root != null)
        {
            manager = other.transform.root.GetComponentInChildren<BusBoardingManager>();
        }

        // 2. Check if the colliding object is a bus / player
        bool isBus = (manager != null) ||
                     other.CompareTag(targetTag) ||
                     (other.transform.root != null && other.transform.root.CompareTag(targetTag));

        // 3. Fallback to inspector reference or static instance ONLY if they are active in the scene
        if (manager == null && isBus)
        {
            if (boardingManager != null && boardingManager.gameObject.activeInHierarchy)
            {
                manager = boardingManager;
            }
            else if (BusBoardingManager.Instance != null && BusBoardingManager.Instance.gameObject.activeInHierarchy)
            {
                manager = BusBoardingManager.Instance;
            }
        }

        if (isBus)
        {
            if (manager == null)
            {
                Debug.LogWarning($"[BusStopTrigger] Object with tag '{targetTag}' entered '{gameObject.name}', but no active BusBoardingManager was found!");
                return;
            }

            _triggered = true;

            bool isDrop = isDropTrigger || CompareTag("Drop") || gameObject.name.ToLower().Contains("drop");

            if (isDrop)
            {
                manager.OnBusArrivedAtDropStop();
            }
            else
            {
                manager.OnBusArrivedAtStop();
            }

            if (deactivateOnTrigger)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
