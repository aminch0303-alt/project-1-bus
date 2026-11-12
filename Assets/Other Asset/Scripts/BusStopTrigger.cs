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

        BusBoardingManager manager = boardingManager != null ? boardingManager : BusBoardingManager.Instance;

        bool isBus = false;
        if (manager != null)
        {
            if (other.transform == manager.transform || other.transform.IsChildOf(manager.transform))
            {
                isBus = true;
            }
        }

        if (!isBus)
        {
            if (other.CompareTag(targetTag) || (other.transform.root != null && other.transform.root.CompareTag(targetTag)))
            {
                isBus = true;
            }
        }

        if (isBus)
        {
            _triggered = true;

            bool isDrop = isDropTrigger || CompareTag("Drop") || gameObject.name.ToLower().Contains("drop");

            if (manager != null)
            {
                if (isDrop)
                {
                    manager.OnBusArrivedAtDropStop();
                }
                else
                {
                    manager.OnBusArrivedAtStop();
                }
            }

            if (deactivateOnTrigger)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
