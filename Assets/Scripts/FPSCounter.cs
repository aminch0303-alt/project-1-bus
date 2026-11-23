using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Real-Time Mobile FPS Counter:
/// - Real-time Frames Per Second (FPS) aur Frame Time (ms) calculate karta hai.
/// - Colors: Green (50+ FPS), Yellow (30-49 FPS), Red (<30 FPS).
/// - 2 in 1: Agar TMP_Text assign ho to UI par show karega, warna screen ke corner par auto draw karega (Zero UI Setup needed).
/// </summary>
public class FPSCounter : MonoBehaviour
{
    public enum ScreenPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    [Header("--- Display Options ---")]
    [Tooltip("Screen ke kis konay mein FPS show ho (agar OnGUI use ho)")]
    [SerializeField] private ScreenPosition position = ScreenPosition.TopRight;

    [Tooltip("Text ka size")]
    [SerializeField] private int fontSize = 22;

    [Tooltip("Kitne waqt baad number update ho (0.2s - 0.3s recommended taake flicker na kare)")]
    [SerializeField] private float updateInterval = 0.3f;

    [Tooltip("Frame Time (ms) sath show karna hai?")]
    [SerializeField] private bool showMilliseconds = true;

    [Header("--- Optional UI Text Reference ---")]
    [Tooltip("Agar Canvas UI par TextMeshPro use karna ho to yahan drag karein (Optional)")]
    [SerializeField] private TextMeshProUGUI tmpFpsText;

    [Tooltip("Agar standard UI Text use karna ho to yahan drag karein (Optional)")]
    [SerializeField] private Text uiFpsText;

    // Calculation variables
    private float _accumulatedDeltaTime = 0f;
    private int _frameCount = 0;
    private float _timeSinceLastUpdate = 0f;

    private float _currentFps = 0f;
    private float _currentFrameTimeMs = 0f;
    private string _fpsDisplayText = "FPS: --";
    private Color _fpsColor = Color.green;

    // GUI Style cache
    private GUIStyle _style;
    private GUIStyle _bgStyle;
    private Texture2D _bgTexture;

    private void Start()
    {
        // FPSCounter does not override Application.targetFrameRate, allowing AutoGraphicSettings
        // to cleanly manage the target frame rate (e.g. locked 30 FPS on budget devices).
    }

    private void Update()
    {
        _accumulatedDeltaTime += Time.unscaledDeltaTime;
        _frameCount++;
        _timeSinceLastUpdate += Time.unscaledDeltaTime;

        if (_timeSinceLastUpdate >= updateInterval)
        {
            _currentFps = _frameCount / _accumulatedDeltaTime;
            _currentFrameTimeMs = (_accumulatedDeltaTime / _frameCount) * 1000f;

            // Color coding: Green, Yellow, Red
            if (_currentFps >= 50f)
            {
                _fpsColor = new Color(0.2f, 1f, 0.2f, 1f); // Vibrant Green
            }
            else if (_currentFps >= 30f)
            {
                _fpsColor = new Color(1f, 0.85f, 0.1f, 1f); // Warm Yellow
            }
            else
            {
                _fpsColor = new Color(1f, 0.25f, 0.25f, 1f); // Alert Red
            }

            // Text formatting
            if (showMilliseconds)
            {
                _fpsDisplayText = string.Format("FPS: {0:F0} ({1:F1}ms)", _currentFps, _currentFrameTimeMs);
            }
            else
            {
                _fpsDisplayText = string.Format("FPS: {0:F0}", _currentFps);
            }

            // Update UI components if assigned
            if (tmpFpsText != null)
            {
                tmpFpsText.text = _fpsDisplayText;
                tmpFpsText.color = _fpsColor;
            }

            if (uiFpsText != null)
            {
                uiFpsText.text = _fpsDisplayText;
                uiFpsText.color = _fpsColor;
            }

            // Reset trackers
            _accumulatedDeltaTime = 0f;
            _frameCount = 0;
            _timeSinceLastUpdate = 0f;
        }
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        // Agar TMP ya UI Text assigned ho to OnGUI draw na kare
        if (tmpFpsText != null || uiFpsText != null) return;

        InitStyles();

        // Calculate rect based on corner position
        float width = showMilliseconds ? 160f : 100f;
        float height = fontSize + 16f;
        float margin = 15f;

        float x = margin;
        float y = margin;

        switch (position)
        {
            case ScreenPosition.TopLeft:
                x = margin;
                y = margin;
                break;
            case ScreenPosition.TopRight:
                x = Screen.width - width - margin;
                y = margin;
                break;
            case ScreenPosition.BottomLeft:
                x = margin;
                y = Screen.height - height - margin;
                break;
            case ScreenPosition.BottomRight:
                x = Screen.width - width - margin;
                y = Screen.height - height - margin;
                break;
        }

        Rect bgRect = new Rect(x, y, width, height);
        GUI.Box(bgRect, GUIContent.none, _bgStyle);

        _style.normal.textColor = _fpsColor;
        GUI.Label(bgRect, _fpsDisplayText, _style);
    }
#endif

    private void InitStyles()
    {
        if (_style == null)
        {
            _style = new GUIStyle();
            _style.fontSize = fontSize;
            _style.fontStyle = FontStyle.Bold;
            _style.alignment = TextAnchor.MiddleCenter;
        }

        if (_bgStyle == null)
        {
            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f)); // Translucent black pill
            _bgTexture.Apply();

            _bgStyle = new GUIStyle();
            _bgStyle.normal.background = _bgTexture;
        }
    }

    private void OnDestroy()
    {
        if (_bgTexture != null)
        {
            Destroy(_bgTexture);
        }
    }
}
