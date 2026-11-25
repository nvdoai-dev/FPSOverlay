using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Profiling;
using UnityEngine.UI;

public sealed class FpsOverlay : MonoBehaviour
{
    [SerializeField] private Text fpsLabel;
    [SerializeField] private int _targetFrameRate = 60;
    [SerializeField] private RectTransform overlayBackground;
    [SerializeField] private Toggle settingsToggle;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider frameRateSlider;
    [SerializeField] private Text targetFrameRateLabel;

    public int TargetFrameRate
    {
        get => IsSupportedFrameRate(_targetFrameRate) ? _targetFrameRate : 60;
        set
        {
            _targetFrameRate = IsSupportedFrameRate(value) ? value : 60;
            if (Application.isPlaying)
                Application.targetFrameRate = _targetFrameRate;
            UpdateTargetFrameRateLabel();
        }
    }

    private static readonly int[] SupportedFrameRates = { 30, 60, 90, 120 };
    private const float OverlayWidth = 880f;
    private const float CollapsedHeight = 136f;
    private const float ExpandedHeight = 360f;
    private const float UiRefreshInterval = 0.3f;
    private ProfilerRecorder _mainThreadTimeRecorder;
    private bool _mainThreadTimeRecorderStarted;
    private float _uiRefreshTimer;

    private void Awake()
    {
        EnsureEventSystem();
        Application.targetFrameRate = TargetFrameRate;

        if (fpsLabel == null)
            fpsLabel = GetComponentInChildren<Text>(true);

        if (overlayBackground != null)
        {
            if (overlayBackground.sizeDelta.x < OverlayWidth)
            {
                overlayBackground.anchoredPosition *= 2f;
                overlayBackground.sizeDelta *= 2f;
                ScaleOverlayContents();
            }

            var size = overlayBackground.sizeDelta;
            size.x = OverlayWidth;
            overlayBackground.sizeDelta = size;
        }

        if (settingsToggle != null)
        {
            settingsToggle.onValueChanged.AddListener(SetSettingsVisible);
            SetSettingsVisible(settingsToggle.isOn);
        }

        if (frameRateSlider != null)
        {
            frameRateSlider.minValue = 0f;
            frameRateSlider.maxValue = SupportedFrameRates.Length - 1;
            frameRateSlider.wholeNumbers = true;
            frameRateSlider.SetValueWithoutNotify(GetFrameRateIndex(TargetFrameRate));
            frameRateSlider.onValueChanged.AddListener(SetFrameRateFromSlider);
        }

        UpdateTargetFrameRateLabel();
    }

    private void ScaleOverlayContents()
    {
        foreach (var rect in overlayBackground.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect == overlayBackground)
                continue;

            rect.anchoredPosition *= 2f;
            rect.sizeDelta *= 2f;
            rect.offsetMin *= 2f;
            rect.offsetMax *= 2f;
        }

        foreach (var text in overlayBackground.GetComponentsInChildren<Text>(true))
            text.fontSize *= 2;
    }

    private void OnEnable()
    {
        _mainThreadTimeRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Internal,
            "Main Thread",
            1);
        _mainThreadTimeRecorderStarted = _mainThreadTimeRecorder.Valid;
        if (!_mainThreadTimeRecorderStarted)
            Debug.LogWarning("FPS Overlay could not record the Unity Main Thread profiler metric.", this);
    }

    private void OnDisable()
    {
        if (_mainThreadTimeRecorderStarted)
        {
            _mainThreadTimeRecorder.Dispose();
            _mainThreadTimeRecorderStarted = false;
        }
    }

    private void OnDestroy()
    {
        if (settingsToggle != null)
            settingsToggle.onValueChanged.RemoveListener(SetSettingsVisible);

        if (frameRateSlider != null)
            frameRateSlider.onValueChanged.RemoveListener(SetFrameRateFromSlider);
    }

    private void Update()
    {
        if (fpsLabel == null)
            return;

        _uiRefreshTimer += Time.unscaledDeltaTime;
        if (_uiRefreshTimer < UiRefreshInterval)
            return;

        _uiRefreshTimer %= UiRefreshInterval;

        var frameTimeSeconds = Time.unscaledDeltaTime;
        var measuredFps = frameTimeSeconds > 0f ? 1f / frameTimeSeconds : 0f;
        var fps = Mathf.Min(measuredFps, TargetFrameRate);
        var frameTimeMilliseconds = frameTimeSeconds * 1000f;
        var frameBudgetMilliseconds = 1000f / TargetFrameRate;
        var cpuTimeNanoseconds = _mainThreadTimeRecorderStarted
            ? _mainThreadTimeRecorder.LastValue
            : 0;
        var cpuTime = cpuTimeNanoseconds > 0
            ? $"{cpuTimeNanoseconds * 1e-6f:0.0} ms"
            : "--";
        fpsLabel.text =
            $"FPS: {fps:0} | Frame: {frameTimeMilliseconds:0.0}/{frameBudgetMilliseconds:0.0} ms\n" +
            $"Main Thread: {cpuTime}";
    }

    private static bool IsSupportedFrameRate(int frameRate)
    {
        return frameRate == 30 || frameRate == 60 || frameRate == 90 || frameRate == 120;
    }

    private static int GetFrameRateIndex(int frameRate)
    {
        for (var index = 0; index < SupportedFrameRates.Length; index++)
        {
            if (SupportedFrameRates[index] == frameRate)
                return index;
        }

        return 1;
    }

    private void SetFrameRateFromSlider(float sliderValue)
    {
        var index = Mathf.Clamp(Mathf.RoundToInt(sliderValue), 0, SupportedFrameRates.Length - 1);
        TargetFrameRate = SupportedFrameRates[index];
    }

    private void SetSettingsVisible(bool visible)
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(visible);

        if (overlayBackground != null)
        {
            var size = overlayBackground.sizeDelta;
            size.x = OverlayWidth;
            size.y = visible ? ExpandedHeight : CollapsedHeight;
            overlayBackground.sizeDelta = size;
        }
    }

    private void UpdateTargetFrameRateLabel()
    {
        if (targetFrameRateLabel != null)
            targetFrameRateLabel.text = $"Target: {TargetFrameRate} FPS";
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        var eventSystemObject = new GameObject("FPS Overlay EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }
}
