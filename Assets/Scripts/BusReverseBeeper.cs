using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bus Reverse Beeper:
/// Bus ke Reverse Light GameObject par ya Bus par lagayein.
/// Jab bhi bus Reverse gear mein aayegi to sound loop mein play hoga.
/// </summary>
public class BusReverseBeeper : MonoBehaviour
{
    [Header("--- Audio Settings ---")]
    [Tooltip("Reverse beep audio clip (e.g. Reverse.wav)")]
    [SerializeField] private AudioClip reverseClip;

    [Tooltip("Volume (0 to 1)")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.85f;

    [Tooltip("3D Sound distance")]
    [SerializeField] private float maxDistance = 35f;

    private RCC_CarControllerV3 _carController;
    private AudioSource _audioSource;

    private void Awake()
    {
        _carController = GetComponentInParent<RCC_CarControllerV3>();

        // AudioSource setup
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.clip = reverseClip;
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f; // 3D sound
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.minDistance = 2f;
        _audioSource.maxDistance = maxDistance;
        _audioSource.volume = volume;
    }

    private void Update()
    {
        if (_carController == null || _audioSource == null) return;

        // Jab bus Reverse gear mein ho (direction == -1) aur canControl ho
        bool isReversing = (_carController.direction == -1 && _carController.canControl);

        if (isReversing)
        {
            if (!_audioSource.isPlaying && _audioSource.clip != null)
            {
                _audioSource.Play();
            }
        }
        else
        {
            if (_audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }
    }
}
