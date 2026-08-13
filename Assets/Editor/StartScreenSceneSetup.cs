using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StartScreenSceneSetup
{
    const string RequestPath = "Assets/Editor/CodexStartScreenSetupRequest.txt";
    const string TargetScenePath = "Assets/Scenes/关卡1.unity";
    const string CanvasName = "Start Screen Canvas";
    const string ScreenRootName = "Start Screen";
    const string BackgroundName = "Start Background";
    const string ButtonName = "Start Button";
    const string ButtonTextName = "Start Button Text";
    const string EventSystemName = "EventSystem";
    const string StartSpritePath = "Assets/Art/ui/bubu start.jpg";

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Setup Start Screen")]
    public static void SetupFromMenu()
    {
        SetupScene();
    }

    static void ProcessRequestedSetup()
    {
        if (!SetupRequested())
        {
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += ProcessRequestedSetup;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ProcessRequestedSetup;
            return;
        }

        try
        {
            SetupScene();
            DeleteSetupRequest();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.delayCall += ProcessRequestedSetup;
        }
    }

    static void SetupScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != TargetScenePath)
        {
            scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        }

        if (!scene.IsValid())
        {
            Debug.LogWarning("Start screen setup skipped because " + TargetScenePath + " could not be opened.");
            return;
        }

        Sprite startSprite = LoadMainSprite(StartSpritePath);
        GameObject canvasObject = EnsureRootObject(scene, CanvasName);
        Canvas canvas = EnsureCanvas(canvasObject);
        EnsureCanvasScaler(canvasObject);
        EnsureGraphicRaycaster(canvasObject);

        GameObject screenRoot = EnsureChild(canvasObject.transform, ScreenRootName);
        StretchToParent(screenRoot.GetComponent<RectTransform>());

        Image background = EnsureImage(EnsureChild(screenRoot.transform, BackgroundName));
        background.sprite = startSprite;
        background.color = Color.white;
        background.preserveAspect = true;
        StretchToParent(background.rectTransform);

        Button button = EnsureStartButton(screenRoot.transform);
        StartScreenController2D controller = canvasObject.GetComponent<StartScreenController2D>();
        if (controller == null)
        {
            controller = canvasObject.AddComponent<StartScreenController2D>();
        }

        controller.startScreenRoot = screenRoot;
        controller.startButton = button;
        controller.defaultSelected = button;
        controller.showOnAwake = true;
        controller.pauseTimeScaleBeforeStart = true;
        controller.gameplayTimeScale = 1f;
        controller.allowKeyboardStart = true;
        controller.autoDisableGameplayUntilStart = true;
        controller.pauseAudioUntilStart = true;

        UnityEventTools.RemovePersistentListener(button.onClick, controller.StartGame);
        UnityEventTools.AddPersistentListener(button.onClick, controller.StartGame);

        EnsureEventSystem(scene);

        EditorUtility.SetDirty(canvasObject);
        EditorUtility.SetDirty(screenRoot);
        EditorUtility.SetDirty(background);
        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Configured start screen with " + StartSpritePath + ".");
    }

    static Canvas EnsureCanvas(GameObject canvasObject)
    {
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = canvasObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        return canvas;
    }

    static void EnsureCanvasScaler(GameObject canvasObject)
    {
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    static void EnsureGraphicRaycaster(GameObject canvasObject)
    {
        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }
    }

    static Button EnsureStartButton(Transform parent)
    {
        GameObject buttonObject = EnsureChild(parent, ButtonName);
        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.12f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.12f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(300f, 88f);

        Image image = EnsureImage(buttonObject);
        image.color = new Color(0.05f, 0.02f, 0.02f, 0.78f);

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            button = buttonObject.AddComponent<Button>();
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.86f, 0.68f, 1f);
        colors.pressedColor = new Color(0.82f, 0.55f, 0.42f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.targetGraphic = image;

        Text text = EnsureButtonText(buttonObject.transform);
        text.text = "START";
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.9f, 0.78f, 1f);
        text.fontSize = 42;
        text.fontStyle = FontStyle.Bold;

        return button;
    }

    static Text EnsureButtonText(Transform parent)
    {
        GameObject textObject = EnsureChild(parent, ButtonTextName);
        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        StretchToParent(rectTransform);

        Text text = textObject.GetComponent<Text>();
        if (text == null)
        {
            text = textObject.AddComponent<Text>();
        }

        text.raycastTarget = false;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        text.font = font;
        return text;
    }

    static Image EnsureImage(GameObject imageObject)
    {
        Image image = imageObject.GetComponent<Image>();
        if (image == null)
        {
            image = imageObject.AddComponent<Image>();
        }

        return image;
    }

    static GameObject EnsureRootObject(Scene scene, string objectName)
    {
        GameObject existing = FindSceneObjectByExactName(scene, objectName);
        if (existing != null)
        {
            return existing;
        }

        GameObject created = new GameObject(objectName, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    static GameObject EnsureChild(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child;
    }

    static void StretchToParent(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }

    static void EnsureEventSystem(Scene scene)
    {
        GameObject eventSystemObject = FindSceneObjectByExactName(scene, EventSystemName);
        if (eventSystemObject == null)
        {
            eventSystemObject = new GameObject(EventSystemName);
            SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
        }

        if (eventSystemObject.GetComponent<EventSystem>() == null)
        {
            eventSystemObject.AddComponent<EventSystem>();
        }

        if (eventSystemObject.GetComponent<StandaloneInputModule>() == null)
        {
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }
    }

    static Sprite LoadMainSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
        {
            return sprite;
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (Object asset in assets)
        {
            if (asset is Sprite loadedSprite)
            {
                return loadedSprite;
            }
        }

        Debug.LogWarning("Start screen sprite was not found at " + assetPath + ".");
        return null;
    }

    static GameObject FindSceneObjectByExactName(Scene scene, string objectName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == objectName)
            {
                return rootObject;
            }

            Transform[] children = rootObject.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child != null && child.gameObject.name == objectName)
                {
                    return child.gameObject;
                }
            }
        }

        return null;
    }

    static bool SetupRequested()
    {
        return File.Exists(GetProjectRelativeAbsolutePath(RequestPath))
            || AssetDatabase.LoadAssetAtPath<TextAsset>(RequestPath) != null;
    }

    static void DeleteSetupRequest()
    {
        bool deletedByAssetDatabase = AssetDatabase.DeleteAsset(RequestPath);
        if (deletedByAssetDatabase)
        {
            return;
        }

        string requestPath = GetProjectRelativeAbsolutePath(RequestPath);
        if (File.Exists(requestPath))
        {
            File.Delete(requestPath);
        }

        string metaPath = requestPath + ".meta";
        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }
    }

    static string GetProjectRelativeAbsolutePath(string projectRelativePath)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath);
    }
}
