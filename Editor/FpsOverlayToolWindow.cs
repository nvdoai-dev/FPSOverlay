using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class FpsOverlayToolWindow : EditorWindow
{
    private const string OverlayName = "FPS Overlay";

    [MenuItem("Tools/Add FPS Overlay")]
    private static void Open()
    {
        GetWindow<FpsOverlayToolWindow>("FPS Overlay Tool");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("FPS UI", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Add or remove an FPS counter in the currently active scene. Save the scene to keep the change.",
            MessageType.Info);

        var activeScene = SceneManager.GetActiveScene();
        EditorGUILayout.LabelField("Active scene", string.IsNullOrEmpty(activeScene.name) ? "Untitled" : activeScene.name);
        var existingOverlay = FindOverlay(activeScene);

        using (new EditorGUI.DisabledScope(!activeScene.IsValid() || !activeScene.isLoaded || existingOverlay != null))
        {
            if (GUILayout.Button("Add FPS to UI", GUILayout.Height(32f)))
                AddOverlay(activeScene);
        }

        using (new EditorGUI.DisabledScope(existingOverlay == null))
        {
            if (GUILayout.Button("Remove FPS from UI", GUILayout.Height(28f)))
                RemoveOverlay(existingOverlay);
        }

        EditorGUILayout.HelpBox(
            existingOverlay == null ? "FPS overlay is not in this scene." : "FPS overlay is present in this scene.",
            existingOverlay == null ? MessageType.None : MessageType.Info);
    }

    private static FpsOverlay FindOverlay(Scene scene)
    {
        if (!scene.IsValid())
            return null;

        var overlays = Object.FindObjectsByType<FpsOverlay>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (var overlay in overlays)
        {
            if (overlay.gameObject.scene == scene)
                return overlay;
        }

        return null;
    }

    private static void AddOverlay(Scene scene)
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
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
