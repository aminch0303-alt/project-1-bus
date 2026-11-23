using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Persistent, zero-GC screen fade controller for smooth cinematic and cutscene transitions.
/// Inspector se direct references assign karein (No auto-detect, zero runtime GC).
/// </summary>
public class ScreenFadeManager : MonoBehaviour
{
    public static ScreenFadeManager Instance { get; private set; }

    [Header("Fade UI References (Inspector se Drag Karein)")]
    [Tooltip("Hierarchy se 'ScreenFadeCanvas' drag karein")]
    [SerializeField] private Canvas fadeCanvas;

    [Tooltip("Hierarchy se 'FadeImage' drag karein")]
    [SerializeField] private Image fadeImage;

    private Tween _fadeTween;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Initialize state cleanly
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
            fadeImage.raycastTarget = false;
        }

        if (fadeCanvas != null)
        {
            fadeCanvas.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Smoothly fades the screen to black (Alpha 0 -> 1).
    /// </summary>
    public IEnumerator FadeOutRoutine(float duration)
    {
        if (fadeImage == null) yield break;

        if (fadeCanvas != null) fadeCanvas.gameObject.SetActive(true);
        _fadeTween?.Kill();
        fadeImage.raycastTarget = true;
        _fadeTween = fadeImage.DOFade(1f, duration).SetUpdate(true);
        yield return _fadeTween.WaitForCompletion();
    }

    /// <summary>
    /// Smoothly fades from black back to clear game screen (Alpha 1 -> 0).
    /// </summary>
    public IEnumerator FadeInRoutine(float duration)
    {
        if (fadeImage == null) yield break;

        if (fadeCanvas != null) fadeCanvas.gameObject.SetActive(true);
        _fadeTween?.Kill();
        _fadeTween = fadeImage.DOFade(0f, duration).SetUpdate(true);
        yield return _fadeTween.WaitForCompletion();

        fadeImage.raycastTarget = false;
        if (fadeCanvas != null) fadeCanvas.gameObject.SetActive(false);
    }

    /// <summary>
    /// Instantly sets the screen fade alpha without animation (e.g. on cutscene skip or reset).
    /// </summary>
    public void SetAlphaImmediate(float alpha)
    {
        _fadeTween?.Kill();

        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = alpha;
            fadeImage.color = c;
            fadeImage.raycastTarget = alpha > 0.01f;
        }

        if (fadeCanvas != null)
        {
            fadeCanvas.gameObject.SetActive(alpha > 0.01f);
        }
    }
}
