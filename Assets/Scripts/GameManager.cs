using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using TMPro;
using DG.Tweening;

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

    [Header("--- Level Direction Arrows ---")]
    [Tooltip("Har level ke direction arrows drag karein (Index 0 = Level 1 Arrows, Index 1 = Level 2 Arrows)")]
    [SerializeField] private GameObject[] levelArrows;

    [Header("--- Player Buses (Garage Selection Ready) ---")]
    [Tooltip("Hierarchy se player buses drag karein (Index 0 = Bus 1)")]
    [SerializeField] private GameObject[] playerBuses;
    [Tooltip("Abhi konsi bus chalani hai (0 = Bus 1)")]
    [SerializeField] private int selectedBusIndex = 0;

    [Header("--- Background Music ---")]
    [Tooltip("Background Music AudioSource (e.g. BG_Msuic)")]
    public AudioSource bgMusicAudioSource;
    private bool isMusicPausedByUser = false;

    [Header("--- Level Complete Panel (Optional) ---")]
    [Tooltip("Hierarchy se 'Level Complete Panel' drag karein taake Next/Restart hone par auto-hide ho")]
    [SerializeField] private GameObject levelCompletePanel;

    [Header("--- Test / Debug Settings ---")]
    [Tooltip("Build mein testing ke liye chotay Level Switch buttons dikhayein (Top-Right)")]
    [SerializeField] private bool showDebugLevelSwitcher = true;

    [Header("--- Canvas Reward UI (Floating Text) ---")]
    [Tooltip("Canvas se 'Reward Text' (Legacy Text) drag karein")]
    [SerializeField] private Text rewardTextUI;
    [SerializeField] private float rewardAnimDuration = 1.2f;
    [SerializeField] private float rewardFloatYOffset = 45f;

    private Vector2 _rewardTextInitialAnchoredPos;
    private Color _initialRewardTextColor;
    private Tween _rewardScaleTween;
    private Tween _rewardMoveTween;
    private Tween _rewardFadeTween;

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

        // Initialize Reward Text UI state
        if (rewardTextUI != null)
        {
            _rewardTextInitialAnchoredPos = rewardTextUI.rectTransform.anchoredPosition;
            _initialRewardTextColor = rewardTextUI.color;
            rewardTextUI.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Displays a floating pop & fade reward notification on screen (e.g. +$100, +$500).
    /// Safe to call repeatedly; smoothly resets and bounces on every call.
    /// </summary>
    public void ShowRewardNotification(string textToDisplay)
    {
        if (rewardTextUI == null) return;

        // Kill any existing running animations
        _rewardScaleTween?.Kill();
        _rewardMoveTween?.Kill();
        _rewardFadeTween?.Kill();

        RectTransform rt = rewardTextUI.rectTransform;
        rewardTextUI.text = textToDisplay;

        // Reset color alpha
        Color c = _initialRewardTextColor;
        c.a = 1f;
        rewardTextUI.color = c;

        // Reset starting position & scale
        rt.anchoredPosition = _rewardTextInitialAnchoredPos;
        rt.localScale = Vector3.zero;

        rewardTextUI.gameObject.SetActive(true);

        // 1. Pop Bounce Animation: scale 0 -> 1 with overshoot ease
        _rewardScaleTween = rt.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack);

        // 2. Float upwards smoothly
        _rewardMoveTween = rt.DOAnchorPosY(_rewardTextInitialAnchoredPos.y + rewardFloatYOffset, rewardAnimDuration).SetEase(Ease.OutCubic);

        // 3. Smooth fade out near the end
        float fadeDuration = 0.35f;
        float fadeDelay = Mathf.Max(0f, rewardAnimDuration - fadeDuration);

        _rewardFadeTween = rewardTextUI.DOFade(0f, fadeDuration).SetDelay(fadeDelay).OnComplete(() =>
        {
            if (rewardTextUI != null) rewardTextUI.gameObject.SetActive(false);
        });
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

        // Cutscene ke dauran Direction Arrows ko OFF rakhein
        SetCurrentLevelArrowsState(false);

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

        // Gameplay start hone par Direction Arrows ko ON karein
        SetCurrentLevelArrowsState(true);

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
        isMusicPausedByUser = false;

        if (bgMusicAudioSource != null)
        {
            if (!bgMusicAudioSource.isPlaying)
            {
                bgMusicAudioSource.UnPause();
                if (!bgMusicAudioSource.isPlaying)
                {
                    bgMusicAudioSource.Play();
                }
            }
        }
    }

    /// <summary>
    /// User Pause Button click se music pause karne ke liye (Canvas Pause Button ke OnClick par yeh call karein ya PauseBGMusic)
    /// </summary>
    public void PauseBGMusicByUser()
    {
        isMusicPausedByUser = true;
        if (bgMusicAudioSource != null && bgMusicAudioSource.isPlaying)
        {
            bgMusicAudioSource.Pause();
        }
    }

    /// <summary>
    /// Middle animation / Cutscene ke dauran background music ko pause karne ke liye
    /// </summary>
    public void PauseBGMusic()
    {
        // Jab user ne Pause button dabaya ho tab bhi yeh call hota hai (OnClick mein)
        isMusicPausedByUser = true;

        if (bgMusicAudioSource != null && bgMusicAudioSource.isPlaying)
        {
            bgMusicAudioSource.Pause();
        }
    }

    /// <summary>
    /// Cutscene shuru honay par sirf temporary pause karne ke liye (user state ko change kiye baghair)
    /// </summary>
    public void PauseBGMusicForCutscene()
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
        // Agar user ne Canvas se music pause kiya hua hai to cutscene khatam hone par play mat karo!
        if (isMusicPausedByUser) return;

        if (bgMusicAudioSource != null)
        {
            bgMusicAudioSource.UnPause();
            if (!bgMusicAudioSource.isPlaying)
            {
                bgMusicAudioSource.Play();
            }
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

    private void SetCurrentLevelArrowsState(bool state)
    {
        // Pehle tamam level arrows ko band karein taake koi galat arrows na dikhein
        if (levelArrows != null)
        {
            for (int i = 0; i < levelArrows.Length; i++)
            {
                if (levelArrows[i] != null)
                    levelArrows[i].SetActive(false);
            }

            // Sirf current selected level ke arrows ko desired state (true/false) dein
            if (state && selectedLevelIndex >= 0 && selectedLevelIndex < levelArrows.Length)
            {
                if (levelArrows[selectedLevelIndex] != null)
                {
                    levelArrows[selectedLevelIndex].SetActive(true);
                }
            }
        }
    }

    #region Level Switching & Testing API

    /// <summary>
    /// Next Level load karta hai (e.g. Level 1 se Level 2).
    /// Level Complete Panel ke 'Next' button ke OnClick par yeh call hoga.
    /// </summary>
    public void LoadNextLevel()
    {
        int nextIndex = selectedLevelIndex + 1;
        if (levels != null && nextIndex >= levels.Length)
        {
            nextIndex = 0; // Wrap around to Level 1 if max reached
        }
        LoadLevel(nextIndex);
    }

    /// <summary>
    /// Current Level ko shuru se restart karta hai.
    /// Level Complete / Pause ke 'Restart' button par use ho sakta hai.
    /// </summary>
    public void RestartCurrentLevel()
    {
        LoadLevel(selectedLevelIndex);
    }

    /// <summary>
    /// Kisi bhi specified level ko directly load karta hai (0 = Level 1, 1 = Level 2).
    /// </summary>
    public void LoadLevel(int levelIndex)
    {
        selectedLevelIndex = levelIndex;

        // Hide Level Complete panel if active
        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(false);
        }

        // Cleanup any active cutscene director callbacks
        if (activeCutsceneDirector != null)
        {
            activeCutsceneDirector.stopped -= OnCutsceneFinished;
            activeCutsceneDirector.Stop();
            activeCutsceneDirector = null;
        }

        // Start from Cutscene if enabled, otherwise directly to gameplay
        if (playIntroCutscene)
        {
            StartCutsceneState();
        }
        else
        {
            StartGameplayState();
        }
    }

    private void OnGUI()
    {
        if (!showDebugLevelSwitcher) return;

        // Clean, small semi-transparent GUI buttons on top-right for quick build testing
        int totalLevels = (levels != null) ? levels.Length : 2;
        int buttonWidth = 85;
        int buttonHeight = 45;
        int margin = 10;

        GUI.skin.button.fontSize = 18;

        for (int i = 0; i < totalLevels; i++)
        {
            int x = Screen.width - (buttonWidth + margin) * (totalLevels - i);
            int y = margin;

            bool isCurrent = (i == selectedLevelIndex);
            GUI.color = isCurrent ? new Color(0.2f, 1f, 0.4f, 0.9f) : new Color(1f, 1f, 1f, 0.7f);

            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), $"Lvl {i + 1}"))
            {
                LoadLevel(i);
            }
        }

        GUI.color = Color.white;
    }

    #endregion
}
