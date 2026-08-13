using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameplayMessageSceneSetup
{
    const string RequestPath = "Assets/Editor/CodexGameplayMessageSetupRequest.txt";
    const string TargetScenePath = "Assets/Scenes/关卡1.unity";
    const string CanvasName = "Gameplay Message Canvas";
    const string PanelName = "Gameplay Message Panel";
    const string IconName = "Item Icon";
    const string TextName = "Message Text";
    const string ExtraContentName = "Extra UI Content";
    const string WeddingPaperPath = "Assets/Art/ui/wedding paper.png";

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Setup Gameplay Messages")]
    public static void SetupGameplayMessagesFromMenu()
    {
        SetupActiveScene();
    }

    static void ProcessRequestedSetup()
    {
        if (AssetDatabase.LoadAssetAtPath<TextAsset>(RequestPath) == null)
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

        if (!SetupActiveScene())
        {
            return;
        }

        AssetDatabase.DeleteAsset(RequestPath);
        AssetDatabase.SaveAssets();
    }

    static bool SetupActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.path != TargetScenePath)
        {
            Debug.LogWarning("Gameplay message setup is waiting for the active scene to be " + TargetScenePath + ".");
            return false;
        }

        int uiLayer = LayerMask.NameToLayer("UI");
        GameObject canvasObject = FindSceneObjectByExactName(CanvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(CanvasName);
        }

        if (uiLayer >= 0)
        {
            canvasObject.layer = uiLayer;
        }

        Canvas canvas = GetOrAddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;

        CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GetOrAddComponent<GraphicRaycaster>(canvasObject);
        GameplayMessageUI2D messageUI = GetOrAddComponent<GameplayMessageUI2D>(canvasObject);

        GameObject panelObject = GetOrCreateUIChild(canvasObject.transform, PanelName, uiLayer);
        panelObject.SetActive(true);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -36f);
        panelRect.sizeDelta = new Vector2(1100f, 370f);

        Image panelImage = GetOrAddComponent<Image>(panelObject);
        panelImage.sprite = LoadLargestSprite(WeddingPaperPath);
        panelImage.type = Image.Type.Simple;
        panelImage.preserveAspect = true;
        panelImage.color = Color.white;
        panelImage.raycastTarget = false;

        GameObject iconObject = GetOrCreateUIChild(panelObject.transform, IconName, uiLayer);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -65f);
        iconRect.sizeDelta = new Vector2(100f, 100f);

        Image itemIcon = GetOrAddComponent<Image>(iconObject);
        itemIcon.sprite = null;
        itemIcon.preserveAspect = true;
        itemIcon.raycastTarget = false;

        GameObject textObject = GetOrCreateUIChild(panelObject.transform, TextName, uiLayer);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -130f);
        textRect.sizeDelta = new Vector2(-240f, 104f);

        Text messageText = GetOrAddComponent<Text>(textObject);
        messageText.text = "NOTICE";
        messageText.font = GetLegacyRuntimeFont();
        messageText.fontSize = 36;
        messageText.fontStyle = FontStyle.Normal;
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.color = new Color(0.25f, 0.055f, 0.025f, 1f);
        messageText.raycastTarget = false;
        messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
        messageText.verticalOverflow = VerticalWrapMode.Truncate;
        messageText.resizeTextForBestFit = true;
        messageText.resizeTextMinSize = 18;
        messageText.resizeTextMaxSize = 36;
        messageText.lineSpacing = 0.9f;

        GameObject extraContentObject = GetOrCreateUIChild(panelObject.transform, ExtraContentName, uiLayer);
        RectTransform extraContentRect = extraContentObject.GetComponent<RectTransform>();
        extraContentRect.anchorMin = Vector2.zero;
        extraContentRect.anchorMax = new Vector2(1f, 0f);
        extraContentRect.pivot = new Vector2(0.5f, 0f);
        extraContentRect.anchoredPosition = new Vector2(0f, 60f);
        extraContentRect.sizeDelta = new Vector2(-56f, 52f);

        messageUI.messagePanel = panelObject;
        messageUI.messageText = messageText;
        messageUI.itemIcon = itemIcon;
        messageUI.extraUIContent = extraContentRect;
        messageUI.resizePanelForItemIcon = true;
        messageUI.panelHeightWithoutIcon = 370f;
        messageUI.panelHeightWithIcon = 370f;
        messageUI.textPositionWithoutIcon = new Vector2(0f, -130f);
        messageUI.textPositionWithIcon = new Vector2(0f, -178f);
        messageUI.autoWrapMessageText = true;
        messageUI.messageTextHorizontalInset = 240f;
        messageUI.messageTextHeight = 104f;
        messageUI.minimumMessageFontSize = 18;
        messageUI.maximumMessageFontSize = 36;
        messageUI.messageLineSpacing = 0.9f;
        messageUI.keyFoundMessage = "You found the key.";
        messageUI.nothingFoundMessage = "Nothing was found.";
        messageUI.missingKeyMessage =
            "You cannot open this door without the key.";
        messageUI.displayDuration = 2.5f;
        messageUI.useUnscaledTime = true;
        messageUI.hideOnAwake = true;
        messageUI.useSystemChineseFontFallback = true;

        PenzaiSearchController2D searchController =
            Object.FindAnyObjectByType<PenzaiSearchController2D>(FindObjectsInactive.Include);
        if (searchController != null)
        {
            searchController.messageUI = messageUI;
            EditorUtility.SetDirty(searchController);
        }

        DoorMazePuzzleTrigger2D doorTrigger =
            Object.FindAnyObjectByType<DoorMazePuzzleTrigger2D>(FindObjectsInactive.Include);
        if (doorTrigger != null)
        {
            doorTrigger.messageUI = messageUI;
            EditorUtility.SetDirty(doorTrigger);
        }

        iconObject.SetActive(false);
        panelObject.SetActive(false);
        EditorUtility.SetDirty(canvasObject);
        EditorUtility.SetDirty(canvas);
        EditorUtility.SetDirty(scaler);
        EditorUtility.SetDirty(messageUI);
        EditorUtility.SetDirty(panelObject);
        EditorUtility.SetDirty(panelImage);
        EditorUtility.SetDirty(itemIcon);
        EditorUtility.SetDirty(messageText);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log("Configured editable gameplay message UI and connected search and door messages.");
        return true;
    }

    static GameObject GetOrCreateUIChild(Transform parent, string childName, int layer)
    {
        Transform existing = parent.Find(childName);
        GameObject childObject;
        if (existing != null)
        {
            childObject = existing.gameObject;
            if (childObject.GetComponent<RectTransform>() == null)
            {
                Debug.LogError(childName + " exists but does not have a RectTransform.");
                return childObject;
            }
        }
        else
        {
            childObject = new GameObject(childName, typeof(RectTransform));
            childObject.transform.SetParent(parent, false);
        }

        if (layer >= 0)
        {
            childObject.layer = layer;
        }

        return childObject;
    }

    static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    static Font GetLegacyRuntimeFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    static Sprite LoadLargestSprite(string assetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<Sprite>()
            .OrderByDescending(candidate => candidate.rect.width * candidate.rect.height)
            .FirstOrDefault();
    }

    static GameObject FindSceneObjectByExactName(string objectName)
    {
        GameObject[] sceneObjects =
            Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject != null && sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }
}
