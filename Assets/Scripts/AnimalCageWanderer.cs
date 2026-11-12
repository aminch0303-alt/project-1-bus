using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AudioSource))]
public class AnimalCageWanderer : MonoBehaviour
{
    [Header("Cage Boundary")]
    [Tooltip("Center of the cage. If zero, animal's starting position is used.")]
    [SerializeField] private Vector3 cageCenter = Vector3.zero;

    [Tooltip("Radius inside the cage the animal can wander.")]
    [SerializeField] private float walkRadius = 3.5f;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 1.2f;
    [SerializeField] private float turnSpeed = 4.0f;
    [SerializeField] private float arrivalThreshold = 0.35f;

    [Header("Timing & Idle")]
    [SerializeField] private float minIdleTime = 3.0f;
    [SerializeField] private float maxIdleTime = 7.0f;

    [Header("Action / Roar Animation")]
    [Tooltip("Name of the trigger parameter in Animator (e.g. Action, Roar, Howl).")]
    [SerializeField] private string actionTriggerName = "Action";
    [Range(0f, 1f)]
    [SerializeField] private float actionChance = 0.3f;

    [Header("Sound Settings (Drop your Audio Clips here)")]
    [Tooltip("Drag & drop sound clips for this animal here (Roar, Howl, Call, etc.).")]
    [SerializeField] private AudioClip[] animalSounds;

    [Tooltip("Max 3D distance where sound is audible (reaches road/bus).")]
    [SerializeField] private float soundAudibleDistance = 60.0f;

    [Tooltip("Minimum time between animal sound calls.")]
    [SerializeField] private float minSoundCooldown = 8.0f;

    [Tooltip("Maximum time between animal sound calls.")]
    [SerializeField] private float maxSoundCooldown = 18.0f;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1.0f;

    [Tooltip("Only make sound when player/bus is within audible distance.")]
    [SerializeField] private bool onlySoundWhenPlayerNear = true;

    [Tooltip("Direct reference to Player/Bus. If null, automatically looks for tag 'Player'.")]
    [SerializeField] private Transform playerTransform;

    [Header("Ground Snapping")]
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;

    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private int _actionHash;

    private Vector3 _effectiveCenter;
    private Coroutine _wanderCoroutine;
    private Coroutine _soundCoroutine;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f; // Full 3D spatial sound
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 5.0f;
            audioSource.maxDistance = soundAudibleDistance;
            audioSource.volume = soundVolume;
        }

        if (!string.IsNullOrEmpty(actionTriggerName))
        {
            _actionHash = Animator.StringToHash(actionTriggerName);
        }

        _effectiveCenter = (cageCenter == Vector3.zero) ? transform.position : cageCenter;
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else if (Camera.main != null) playerTransform = Camera.main.transform;
        }

        _wanderCoroutine = StartCoroutine(WanderLoop());
        _soundCoroutine = StartCoroutine(SoundLoop());
    }

    private void OnDisable()
    {
        if (_wanderCoroutine != null)
        {
            StopCoroutine(_wanderCoroutine);
            _wanderCoroutine = null;
        }

        if (_soundCoroutine != null)
        {
            StopCoroutine(_soundCoroutine);
            _soundCoroutine = null;
        }

        if (animator != null)
        {
            animator.SetBool(IsWalkingHash, false);
        }
    }

    private IEnumerator WanderLoop()
    {
        yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));

        while (true)
        {
            // 1. Idle state
            SetWalking(false);

            float idleDuration = Random.Range(minIdleTime, maxIdleTime);

            // Occasional action animation during idle
            if (Random.value < actionChance)
            {
                yield return new WaitForSeconds(Random.Range(1.0f, 2.0f));
                TriggerAction();
                yield return new WaitForSeconds(2.0f);
            }
            else
            {
                yield return new WaitForSeconds(idleDuration);
            }

            // 2. Pick a random point inside cage circle
            Vector2 randomPoint2D = Random.insideUnitCircle * walkRadius;
            Vector3 targetPos = new Vector3(_effectiveCenter.x + randomPoint2D.x, transform.position.y, _effectiveCenter.z + randomPoint2D.y);

            if (snapToGround)
            {
                float groundY = GetGroundHeight(targetPos);
                if (groundY > float.MinValue)
                {
                    targetPos.y = groundY;
                }
            }

            // 3. Turn & Walk towards target
            SetWalking(true);

            while (true)
            {
                Vector3 currentFlat = new Vector3(transform.position.x, 0f, transform.position.z);
                Vector3 targetFlat = new Vector3(targetPos.x, 0f, targetPos.z);
                float dist = Vector3.Distance(currentFlat, targetFlat);

                if (dist <= arrivalThreshold)
                    break;

                // Rotation
                Vector3 moveDir = (targetPos - transform.position);
                Vector3 flatDir = new Vector3(moveDir.x, 0f, moveDir.z);

                if (flatDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(flatDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                }

                // Translation
                Vector3 nextPos = Vector3.MoveTowards(transform.position, targetPos, walkSpeed * Time.deltaTime);

                if (snapToGround)
                {
                    float currentGroundY = GetGroundHeight(nextPos);
                    if (currentGroundY > float.MinValue)
                    {
                        nextPos.y = currentGroundY;
                    }
                }

                transform.position = nextPos;
                yield return null;
            }

            SetWalking(false);
        }
    }

    private IEnumerator SoundLoop()
    {
        yield return new WaitForSeconds(Random.Range(2.0f, 6.0f));

        while (true)
        {
            float wait = Random.Range(minSoundCooldown, maxSoundCooldown);
            yield return new WaitForSeconds(wait);

            if (animalSounds == null || animalSounds.Length == 0)
                continue;

            bool canPlay = true;
            if (onlySoundWhenPlayerNear)
            {
                if (playerTransform == null)
                {
                    GameObject p = GameObject.FindWithTag("Player");
                    if (p != null) playerTransform = p.transform;
                    else if (Camera.main != null) playerTransform = Camera.main.transform;
                }

                if (playerTransform != null)
                {
                    float d = Vector3.Distance(transform.position, playerTransform.position);
                    canPlay = (d <= soundAudibleDistance);
                }
            }

            if (canPlay && audioSource != null)
            {
                AudioClip clip = animalSounds[Random.Range(0, animalSounds.Length)];
                if (clip != null)
                {
                    audioSource.pitch = Random.Range(0.95f, 1.05f);
                    audioSource.PlayOneShot(clip, soundVolume);

                    // Also trigger animation call
                    TriggerAction();
                }
            }
        }
    }

    private void SetWalking(bool walking)
    {
        if (animator != null)
        {
            animator.SetBool(IsWalkingHash, walking);
        }
    }

    private void TriggerAction()
    {
        if (animator != null && _actionHash != 0)
        {
            animator.SetTrigger(_actionHash);
        }
    }

    private float GetGroundHeight(Vector3 pos)
    {
        Vector3 origin = pos + Vector3.up * 3.0f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10.0f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform != transform && !hit.transform.IsChildOf(transform))
            {
                return hit.point.y;
            }
        }
        return float.MinValue;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Cage boundary circle
        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.75f);
        Vector3 center = (cageCenter == Vector3.zero) ? transform.position : cageCenter;
        Gizmos.DrawWireSphere(center, walkRadius);

        // Center marker
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(center, 0.15f);

        // Audible distance
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(center, soundAudibleDistance);
    }
#endif
}
