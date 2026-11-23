using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bus Reverse Beeper & Front Blinker:
/// - Bus ke Reverse Light GameObject par ya Bus par lagayein.
/// - Jab bhi bus Reverse gear mein aayegi to sound loop mein play hoga.
/// - RCC ke built-in hazard/indicator lights automatically blink karein ge.
/// - Agar custom front light GameObjects hon to wo bhi flash/blink karein ge.
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

    [Header("--- RCC Built-in Hazard Blinkers ---")]
    [Tooltip("Reverse gear mein RCC ke indicators ko All (Hazard) par auto-blink karwana")]
    [SerializeField] private bool blinkRCCIndicators = true;

    [Header("--- Headlight Blinking on Reverse ---")]
    [Tooltip("Reverse gear mein Headlights ko bhi ON/OFF blink karwana taake front shine flash kare")]
    [SerializeField] private bool blinkHeadlights = true;

    [Tooltip("Headlights ko RCC indicators ke rhythm (0.5s) ke saath sync rakhna")]
    [SerializeField] private bool syncWithIndicators = true;

    [Header("--- Custom Front Lights Blinking (Optional) ---")]
    [Tooltip("Agar koi custom front lights / headlights GameObjects ko blink karwana ho to yahan drag karein")]
    [SerializeField] private GameObject[] customFrontLightsToBlink;

    [Tooltip("Custom blink speed (agar syncWithIndicators off ho)")]
    [SerializeField] private float blinkInterval = 0.35f;

    private RCC_CarControllerV3 _carController;
    private AudioSource _audioSource;
    private bool _wasReversing = false;
    private bool _originalHeadlightsOn = false;

    private float _blinkTimer = 0f;
    private bool _blinkState = false;

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
        if (_carController == null) return;

        // Jab bus Reverse gear mein ho (direction == -1) aur canControl ho
        bool isReversing = (_carController.direction == -1 && _carController.canControl);

        if (isReversing)
        {
            // Pehli dafa reverse gear enter hone par original headlight status save karein
            if (!_wasReversing)
            {
                _originalHeadlightsOn = _carController.lowBeamHeadLightsOn;
            }

            // 1. Play reverse sound
            if (_audioSource != null && !_audioSource.isPlaying && _audioSource.clip != null)
            {
                _audioSource.Play();
            }

            // 2. RCC Built-in Hazard Blinker ON
            if (blinkRCCIndicators && _carController.indicatorsOn != RCC_CarControllerV3.IndicatorsOn.All)
            {
                _carController.indicatorsOn = RCC_CarControllerV3.IndicatorsOn.All;
            }

            // 3. Headlight Blinking (Front lights shine on/off)
            if (blinkHeadlights)
            {
                if (syncWithIndicators)
                {
                    // RCC indicatorTimer: 0.0 - 0.5s is ON, 0.5 - 1.0s is OFF
                    bool flashState = (_carController.indicatorTimer < 0.5f);
                    _carController.lowBeamHeadLightsOn = flashState;
                }
                else
                {
                    _blinkTimer += Time.deltaTime;
                    if (_blinkTimer >= blinkInterval)
                    {
                        _blinkTimer = 0f;
                        _blinkState = !_blinkState;
                        _carController.lowBeamHeadLightsOn = _blinkState;
                    }
                }
            }

            // 4. Custom front lights blinking
            if (customFrontLightsToBlink != null && customFrontLightsToBlink.Length > 0)
            {
                bool customState = syncWithIndicators ? (_carController.indicatorTimer < 0.5f) : _blinkState;
                SetCustomLightsActive(customState);
            }

            _wasReversing = true;
        }
        else
        {
            if (_wasReversing)
            {
                // 1. Stop reverse sound
                if (_audioSource != null && _audioSource.isPlaying)
                {
                    _audioSource.Stop();
                }

                // 2. RCC Indicators OFF
                if (blinkRCCIndicators && _carController.indicatorsOn == RCC_CarControllerV3.IndicatorsOn.All)
                {
                    _carController.indicatorsOn = RCC_CarControllerV3.IndicatorsOn.Off;
                }

                // 3. Restore original headlights state
                if (blinkHeadlights)
                {
                    _carController.lowBeamHeadLightsOn = _originalHeadlightsOn;
                }

                // 4. Reset custom front lights
                if (customFrontLightsToBlink != null && customFrontLightsToBlink.Length > 0)
                {
                    SetCustomLightsActive(false);
                }

                _blinkTimer = 0f;
                _blinkState = false;
                _wasReversing = false;
            }
        }
    }

    private void SetCustomLightsActive(bool state)
    {
        if (customFrontLightsToBlink == null) return;

        for (int i = 0; i < customFrontLightsToBlink.Length; i++)
        {
            // Sirf tab SetActive call hoga jab state waqayi badli ho (1 second mein 60 dafa call hone se bachega)
            if (customFrontLightsToBlink[i] != null && customFrontLightsToBlink[i].activeSelf != state)
            {
                customFrontLightsToBlink[i].SetActive(state);
            }
        }
    }

    private void OnDisable()
    {
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }

        if (_carController != null)
        {
            if (_carController.indicatorsOn == RCC_CarControllerV3.IndicatorsOn.All)
            {
                _carController.indicatorsOn = RCC_CarControllerV3.IndicatorsOn.Off;
            }

            if (_wasReversing && blinkHeadlights)
            {
                _carController.lowBeamHeadLightsOn = _originalHeadlightsOn;
            }
        }

        SetCustomLightsActive(false);
        _wasReversing = false;
    }
}
