using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controls sequential activation of checkpoints for a specific level.
/// 100% serialized array with ZERO Find/GetComponent calls at runtime for maximum performance (0 lag).
/// </summary>
public class CheckpointSequence : MonoBehaviour
{
    [Header("--- Checkpoints Array (Inspector Drag & Drop) ---")]
    [Tooltip("Drag all checkpoints of this level in order (Element 0 = 1st checkpoint, Element 1 = 2nd, etc.)")]
    [SerializeField] private GameObject[] checkpoints;

    [Header("--- Sequence Completion ---")]
    [Tooltip("Aakhri checkpoint collect hone par jo Drop Trigger ON hona hai (Drag Drop Trigger here)")]
    [SerializeField] private GameObject dropTriggerToActivate;

    [Tooltip("Optional event triggered when the very last checkpoint is collected")]
    [SerializeField] private UnityEvent onSequenceCompleted;

    private int _currentIndex = 0;

    private void Awake()
    {
        // Wire up callbacks directly in Awake (Zero runtime overhead)
        if (checkpoints != null)
        {
            for (int i = 0; i < checkpoints.Length; i++)
            {
                if (checkpoints[i] != null)
                {
                    CoinCheckpoint coin = checkpoints[i].GetComponent<CoinCheckpoint>();
                    if (coin != null)
                    {
                        int index = i;
                        coin.SetSequenceCallback(() => OnCheckpointCollected(index));
                    }
                }
            }
        }
    }

    private void OnEnable()
    {
        InitializeSequence();
    }

    /// <summary>
    /// Resets the sequence: Activates the 1st checkpoint and disables all others.
    /// </summary>
    public void InitializeSequence()
    {
        _currentIndex = 0;

        // Sequence ke shuru mein Drop Trigger ko OFF rakhein
        if (dropTriggerToActivate != null)
        {
            dropTriggerToActivate.SetActive(false);
        }

        if (checkpoints == null || checkpoints.Length == 0) return;

        for (int i = 0; i < checkpoints.Length; i++)
        {
            if (checkpoints[i] != null)
            {
                // Only index 0 starts active, rest are inactive
                checkpoints[i].SetActive(i == 0);
            }
        }
    }

    /// <summary>
    /// Called when a checkpoint is collected by the player.
    /// </summary>
    public void OnCheckpointCollected(int index)
    {
        if (index != _currentIndex) return;

        // Turn OFF current collected checkpoint
        if (_currentIndex < checkpoints.Length && checkpoints[_currentIndex] != null)
        {
            checkpoints[_currentIndex].SetActive(false);
        }

        _currentIndex++;

        // Turn ON next checkpoint if available
        if (_currentIndex < checkpoints.Length)
        {
            if (checkpoints[_currentIndex] != null)
            {
                checkpoints[_currentIndex].SetActive(true);
            }
        }
        else
        {
            // All checkpoints in this sequence collected!
            if (dropTriggerToActivate != null)
            {
                dropTriggerToActivate.SetActive(true);
            }

            onSequenceCompleted?.Invoke();
        }
    }
}
