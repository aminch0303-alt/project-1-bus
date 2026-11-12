using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// GameManager controls the startup flow and gameplay transitions.
/// All references are serialized (Inspector drag-and-drop) with ZERO Find statements.
/// Future-proofed for garage bus selection and level selection.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("--- Cutscene Settings ---")]
    [Tooltip("Root GameObject of Cutscene 1 (Hierarchy mein jo Cutscene 1 hai)")]
    [SerializeField] private GameObject cutsceneObject;

    [Tooltip("PlayableDirector component on the Cutscene GameObject")]
    [SerializeField] private PlayableDirector cutsceneDirector;

    [Tooltip("Kya scene load hone par intro cutscene play ho?")]
    [SerializeField] private bool playIntroCutscene = true;

    [Header("--- RCC Gameplay Elements ---")]
    [Tooltip("RCC Camera GameObject")]
    [SerializeField] private GameObject rccCamera;

    [Tooltip("RCC Canvas (Speedometer, UI, Mobile controls)")]
    [SerializeField] private GameObject rccCanvas;

    [Header("--- Levels (Level Selection Ready) ---")]
    [Tooltip("Hierarchy se 'levels 1', 'levels 2', etc. drag karein")]
    [SerializeField] private GameObject[] levels;
    [Tooltip("Abhi konsa level chalana hai (0 = Level 1)")]
    [SerializeField] private int selectedLevelIndex = 0;

    [Header("--- Player Buses (Garage Selection Ready) ---")]
    [Tooltip("Hierarchy se player buses drag karein (Index 0 = Bus 1)")]
    [SerializeField] private GameObject[] playerBuses;
    [Tooltip("Abhi konsi bus chalani hai (0 = Bus 1)")]
    [SerializeField] private int selectedBusIndex = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        if (cutsceneDirector != null)
        {
            cutsceneDirector.stopped += OnCutsceneFinished;
        }
    }

    private void OnDisable()
    {
        if (cutsceneDirector != null)
        {
            cutsceneDirector.stopped -= OnCutsceneFinished;
        }
    }

    private void Start()
    {
        if (playIntroCutscene && cutsceneDirector != null && cutsceneObject != null)
        {
            StartCutsceneState();
        }
        else
        {
            StartGameplayState();
        }
    }

    /// <summary>
    /// Game start hone par cutscene mode setup karta hai:
    /// Gameplay elements band rehte hain aur Timeline play hoti hai.
    /// </summary>
    public void StartCutsceneState()
    {
        // 1. Cutscene ko ON karein
        if (cutsceneObject != null)
            cutsceneObject.SetActive(true);

        // 2. Gameplay elements ko OFF rakhein
        if (rccCamera != null)
            rccCamera.SetActive(false);

        if (rccCanvas != null)
            rccCanvas.SetActive(false);

        SetAllLevelsState(false);
        SetAllPlayerBusesState(false);

        // 3. Timeline play karein
        if (cutsceneDirector != null)
        {
            cutsceneDirector.time = 0;
            cutsceneDirector.Play();
        }
    }

    /// <summary>
    /// Timeline complete hone par automatically trigger hota hai.
    /// </summary>
    private void OnCutsceneFinished(PlayableDirector director)
    {
        StartGameplayState();
    }

    /// <summary>
    /// Cutscene band kar ke gameplay on karta hai:
    /// RCC Camera, UI, Level aur Player Bus activate ho jatay hain.
    /// </summary>
    public void StartGameplayState()
    {
        // 1. Cutscene OFF
        if (cutsceneObject != null)
            cutsceneObject.SetActive(false);

        // 2. RCC Camera & Canvas ON
        if (rccCamera != null)
            rccCamera.SetActive(true);

        if (rccCanvas != null)
            rccCanvas.SetActive(true);

        // 3. Level ON
        ActivateLevel(selectedLevelIndex);

        // 4. Player Bus ON
        ActivatePlayerBus(selectedBusIndex);
    }

    /// <summary>
    /// Specified index wala level ON karta hai aur baqi OFF karta hai.
    /// </summary>
    public void ActivateLevel(int index)
    {
        SetAllLevelsState(false);

        if (levels != null && index >= 0 && index < levels.Length)
        {
            if (levels[index] != null)
            {
                levels[index].SetActive(true);
            }
        }
    }

    /// <summary>
    /// Specified index wali player bus ON karta hai aur RCC register karta hai.
    /// </summary>
    public void ActivatePlayerBus(int index)
    {
        SetAllPlayerBusesState(false);

        if (playerBuses != null && index >= 0 && index < playerBuses.Length)
        {
            GameObject bus = playerBuses[index];
            if (bus != null)
            {
                bus.SetActive(true);

                // Agar bus par RCC component laga hai to RCC register karwayein
                RCC_CarControllerV3 carController = bus.GetComponent<RCC_CarControllerV3>();
                if (carController != null)
                {
                    RCC.RegisterPlayerVehicle(carController, true, true);
                }
            }
        }
    }

    private void SetAllPlayerBusesState(bool state)
    {
        if (playerBuses == null) return;
        for (int i = 0; i < playerBuses.Length; i++)
        {
            if (playerBuses[i] != null)
                playerBuses[i].SetActive(state);
        }
    }

    private void SetAllLevelsState(bool state)
    {
        if (levels == null) return;
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] != null)
                levels[i].SetActive(state);
        }
    }
}
