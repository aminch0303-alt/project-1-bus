using UnityEngine;

public enum DropSide
{
    Left,
    Right
}

public enum PickApproachSide
{
    AutoDetect,
    DoorSide,
    OppositeSide
}

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

    [Header("Pick Settings (For Pick Trigger Only)")]
    [Tooltip("Is stop / level ke passengers ka parent root drag karein (e.g. levels 1 -> Passengers)")]
    [SerializeField] private Transform passengersRoot;

    [Tooltip("Passengers kis taraf se bus mein charhein ge: AutoDetect (position se), DoorSide, ya OppositeSide (bus ke aagay se wrap ho kar)")]
    [SerializeField] private PickApproachSide pickApproachSide = PickApproachSide.AutoDetect;

    public Transform PassengersRoot => passengersRoot;
    public PickApproachSide TargetPickSide => pickApproachSide;

    [Header("Luggage / Samaan Settings (For Pick Trigger Only)")]
    [Tooltip("Passengers ke pas zameen par pada samaan (e.g. Cycle, Bike, Bags) jo pick par hide hoga")]
    [SerializeField] private GameObject groundLuggage;

    [Tooltip("Bus ki chhat par rakha samaan jo pick par ON hoga")]
    [SerializeField] private GameObject busRoofLuggage;

    public GameObject GroundLuggage => groundLuggage;
    public GameObject BusRoofLuggage => busRoofLuggage;

    [Header("Drop Settings (For Drop Trigger Only)")]
    [Tooltip("Choose whether passengers drop to the Left or Right side of the bus.")]
    [SerializeField] private DropSide dropSide = DropSide.Left;

    [Tooltip("Optional custom target point where passengers will walk to. If assigned, dropSide is automatically detected from its position!")]
    [SerializeField] private Transform customDropTarget;

    public DropSide TargetDropSide => dropSide;
    public Transform CustomDropTarget => customDropTarget;

    [Header("Bus Alignment (Optional)")]
    [Tooltip("Jab bus is trigger ko lage to wo kis position/rotation par align ho (Empty GameObject marker)")]
    [SerializeField] private Transform busStopAlignPoint;

    [Tooltip("Bus ko align point tak pohnchne mein kitna waqt lage (e.g. 0.6 seconds glide)")]
    [SerializeField] private float alignmentDuration = 0.6f;

    public Transform BusStopAlignPoint => busStopAlignPoint;
    public float AlignmentDuration => alignmentDuration;

    [Header("Deactivation Settings")]
    [Tooltip("Disable trigger GameObject immediately when entered.")]
    [SerializeField] private bool deactivateOnTrigger = true;

    [Header("Audio Settings (Optional)")]
    [Tooltip("Drop trigger hit honay par bajne wala sound (e.g. 'reached at our destin' AudioSource)")]
    [SerializeField] private AudioSource arrivalAudioSource;

    [Header("Indicator Text Settings (Optional)")]
    [Tooltip("Drop point par pohnchne par jo UI text show karwana hai")]
    [SerializeField] private GameObject arrivalTextObject;

    [Tooltip("Kitni der text show rahay (default 3 seconds)")]
    [SerializeField] private float textDisplayDuration = 3f;

    public AudioSource ArrivalAudioSource => arrivalAudioSource;

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
                Debug.Log($"[BusStopTrigger] Drop stop triggered on '{gameObject.name}'! ArrivalTextObject: {(arrivalTextObject != null ? arrivalTextObject.name : "NULL")}");

                if (arrivalAudioSource != null)
                {
                    arrivalAudioSource.Play();
                }

                if (arrivalTextObject != null)
                {
                    arrivalTextObject.SetActive(true);
                    manager.StartCoroutine(ShowArrivalTextRoutine(arrivalTextObject, textDisplayDuration));
                }

                manager.OnBusArrivedAtDropStop(this);
            }
            else
            {
                manager.OnBusArrivedAtStop(this);
            }

            if (deactivateOnTrigger)
            {
                gameObject.SetActive(false);
            }
        }
    }

    private static System.Collections.IEnumerator ShowArrivalTextRoutine(GameObject textObj, float duration)
    {
        if (textObj != null)
        {
            textObj.SetActive(true);
            yield return new WaitForSeconds(duration);
            textObj.SetActive(false);
        }
    }
}
