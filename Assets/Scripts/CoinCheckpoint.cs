using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// Checkpoint trigger that spawns a Coin Burst particle effect,
/// shows a bouncing and floating +$500 text, and deactivates itself on player hit.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CoinCheckpoint : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Target tag for detection (e.g. 'Player')")]
    [SerializeField] private string targetTag = "Player";

    [Header("Coin Burst Particle")]
    [Tooltip("Drag 'FX_Money_Coins_Burst_01' prefab here from Project panel")]
    [SerializeField] private GameObject coinBurstPrefab;
    [SerializeField] private Vector3 burstOffset = new Vector3(0, 0.5f, 0);

    [Header("Floating Text Effect")]
    [Tooltip("Reward text to display (e.g. +$500)")]
    [SerializeField] private string rewardText = "+$500";
    [SerializeField] private Color textColor = new Color(0.1f, 1f, 0.2f, 1f); // Vibrant bright green
    [SerializeField] private float floatHeight = 2.8f;
    [SerializeField] private float floatDuration = 1.3f;
    [Tooltip("Optional custom prefab for floating text (if empty, generates 3D text automatically)")]
    [SerializeField] private GameObject customTextPrefab;

    [Header("Sound Effect (Optional)")]
    [SerializeField] private AudioClip collectSound;
    [SerializeField] [Range(0f, 1f)] private float soundVolume = 1.0f;

    [Header("Deactivation")]
    [Tooltip("The object to turn OFF on collect (e.g. 'green ' or 'Checkpoint'). If empty, disables this GameObject.")]
    [SerializeField] private GameObject objectToDeactivate;

    private bool _collected = false;
    private System.Action _onCollectedCallback;

    public void SetSequenceCallback(System.Action callback)
    {
        _onCollectedCallback = callback;
    }

    private void Awake()
    {
        // Ensure collider is set to trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (objectToDeactivate == null)
        {
            objectToDeactivate = gameObject;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected) return;

        bool isPlayer = false;

        if (other.CompareTag(targetTag))
            isPlayer = true;
        else if (other.transform.root != null && other.transform.root.CompareTag(targetTag))
            isPlayer = true;
        else if (other.GetComponentInParent<RCC_CarControllerV3>() != null)
            isPlayer = true;

        if (isPlayer)
        {
            Collect();
        }
    }

    public void Collect()
    {
        if (_collected) return;
        _collected = true;

        Vector3 spawnPos = transform.position + burstOffset;

        // 1. Spawn Coin Burst Particle Effect
        if (coinBurstPrefab != null)
        {
            GameObject fx = Instantiate(coinBurstPrefab, spawnPos, Quaternion.identity);
            Destroy(fx, 3.0f);
        }

        // 2. Play Sound
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, spawnPos, soundVolume);
        }

        // 3. Spawn Floating Text
        SpawnFloatingRewardText(spawnPos);

        // 4. Notify CheckpointSequence if present
        _onCollectedCallback?.Invoke();

        // 5. Deactivate Checkpoint
        if (objectToDeactivate != null)
        {
            objectToDeactivate.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void SpawnFloatingRewardText(Vector3 position)
    {
        if (customTextPrefab != null)
        {
            GameObject textObj = Instantiate(customTextPrefab, position, Quaternion.identity);
            AnimateCustomTextObject(textObj, position);
            return;
        }

        // Auto 3D TextMeshPro billboard text
        GameObject textGo = new GameObject("FloatingRewardText");
        textGo.transform.position = position + Vector3.up * 0.8f;

        TextMeshPro tmp = textGo.AddComponent<TextMeshPro>();
        tmp.text = rewardText;
        tmp.fontSize = 8;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.fontStyle = FontStyles.Bold;

        // Billboard rotation so it always faces camera
        textGo.AddComponent<CheckpointBillboard>();

        // DOTween smooth animations
        textGo.transform.localScale = Vector3.zero;
        textGo.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
        textGo.transform.DOMoveY(textGo.transform.position.y + floatHeight, floatDuration).SetEase(Ease.OutCubic);

        tmp.DOFade(0f, 0.4f).SetDelay(floatDuration - 0.4f).OnComplete(() =>
        {
            Destroy(textGo);
        });
    }

    private void AnimateCustomTextObject(GameObject textObj, Vector3 startPos)
    {
        textObj.transform.localScale = Vector3.zero;
        textObj.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
        textObj.transform.DOMoveY(startPos.y + floatHeight, floatDuration).SetEase(Ease.OutCubic);

        TMP_Text tmp = textObj.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = rewardText;
            tmp.DOFade(0f, 0.4f).SetDelay(floatDuration - 0.4f).OnComplete(() =>
            {
                Destroy(textObj);
            });
        }
        else
        {
            Destroy(textObj, floatDuration);
        }
    }
}

/// <summary>
/// Helper to keep floating text facing the camera.
/// </summary>
public class CheckpointBillboard : MonoBehaviour
{
    private Camera _cam;

    private void Start()
    {
        _cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (_cam == null || !_cam.enabled || !_cam.gameObject.activeInHierarchy)
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                foreach (var c in cams)
                {
                    if (c.enabled && c.gameObject.activeInHierarchy)
                    {
                        _cam = c;
                        break;
                    }
                }
            }
        }

        if (_cam != null)
        {
            transform.rotation = _cam.transform.rotation;
        }
    }
}
