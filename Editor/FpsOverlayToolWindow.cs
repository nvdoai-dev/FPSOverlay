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
    private readonly Dictionary<string, bool> _sceneOverlayStatus = new Dictionary<string, bool>();
    private Vector2 _sceneListScrollPosition;

    [MenuItem("Tools/Add FPS Overlay")]
    private static void Open()
    {
        GetWindow<FpsOverlayToolWindow>("FPS Overlay Tool");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("FPS UI", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The FPS overlay is stored in each scene separately. Check Build Settings scenes, then add or remove the overlay per scene.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Scenes in Build Settings", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh status", GUILayout.Width(110f)))
                RefreshSceneStatuses();
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
                    GUILayout.Width(85f));

                if (GUILayout.Button(hasOverlay ? "Remove FPS" : "Add FPS", GUILayout.Width(90f)))
                {
                    SetOverlayInBuildScene(buildScene.path, !hasOverlay);
                    _sceneOverlayStatus[buildScene.path] = !hasOverlay;
                }
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.HelpBox(
            "Changes to scenes already open are marked dirty; save those scenes in the editor. Closed scenes are saved by this tool.",
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
                    _sceneOverlayStatus[buildScene.path] = FindOverlay(scene) != null;
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

    private static void SetOverlayInBuildScene(string scenePath, bool addOverlay)
    {
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
            typeof(CanvasScaler));
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
        backgroundRect.anchoredPosition = new Vector2(-16f, -16f);
        backgroundRect.sizeDelta = new Vector2(136f, 48f);

        var background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.65f);
        background.raycastTarget = false;

        var labelObject = new GameObject(
            "FPS Label",
            typeof(RectTransform),
            typeof(Text));
        labelObject.transform.SetParent(backgroundObject.transform, false);

        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(4f, 2f);
        labelRect.offsetMax = new Vector2(-4f, -2f);

        var label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 24;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = "FPS: --";

        var fpsOverlay = canvasObject.AddComponent<FpsOverlay>();
        var serializedOverlay = new SerializedObject(fpsOverlay);
        serializedOverlay.FindProperty("fpsLabel").objectReferenceValue = label;
        serializedOverlay.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(canvasObject, "Add FPS UI");
        EditorSceneManager.MarkSceneDirty(scene);
        if (selectCreatedObject)
            Selection.activeGameObject = canvasObject;
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
