using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class VictorySceneSetup
{
    const string RequestPath =
        "Assets/Editor/CodexVictorySceneSetupRequest.txt";
    const string TargetScenePath = "Assets/Scenes/关卡1.unity";
    const string GateName = "Gate";
    const string VictoryControllerName = "Victory Sequence Controller";
    const string VictoryCanvasName = "Victory Canvas";
    const string VictoryPanelName = "Victory Settlement Panel";
    const string EscapeTriggerName = "Gate Escape Victory Trigger";

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Setup Victory Sequence")]
    public static void SetupFromMenu()
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

        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating)
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
            Debug.LogWarning(
                "Victory setup is waiting for the active scene to be "
                + TargetScenePath
                + ".");
            return false;
        }

        GameObject gateObject = FindSceneObjectByExactName(GateName);
        if (gateObject == null)
        {
            Debug.LogError("Victory setup could not find Gate.");
            return false;
        }

        GateNeedleChallengeController2D gateController =
            GetOrAddComponent<GateNeedleChallengeController2D>(gateObject);
        gateController.hideGateVisualsWhenUnlocked = false;
        Renderer[] gateRenderers =
            gateObject.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer gateRenderer in gateRenderers)
        {
            if (gateRenderer != null)
            {
                gateRenderer.enabled = true;
                EditorUtility.SetDirty(gateRenderer);
            }
        }

        GameObject victoryControllerObject =
            FindSceneObjectByExactName(VictoryControllerName);
        if (victoryControllerObject == null)
        {
            victoryControllerObject = new GameObject(VictoryControllerName);
        }

        VictoryPresentation2D victory =
            GetOrAddComponent<VictoryPresentation2D>(victoryControllerObject);
        VictoryUI ui = EnsureVictoryCanvas();
        victory.victoryRoot = ui.panel;
        victory.cgImage = ui.cgImage;
        victory.victoryTitle = ui.title;
        victory.victorySummary = ui.summary;
        victory.returnToStartButton = ui.returnButton;
        victory.restartButton = ui.restartButton;
        victory.victoryTitleText = "ESCAPED";
        victory.victorySummaryText = "GAME COMPLETE";
        victory.secondsPerSlide = 3f;
        victory.loopCgSlides = false;
        victory.pauseGameOnVictory = true;
        victory.hideGameplayMessageOnVictory = true;
        victory.gameplayMessageUI =
            Object.FindAnyObjectByType<GameplayMessageUI2D>(
                FindObjectsInactive.Include);
        victory.allowKeyboardReturn = false;
        victory.gameplayBehavioursToDisable =
            BuildGameplayBehaviourList(victory, gateController);

        GameObject triggerObject =
            FindSceneObjectByExactName(EscapeTriggerName);
        if (triggerObject == null)
        {
            triggerObject = new GameObject(EscapeTriggerName);
        }

        triggerObject.transform.SetParent(null, true);
        triggerObject.transform.position =
            gateObject.transform.position + new Vector3(0.85f, 0f, 0f);
        triggerObject.transform.rotation = Quaternion.identity;
        triggerObject.transform.localScale = Vector3.one;

        BoxCollider2D escapeCollider =
            GetOrAddComponent<BoxCollider2D>(triggerObject);
        escapeCollider.enabled = true;
        escapeCollider.isTrigger = true;
        escapeCollider.offset = Vector2.zero;
        escapeCollider.size = new Vector2(0.7f, 8.5f);

        GateEscapeVictoryTrigger2D escapeTrigger =
            GetOrAddComponent<GateEscapeVictoryTrigger2D>(triggerObject);
        escapeTrigger.playerObjectName = BubuRunningGame.PlayerRootName;
        escapeTrigger.requireGateOpen = true;
        escapeTrigger.triggerOnlyOnce = true;
        escapeTrigger.gateController = gateController;
        escapeTrigger.victoryPresentation = victory;

        ui.panel.SetActive(false);
        SetDirtyRecursively(ui.canvas.gameObject);
        EditorUtility.SetDirty(gateObject);
        EditorUtility.SetDirty(gateController);
        EditorUtility.SetDirty(victoryControllerObject);
        EditorUtility.SetDirty(victory);
        EditorUtility.SetDirty(triggerObject);
        EditorUtility.SetDirty(escapeCollider);
        EditorUtility.SetDirty(escapeTrigger);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log(
            "Configured persistent Gate art, an interior escape trigger and "
            + "an editable victory CG settlement.");
        return true;
    }

    static MonoBehaviour[] BuildGameplayBehaviourList(
        VictoryPresentation2D victory,
        GateNeedleChallengeController2D gateController)
    {
        List<MonoBehaviour> behaviours = new List<MonoBehaviour>();
        AddIfPresent(
            behaviours,
            Object.FindAnyObjectByType<BubuRunningGame>(
                FindObjectsInactive.Include));
        AddIfPresent(behaviours, gateController);

        foreach (PatrollingSoldier2D soldier in
            Object.FindObjectsByType<PatrollingSoldier2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
        {
            AddIfPresent(behaviours, soldier);
        }

        foreach (SoldierFailureRange2D failureRange in
            Object.FindObjectsByType<SoldierFailureRange2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
        {
            AddIfPresent(behaviours, failureRange);
        }

        foreach (EdgeScrollCamera2D cameraController in
            Object.FindObjectsByType<EdgeScrollCamera2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
        {
            AddIfPresent(behaviours, cameraController);
        }

        behaviours.Remove(victory);
        return behaviours.ToArray();
    }

    static void AddIfPresent(
        List<MonoBehaviour> behaviours,
        MonoBehaviour behaviour)
    {
        if (behaviour != null && !behaviours.Contains(behaviour))
        {
            behaviours.Add(behaviour);
        }
    }

    static VictoryUI EnsureVictoryCanvas()
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        GameObject canvasObject =
            FindSceneObjectByExactName(VictoryCanvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(VictoryCanvasName);
        }

        SetLayer(canvasObject, uiLayer);
        Canvas canvas = GetOrAddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 8000;

        CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        GetOrAddComponent<GraphicRaycaster>(canvasObject);

        GameObject panel =
            GetOrCreateUIChild(canvasObject.transform, VictoryPanelName, uiLayer);
        panel.SetActive(true);
        SetStretch(panel.GetComponent<RectTransform>());
        Image background = GetOrAddComponent<Image>(panel);
        background.color = new Color(0.015f, 0.008f, 0.01f, 1f);
        background.raycastTarget = true;

        GameObject cgObject =
            GetOrCreateUIChild(panel.transform, "Victory CG", uiLayer);
        RectTransform cgRect = cgObject.GetComponent<RectTransform>();
        cgRect.anchorMin = new Vector2(0.08f, 0.15f);
        cgRect.anchorMax = new Vector2(0.92f, 0.9f);
        cgRect.pivot = new Vector2(0.5f, 0.5f);
        cgRect.anchoredPosition = Vector2.zero;
        cgRect.sizeDelta = Vector2.zero;
        Image cgImage = GetOrAddComponent<Image>(cgObject);
        cgImage.sprite = null;
        cgImage.color = Color.white;
        cgImage.preserveAspect = true;
        cgImage.raycastTarget = false;

        Text title =
            EnsureText(
                panel.transform,
                "Victory Title",
            "ESCAPED",
                78,
                new Vector2(0f, 170f),
                new Vector2(1200f, 110f),
                uiLayer);
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(0.94f, 0.77f, 0.48f, 1f);

        Text summary =
            EnsureText(
                panel.transform,
                "Victory Summary",
            "GAME COMPLETE",
                42,
                new Vector2(0f, 70f),
                new Vector2(1000f, 80f),
                uiLayer);
        summary.color = new Color(0.95f, 0.9f, 0.82f, 1f);

        Button returnButton =
            EnsureButton(
                panel.transform,
                "Return To Start Button",
            "RETURN TO START",
                new Vector2(-170f, -330f),
                uiLayer);
        Button restartButton =
            EnsureButton(
                panel.transform,
                "Restart Button",
            "RESTART",
                new Vector2(170f, -330f),
                uiLayer);

        return new VictoryUI
        {
            canvas = canvas,
            panel = panel,
            cgImage = cgImage,
            title = title,
            summary = summary,
            returnButton = returnButton,
            restartButton = restartButton,
        };
    }

    static Text EnsureText(
        Transform parent,
        string name,
        string value,
        int fontSize,
        Vector2 position,
        Vector2 size,
        int layer)
    {
        GameObject textObject = GetOrCreateUIChild(parent, name, layer);
        SetCenteredRect(textObject.GetComponent<RectTransform>(), position, size);
        Text text = GetOrAddComponent<Text>(textObject);
        text.text = value;
        text.font = GetLegacyRuntimeFont();
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    static Button EnsureButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        int layer)
    {
        GameObject buttonObject = GetOrCreateUIChild(parent, name, layer);
        SetCenteredRect(
            buttonObject.GetComponent<RectTransform>(),
            position,
            new Vector2(280f, 72f));
        Image buttonImage = GetOrAddComponent<Image>(buttonObject);
        buttonImage.sprite =
            AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "UI/Skin/UISprite.psd");
        buttonImage.type = Image.Type.Sliced;
        buttonImage.color = new Color(0.42f, 0.055f, 0.045f, 0.96f);
        Button button = GetOrAddComponent<Button>(buttonObject);
        button.targetGraphic = buttonImage;

        Text buttonText =
            EnsureText(
                buttonObject.transform,
                "Label",
                label,
                30,
                Vector2.zero,
                new Vector2(260f, 60f),
                layer);
        buttonText.fontStyle = FontStyle.Bold;
        return button;
    }

    static GameObject GetOrCreateUIChild(
        Transform parent,
        string childName,
        int layer)
    {
        Transform existing = parent.Find(childName);
        GameObject childObject =
            existing != null
                ? existing.gameObject
                : new GameObject(childName, typeof(RectTransform));
        if (existing == null)
        {
            childObject.transform.SetParent(parent, false);
        }

        SetLayer(childObject, layer);
        return childObject;
    }

    static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    static void SetCenteredRect(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    static void SetLayer(GameObject target, int layer)
    {
        if (target != null && layer >= 0)
        {
            target.layer = layer;
        }
    }

    static void SetDirtyRecursively(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component != null)
            {
                EditorUtility.SetDirty(component);
            }
        }
    }

    static Font GetLegacyRuntimeFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null
            ? font
            : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    static GameObject FindSceneObjectByExactName(string objectName)
    {
        GameObject[] sceneObjects =
            Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject != null && sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }

    class VictoryUI
    {
        public Canvas canvas;
        public GameObject panel;
        public Image cgImage;
        public Text title;
        public Text summary;
        public Button returnButton;
        public Button restartButton;
    }
}
