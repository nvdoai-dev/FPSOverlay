using UnityEngine;
using UnityEngine.UI;

public sealed class FpsOverlay : MonoBehaviour
{
    [SerializeField] private Text fpsLabel;

    private int _frameCount;
    private float _elapsed;

    private void Awake()
    {
        if (fpsLabel == null)
            fpsLabel = GetComponentInChildren<Text>(true);
    }

    private void Update()
    {
        _frameCount++;
        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed < 0.5f)
            return;

        if (fpsLabel != null)
            fpsLabel.text = $"FPS: {_frameCount / _elapsed:0}";

        _frameCount = 0;
        _elapsed = 0f;
    }
}
