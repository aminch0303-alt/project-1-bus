using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;

/// <summary>
/// GameManager controls the startup flow, engine ignition, and gameplay transitions.
/// All references are serialized (Inspector drag-and-drop) with ZERO Find statements.
/// Future-proofed for garage bus selection and level selection.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("--- Cutscene Settings ---")]
    [Tooltip("Har level ka start animation object drag karein (Index 0 = Level (1), Index 1 = Level 2)")]
    [SerializeField] private GameObject[] levelCutsceneObjects;

    [Tooltip("Kya scene load hone par intro cutscene play ho?")]
    [SerializeField] private bool playIntroCutscene = true;

    private PlayableDirector activeCutsceneDirector;

    [Header("--- RCC Gameplay Elements ---")]
    [Tooltip("RCC Camera GameObject")]
    [SerializeField] private GameObject rccCamera;

    [Tooltip("RCC Canvas (Speedometer, UI, Mobile controls)")]
    [SerializeField] private GameObject rccCanvas;

    [Header("--- Engine Start UI ---")]
    [Tooltip("Engine Button GameObject (e.g. RCCCanvas -> Engine)")]
    [SerializeField] private GameObject engineButton;

    [Tooltip("Driving UI Controls (e.g. RCCCanvas -> Controller Buttons) jo engine start hone par ON hongi")]
    [SerializeField] private GameObject gameplayControlsUI;

    [Header("--- Levels (Level Selection Ready) ---")]
    [Tooltip("Hierarchy se 'levels 1', 'levels 2', etc. drag karein")]
    [SerializeField] private GameObject[] levels;
    [Tooltip("Abhi konsa level chalana hai (0 = Level 1)")]
    [SerializeField] private int selectedLevelIndex = 0;

    [Header("--- Level Starting Positions ---")]
    [Tooltip("Har level ki starting position/transform drag karein (Index 0 = Level 1 Starting Pos)")]
    [SerializeField] private Transform[] levelStartingPositions;

    [Header("--- Player Buses (Garage Selection Ready) ---")]
    [Tooltip("Hierarchy se player buses drag karein (Index 0 = Bus 1)")]
    [SerializeField] private GameObject[] playerBuses;
    [Tooltip("Abhi konsi bus chalani hai (0 = Bus 1)")]
    [SerializeField] private int selectedBusIndex = 0;

    [Header("--- Background Music ---")]
    [Tooltip("Background Music AudioSource (e.g. BG_Msuic)")]
    public AudioSource bgMusicAudioSource;

    private bool isWaitingForEngineStart = false;

    private RCC_UIDashboardDisplay GetDashboardDisplay()
    {
        return rccCanvas != null ? rccCanvas.GetComponent<RCC_UIDashboardDisplay>() : null;
    }

    private RCC_MobileButtons GetMobileButtons()
    {
        return rccCanvas != null ? rccCanvas.GetComponent<RCC_MobileButtons>() : null;
    }

    private void Update()
    {
        if (isWaitingForEngineStart)
        {
            if (gameplayControlsUI != null && gameplayControlsUI.activeSelf)
            {
                gameplayControlsUI.SetActive(false);
            }
        }
    }

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
        if (engineButton != null)
        {
            Button btn = engineButton.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(OnEngineButtonPressed);
                btn.onClick.AddListener(OnEngineButtonPressed);
            }
        }
    }

    private void OnDisable()
    {
        UnsubscribeActiveDirector();

        if (engineButton != null)
        {
            Button btn = engineButton.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(OnEngineButtonPressed);
            }
        }
    }

    private void Start()
    {
        if (playIntroCutscene)
        {
            StartCutsceneState();
        }
        else
        {
            StartGameplayState();
        }
    }

    private PlayableDirector GetCurrentCutsceneDirector()
    {
        if (levelCutsceneObjects != null && selectedLevelIndex >= 0 && selectedLevelIndex < levelCutsceneObjects.Length)
        {
            GameObject currentCutscene = levelCutsceneObjects[selectedLevelIndex];
            if (currentCutscene != null)
            {
                PlayableDirector dir = currentCutscene.GetComponent<PlayableDirector>();
                if (dir == null)
                    dir = currentCutscene.GetComponentInChildren<PlayableDirector>();
                return dir;
            }
        }

        return null;
    }

    private void UnsubscribeActiveDirector()
    {
        if (activeCutsceneDirector != null)
        {
            activeCutsceneDirector.stopped -= OnCutsceneFinished;
            activeCutsceneDirector = null;
        }
    }

    /// <summary>
    /// Game start hone par cutscene mode setup karta hai:
    /// Selected level ka cutscene ON hota hai aur Timeline play hoti hai.
    /// </summary>
    public void StartCutsceneState()
    {
        isWaitingForEngineStart = false;

        // Tamam cutscenes ko pehle OFF karein
        SetAllCutscenesState(false);

        // Gameplay elements ko OFF rakhein
        if (rccCamera != null)
            rccCamera.SetActive(false);

        if (rccCanvas != null)
            rccCanvas.SetActive(false);

        if (engineButton != null)
            engineButton.SetActive(false);

        if (gameplayControlsUI != null)
            gameplayControlsUI.SetActive(false);

        // Level aur Player Bus dono ko ON karein taake environment aur bus cutscene mein nazar aayein
        ActivateLevel(selectedLevelIndex);
        ActivatePlayerBus(selectedBusIndex);

        // Selected Level ka cutscene ON karein
        GameObject currentCutsceneObj = null;
        if (levelCutsceneObjects != null && selectedLevelIndex >= 0 && selectedLevelIndex < levelCutsceneObjects.Length)
        {
            currentCutsceneObj = levelCutsceneObjects[selectedLevelIndex];
        }

        if (currentCutsceneObj != null)
        {
            currentCutsceneObj.SetActive(true);
        }

        // Timeline play karein
        UnsubscribeActiveDirector();
        activeCutsceneDirector = GetCurrentCutsceneDirector();

        if (activeCutsceneDirector != null)
        {
            activeCutsceneDirector.stopped += OnCutsceneFinished;
            activeCutsceneDirector.time = 0;
            activeCutsceneDirector.Play();
        }
        else
        {
            // Agar director na mile to direct gameplay start kar dein
            StartGameplayState();
        }
    }

    /// <summary>
    /// Timeline complete hone par automatically trigger hota hai.
    /// </summary>
    private void OnCutsceneFinished(PlayableDirector director)
    {
        UnsubscribeActiveDirector();
        StartGameplayState();
    }

    /// <summary>
    /// UI Button click se cutscene foran skip karne ke liye.
    /// </summary>
    public void SkipCutscene()
    {
        if (activeCutsceneDirector != null)
        {
            activeCutsceneDirector.Stop();
        }
        UnsubscribeActiveDirector();
        StartGameplayState();
    }

    /// <summary>
    /// Cutscene band kar ke gameplay on karta hai:
    /// RCC Camera, Canvas, Level aur Player Bus activate ho jatay hain.
    /// Agar engineButton assigned hai to sirf engine button on hota hai aur controls off rehte hain.
    /// </summary>
    public void StartGameplayState()
    {
        // 1. Cutscenes OFF
        SetAllCutscenesState(false);

        // 2. RCC Camera & Canvas ON
        if (rccCamera != null)
            rccCamera.SetActive(true);

        if (rccCanvas != null)
            rccCanvas.SetActive(true);

            

        // 3. Level ON
        ActivateLevel(selectedLevelIndex);

        // 4. Player Bus ON (Engine OFF agar engineButton assigned ho)
        ActivatePlayerBus(selectedBusIndex);

        // 5. Engine Button & Driving Controls UI State
        if (engineButton != null)
        {
            isWaitingForEngineStart = true;

            // RCC Dashboard Display aur Mobile Buttons ko temporarily disable karein taake wo Controller Buttons ko Update mein zabardasti On na karein
            RCC_UIDashboardDisplay dashboard = GetDashboardDisplay();
            if (dashboard != null)
                dashboard.enabled = false;

            RCC_MobileButtons mobileButtons = GetMobileButtons();
            if (mobileButtons != null)
                mobileButtons.enabled = false;

            if (gameplayControlsUI != null)
                gameplayControlsUI.SetActive(false);

            engineButton.SetActive(true);
        }
        else
        {
            isWaitingForEngineStart = false;

            RCC_UIDashboardDisplay dashboard = GetDashboardDisplay();
            if (dashboard != null)
                dashboard.enabled = true;

            RCC_MobileButtons mobileButtons = GetMobileButtons();
            if (mobileButtons != null)
                mobileButtons.enabled = true;

            if (gameplayControlsUI != null)
                gameplayControlsUI.SetActive(true);
        }
    }

    /// <summary>
    /// Engine Button press hone par call hota hai:
    /// Engine button OFF ho jata hai, driving controls UI ON ho jati hai, aur bus engine start hota hai.
    /// </summary>
    public void OnEngineButtonPressed()
    {
        isWaitingForEngineStart = false;

        // 1. Engine button band karein
        if (engineButton != null)
            engineButton.SetActive(false);

        // 2. RCC components ko re-enable karein aur driving controls on karein
        RCC_UIDashboardDisplay dashboard = GetDashboardDisplay();
        if (dashboard != null)
            dashboard.enabled = true;

        RCC_MobileButtons mobileButtons = GetMobileButtons();
        if (mobileButtons != null)
            mobileButtons.enabled = true;

        if (gameplayControlsUI != null)
            gameplayControlsUI.SetActive(true);

        // 3. Player Bus ka Engine aur controls enable karein (realistic ignition sound ke sath)
        RCC_CarControllerV3 activeCar = GetCurrentPlayerCarController();
        if (activeCar != null)
        {
            activeCar.canControl = true;
            activeCar.handbrakeInput = 0f;
            activeCar.StartEngine();
        }

        // 4. Background music start karein
        PlayBGMusic();
    }

    /// <summary>
    /// Background music abhi chal raha hai ya nahi
    /// </summary>
    public bool IsMusicPlaying => bgMusicAudioSource != null && bgMusicAudioSource.isPlaying;

    /// <summary>
    /// Background music play/resume karne ke liye
    /// </summary>
    public void PlayBGMusic()
    {
        if (bgMusicAudioSource != null)
        {
            if (!bgMusicAudioSource.isPlaying)
            {
                bgMusicAudioSource.Play();
            }
        }
    }

    /// <summary>
    /// Middle animation / Cutscene ke dauran background music ko pause karne ke liye
    /// </summary>
    public void PauseBGMusic()
    {
        if (bgMusicAudioSource != null && bgMusicAudioSource.isPlaying)
        {
            bgMusicAudioSource.Pause();
        }
    }

    /// <summary>
    /// Middle animation / Cutscene complete hone par background music ko dubara continue karne ke liye
    /// </summary>
    public void ResumeBGMusic()
    {
        if (bgMusicAudioSource != null)
        {
            bgMusicAudioSource.UnPause();
        }
    }

    /// <summary>
    /// Abhi active player bus ka RCC_CarControllerV3 component laata hai.
    /// </summary>
    public RCC_CarControllerV3 GetCurrentPlayerCarController()
    {
        if (playerBuses != null && selectedBusIndex >= 0 && selectedBusIndex < playerBuses.Length)
        {
            GameObject bus = playerBuses[selectedBusIndex];
            if (bus != null)
            {
                return bus.GetComponent<RCC_CarControllerV3>();
            }
        }
        return null;
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
                // Level ke starting position par bus ko set karein
                if (levelStartingPositions != null && selectedLevelIndex >= 0 && selectedLevelIndex < levelStartingPositions.Length)
                {
                    Transform spawnPoint = levelStartingPositions[selectedLevelIndex];
                    if (spawnPoint != null)
                    {
                        bus.transform.position = spawnPoint.position;
                        bus.transform.rotation = spawnPoint.rotation;

                        Rigidbody rb = bus.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.linearVelocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }
                    }
                }

                bus.SetActive(true);

                // Agar bus par RCC component laga hai to RCC register karwayein
                RCC_CarControllerV3 carController = bus.GetComponent<RCC_CarControllerV3>();
                if (carController != null)
                {
                    // Agar engineButton use ho raha hai to engine aur controls start mein band rakhein
                    bool isEngineReady = (engineButton == null);
                    RCC.RegisterPlayerVehicle(carController, isEngineReady, isEngineReady);
                    carController.canControl = isEngineReady;

                    if (!isEngineReady)
                    {
                        carController.KillEngine();
                        carController.handbrakeInput = 1f;
                    }
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

    private void SetAllCutscenesState(bool state)
    {
        if (levelCutsceneObjects == null) return;
        for (int i = 0; i < levelCutsceneObjects.Length; i++)
        {
            if (levelCutsceneObjects[i] != null)
                levelCutsceneObjects[i].SetActive(state);
        }
    }
}
