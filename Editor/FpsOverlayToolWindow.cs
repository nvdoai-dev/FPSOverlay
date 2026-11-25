using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class FpsOverlayToolWindow : EditorWindow
{
    private const string OverlayName = "FPS Overlay";
    private static readonly int[] SupportedFrameRates = { 30, 60, 90, 120 };
    private readonly Dictionary<string, bool> _sceneOverlayStatus = new Dictionary<string, bool>();
    private Vector2 _sceneListScrollPosition;

    [MenuItem("Tools/Add FPS Overlay")]
    private static void Open()
    {
        GetWindow<FpsOverlayToolWindow>("FPS Overlay Tool");
    }

    private void OnFocus()
    {
        RefreshSceneStatuses();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("FPS UI", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The overlay measures FPS during play. Players can change the frame-rate target using the in-game SET control.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Scenes in Build Settings", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Refresh status", GUILayout.Width(110f)))
                    RefreshSceneStatuses();
            }
        }

        var buildScenes = EditorBuildSettings.scenes;
        if (buildScenes.Length == 0)
        {
            EditorGUILayout.HelpBox("No scenes are listed in Build Settings.", MessageType.Warning);
            return;
        }

        _sceneListScrollPosition = EditorGUILayout.BeginScrollView(_sceneListScrollPosition);
        foreach (var buildScene in buildScenes)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var sceneName = Path.GetFileNameWithoutExtension(buildScene.path);
                EditorGUILayout.LabelField(
                    string.IsNullOrEmpty(sceneName) ? buildScene.path : sceneName,
                    GUILayout.MinWidth(120f));

                if (!buildScene.enabled)
                    GUILayout.Label("Disabled", GUILayout.Width(58f));

                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path))
                {
                    GUILayout.Label("Scene not found", GUILayout.Width(100f));
                    continue;
                }

                var hasOverlay = _sceneOverlayStatus.TryGetValue(buildScene.path, out var status) && status;
                GUILayout.Label(
                    !_sceneOverlayStatus.ContainsKey(buildScene.path)
                        ? "Not checked"
                        : hasOverlay ? "FPS added" : "Needs FPS",
                    GUILayout.Width(75f));

                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(hasOverlay ? "Remove" : "Add", GUILayout.Width(70f)))
                    {
                        SetOverlayInBuildScene(buildScene.path, !hasOverlay);
                        _sceneOverlayStatus[buildScene.path] = !hasOverlay;
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.HelpBox(
            "New overlays start at 60 FPS. Players can change the target in-game. Changes to scenes already open are marked dirty; save those scenes in the editor. Closed scenes are saved by this tool.",
            MessageType.None);
    }

    private static FpsOverlay FindOverlay(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        foreach (var root in scene.GetRootGameObjects())
        {
            var overlay = root.GetComponentInChildren<FpsOverlay>(true);
            if (overlay != null)
                return overlay;
        }

        return null;
    }

    private void RefreshSceneStatuses()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        _sceneOverlayStatus.Clear();
        var activeScene = SceneManager.GetActiveScene();

        try
        {
            foreach (var buildScene in EditorBuildSettings.scenes)
            {
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path))
                    continue;

                var scene = SceneManager.GetSceneByPath(buildScene.path);
                var openedTemporarily = !scene.IsValid() || !scene.isLoaded;
                if (openedTemporarily)
                    scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);

                try
                {
                    var overlay = FindOverlay(scene);
                    _sceneOverlayStatus[buildScene.path] = overlay != null;
                }
                finally
                {
                    if (openedTemporarily && scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
        finally
        {
            if (activeScene.IsValid() && activeScene.isLoaded)
                SceneManager.SetActiveScene(activeScene);
        }

        Repaint();
    }

    private void SetOverlayInBuildScene(string scenePath, bool addOverlay)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var previousActiveScene = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(scenePath);
        var openedTemporarily = !scene.IsValid() || !scene.isLoaded;
        if (openedTemporarily)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        var closeTemporaryScene = openedTemporarily;
        try
        {
            var existingOverlay = FindOverlay(scene);
            if (addOverlay && existingOverlay == null)
            {
                if (openedTemporarily)
                    closeTemporaryScene = false;
                AddOverlay(scene, false);
                if (openedTemporarily)
                {
                    if (!EditorSceneManager.SaveScene(scene))
                    {
                        EditorUtility.DisplayDialog(
                            "FPS Overlay",
                            $"Could not save {scenePath}. The scene is left open so you can save it manually.",
                            "OK");
                        return;
                    }

                    closeTemporaryScene = true;
                }
            }
            else if (!addOverlay && existingOverlay != null)
            {
                if (openedTemporarily)
                    closeTemporaryScene = false;
                RemoveOverlay(existingOverlay);
                if (openedTemporarily)
                {
                    if (!EditorSceneManager.SaveScene(scene))
                    {
                        EditorUtility.DisplayDialog(
                            "FPS Overlay",
                            $"Could not save {scenePath}. The scene is left open so you can save it manually.",
                            "OK");
                        return;
                    }

                    closeTemporaryScene = true;
                }
            }
        }
        finally
        {
            if (openedTemporarily && closeTemporaryScene && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }
    }

    private static void AddOverlay(Scene scene, bool selectCreatedObject)
    {
        var canvasObject = new GameObject(
            OverlayName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        var backgroundObject = new GameObject(
            "Background",
            typeof(RectTransform),
            typeof(Image));
        backgroundObject.transform.SetParent(canvasObject.transform, false);

        var backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(1f, 1f);
        backgroundRect.anchorMax = new Vector2(1f, 1f);
        backgroundRect.pivot = new Vector2(1f, 1f);
        backgroundRect.anchoredPosition = new Vector2(-32f, -32f);
        backgroundRect.sizeDelta = new Vector2(880f, 360f);

        var background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.65f);
        background.raycastTarget = false;

        var labelObject = new GameObject(
            "FPS Label",
            typeof(RectTransform),
            typeof(Text));
        labelObject.transform.SetParent(backgroundObject.transform, false);

        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.offsetMin = new Vector2(16f, -124f);
        labelRect.offsetMax = new Vector2(-164f, -8f);

        var label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 40;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = "FPS: --";

        var settingsToggleObject = new GameObject(
            "Settings Toggle",
            typeof(RectTransform),
            typeof(Image),
            typeof(Toggle));
        settingsToggleObject.transform.SetParent(backgroundObject.transform, false);

        var settingsToggleRect = settingsToggleObject.GetComponent<RectTransform>();
        settingsToggleRect.anchorMin = new Vector2(1f, 1f);
        settingsToggleRect.anchorMax = new Vector2(1f, 1f);
        settingsToggleRect.pivot = new Vector2(1f, 1f);
        settingsToggleRect.anchoredPosition = new Vector2(-16f, -16f);
        settingsToggleRect.sizeDelta = new Vector2(140f, 64f);

        var settingsToggleImage = settingsToggleObject.GetComponent<Image>();
        settingsToggleImage.color = new Color(0.12f, 0.18f, 0.25f, 0.95f);

        var settingsToggleLabel = CreateText(settingsToggleObject.transform, "SET", 28, TextAnchor.MiddleCenter);
        var settingsToggleLabelRect = settingsToggleLabel.rectTransform;
        settingsToggleLabelRect.anchorMin = Vector2.zero;
        settingsToggleLabelRect.anchorMax = Vector2.one;
        settingsToggleLabelRect.offsetMin = new Vector2(32f, 0f);
        settingsToggleLabelRect.offsetMax = Vector2.zero;

        var checkmarkObject = new GameObject(
            "Checkmark",
            typeof(RectTransform),
            typeof(Image));
        checkmarkObject.transform.SetParent(settingsToggleObject.transform, false);
        var checkmarkRect = checkmarkObject.GetComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0f, 0.5f);
        checkmarkRect.anchorMax = new Vector2(0f, 0.5f);
        checkmarkRect.pivot = new Vector2(0f, 0.5f);
        checkmarkRect.anchoredPosition = new Vector2(12f, 0f);
        checkmarkRect.sizeDelta = new Vector2(20f, 20f);
        var checkmark = checkmarkObject.GetComponent<Image>();
        checkmark.color = Color.white;

        var settingsToggle = settingsToggleObject.GetComponent<Toggle>();
        settingsToggle.targetGraphic = settingsToggleImage;
        settingsToggle.graphic = checkmark;
        settingsToggle.isOn = false;

        var settingsPanelObject = new GameObject(
            "Frame Rate Settings",
            typeof(RectTransform));
        settingsPanelObject.transform.SetParent(backgroundObject.transform, false);
        var settingsPanelRect = settingsPanelObject.GetComponent<RectTransform>();
        settingsPanelRect.anchorMin = Vector2.zero;
        settingsPanelRect.anchorMax = Vector2.one;
        settingsPanelRect.offsetMin = new Vector2(16f, 16f);
        settingsPanelRect.offsetMax = new Vector2(-16f, -144f);

        var targetFrameRateLabel = CreateText(
            settingsPanelObject.transform,
            "Target: 60 FPS",
            32,
            TextAnchor.MiddleLeft);
        var targetFrameRateLabelRect = targetFrameRateLabel.rectTransform;
        targetFrameRateLabelRect.anchorMin = new Vector2(0f, 1f);
        targetFrameRateLabelRect.anchorMax = new Vector2(1f, 1f);
        targetFrameRateLabelRect.pivot = new Vector2(0.5f, 1f);
        targetFrameRateLabelRect.anchoredPosition = Vector2.zero;
        targetFrameRateLabelRect.sizeDelta = new Vector2(0f, 52f);

        var sliderObject = new GameObject(
            "Frame Rate Slider",
            typeof(RectTransform),
            typeof(Slider));
        sliderObject.transform.SetParent(settingsPanelObject.transform, false);
        var sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0.42f);
        sliderRect.anchorMax = new Vector2(1f, 0.42f);
        sliderRect.offsetMin = new Vector2(16f, -12f);
        sliderRect.offsetMax = new Vector2(-16f, 12f);

        var sliderBackground = CreateImage(sliderObject.transform, "Track", new Color(0.45f, 0.5f, 0.55f, 1f));
        sliderBackground.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        sliderBackground.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        sliderBackground.rectTransform.offsetMin = new Vector2(10f, -6f);
        sliderBackground.rectTransform.offsetMax = new Vector2(-10f, 6f);

        var handleAreaObject = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaObject.transform.SetParent(sliderObject.transform, false);
        var handleAreaRect = handleAreaObject.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        var handle = CreateImage(handleAreaObject.transform, "Handle", Color.white);
        var handleRect = handle.rectTransform;
        handleRect.sizeDelta = new Vector2(40f, 56f);
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);

        var frameRateSlider = sliderObject.GetComponent<Slider>();
        frameRateSlider.fillRect = null;
        frameRateSlider.handleRect = handleRect;
        frameRateSlider.targetGraphic = handle;
        frameRateSlider.direction = Slider.Direction.LeftToRight;
        frameRateSlider.minValue = 0f;
        frameRateSlider.maxValue = 3f;
        frameRateSlider.wholeNumbers = true;
        frameRateSlider.value = 1f;

        for (var index = 0; index < SupportedFrameRates.Length; index++)
        {
            var rateLabel = CreateText(
                settingsPanelObject.transform,
                SupportedFrameRates[index].ToString(),
                24,
                TextAnchor.MiddleCenter);
            var rateLabelRect = rateLabel.rectTransform;
            var ratePosition = 0.053f + index * (0.894f / (SupportedFrameRates.Length - 1));
            rateLabelRect.anchorMin = new Vector2(ratePosition, 0f);
            rateLabelRect.anchorMax = new Vector2(ratePosition, 0f);
            rateLabelRect.pivot = new Vector2(0.5f, 0f);
            rateLabelRect.anchoredPosition = new Vector2(0f, -4f);
            rateLabelRect.sizeDelta = new Vector2(72f, 36f);
        }

        settingsPanelObject.SetActive(false);

        var fpsOverlay = canvasObject.AddComponent<FpsOverlay>();
        var serializedOverlay = new SerializedObject(fpsOverlay);
        serializedOverlay.FindProperty("fpsLabel").objectReferenceValue = label;
        serializedOverlay.FindProperty("overlayBackground").objectReferenceValue = backgroundRect;
        serializedOverlay.FindProperty("settingsToggle").objectReferenceValue = settingsToggle;
        serializedOverlay.FindProperty("settingsPanel").objectReferenceValue = settingsPanelObject;
        serializedOverlay.FindProperty("frameRateSlider").objectReferenceValue = frameRateSlider;
        serializedOverlay.FindProperty("targetFrameRateLabel").objectReferenceValue = targetFrameRateLabel;
        serializedOverlay.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(canvasObject, "Add FPS UI");
        EditorSceneManager.MarkSceneDirty(scene);
        if (selectCreatedObject)
            Selection.activeGameObject = canvasObject;
    }

    private static Text CreateText(Transform parent, string text, int fontSize, TextAnchor alignment)
    {
        var textObject = new GameObject(text, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        var label = textObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void RemoveOverlay(FpsOverlay overlay)
    {
        if (overlay == null)
            return;

        var scene = overlay.gameObject.scene;
        Undo.DestroyObjectImmediate(overlay.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
    }
}
