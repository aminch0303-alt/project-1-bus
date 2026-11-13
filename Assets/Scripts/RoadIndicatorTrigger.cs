using System.Collections;
using UnityEngine;

public class RoadIndicatorTrigger : MonoBehaviour
{
    [Header("Indicator Settings")]
    [Tooltip("The AudioSource to play when triggered.")]
    public AudioSource audioSource;

    [Tooltip("The UI Text or GameObject to display for 3 seconds.")]
    public GameObject indicatorTextObject;

    [Tooltip("How long the indicator text stays on screen (default 3 seconds).")]
    public float displayDuration = 3f;

    [Header("Trigger Detection")]
    [Tooltip("Tag used to identify the player vehicle.")]
    public string targetTag = "Player";

    [Tooltip("If true, this trigger will only activate once per session/level.")]
    public bool triggerOnce = true;

    private bool hasTriggered = false;
    private Coroutine hideCoroutine;

    private void Awake()
    {
        // Ensure the text indicator is hidden at startup
        if (indicatorTextObject != null)
        {
            indicatorTextObject.SetActive(false);
        }

        // Ensure audio source does not play automatically on awake
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnce) return;

        if (IsPlayer(other))
        {
            TriggerIndicator();
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;

        // Check collider tag or root hierarchy tag (useful for RCC compound colliders)
        if (other.CompareTag(targetTag)) return true;
        if (other.transform.root != null && other.transform.root.CompareTag(targetTag)) return true;

        return false;
    }

    public void TriggerIndicator()
    {
        hasTriggered = true;

        // 1. Play Audio
        if (audioSource != null)
        {
            audioSource.Play();
        }

        // 2. Show Indicator Text
        if (indicatorTextObject != null)
        {
            indicatorTextObject.SetActive(true);

            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }
            hideCoroutine = StartCoroutine(HideTextAfterDuration());
        }

        // 3. Disable collider if single-use
        if (triggerOnce)
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }
        }
    }

    private IEnumerator HideTextAfterDuration()
    {
        yield return new WaitForSeconds(displayDuration);

        if (indicatorTextObject != null)
        {
            indicatorTextObject.SetActive(false);
        }
        hideCoroutine = null;
    }
}
