using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class IncenseShoeCutSceneSetup
{
    const string RequestPath =
        "Assets/Editor/CodexIncenseShoeCutSetupRequest.txt";
    const string TargetScenePath = "Assets/Scenes/关卡1.unity";
    const string IncenseName = "xianglu";
    const string CanvasName = "Incense Shoe Cut Canvas";
    const string PanelName = "Shoe Cut Challenge Panel";
    const string ShoeSpritePath = "Assets/Art/puzzles/xiuhuaxie.png";
    const string ScissorsSpritePath = "Assets/Art/puzzles/scissors.png";
    const string KeySpritePath = "Assets/Art/puzzles/key.png";
    const string WeddingPaperSpritePath = "Assets/Art/ui/wedding paper.png";

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Setup Incense Shoe Cut")]
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
                "Incense shoe cut setup is waiting for the active scene to be "
                + TargetScenePath
                + ".");
            return false;
        }

        GameObject incenseObject =
            FindSceneObjectByExactName(IncenseName);
        if (incenseObject == null)
        {
            Debug.LogError(
                "Incense shoe cut setup could not find xianglu.");
            return false;
        }

        GameObject playerObject =
            FindSceneObjectByExactName(BubuRunningGame.PlayerRootName);
        PenzaiSearchController2D inventory =
            UnityEngine.Object.FindAnyObjectByType<PenzaiSearchController2D>(
                FindObjectsInactive.Include);
        GameplayMessageUI2D messageUI =
            UnityEngine.Object.FindAnyObjectByType<GameplayMessageUI2D>(
                FindObjectsInactive.Include);
        if (playerObject == null || inventory == null)
        {
            Debug.LogError(
                "Incense shoe cut setup requires Bubu Player and the item inventory.");
            return false;
        }

        Sprite shoeSprite = LoadLargestSprite(ShoeSpritePath);
        Sprite scissorsSprite = LoadLargestSprite(ScissorsSpritePath);
        Sprite keySprite = LoadLargestSprite(KeySpritePath);
        Sprite weddingPaperSprite =
            LoadLargestSprite(WeddingPaperSpritePath);
        CanvasReferences ui =
            EnsureChallengeCanvas(
                shoeSprite,
                scissorsSprite,
                weddingPaperSprite);

        ShoeCutMiniGame2D miniGame =
            GetOrAddComponent<ShoeCutMiniGame2D>(ui.canvas.gameObject);
        IncenseShoeCutInteraction2D interaction =
            GetOrAddComponent<IncenseShoeCutInteraction2D>(incenseObject);

        interaction.playerObjectName = BubuRunningGame.PlayerRootName;
        interaction.player = playerObject.transform;
        interaction.inventory = inventory;
        interaction.messageUI = messageUI;
        interaction.miniGame = miniGame;
        interaction.interactionCenterOffset = Vector2.zero;
        interaction.interactionRadius = 2.2f;
        interaction.shoeItemName = "xiuhuaxie";
        interaction.scissorsItemName = "scissors";
        interaction.rewardItemName = "key";
        interaction.readyMessage =
            "Press P at the incense burner to cut open the embroidered shoe.";
        interaction.missingBothMessage =
            "You need both the embroidered shoe and the scissors.";
        interaction.missingShoeMessage =
            "The embroidered shoe is missing.";
        interaction.missingScissorsMessage =
            "The scissors are missing.";
        interaction.alreadyCompletedMessage =
            "The embroidered shoe has already been cut open.";

        miniGame.challengePanel = ui.panel;
        miniGame.scissorsRect = ui.scissorsRect;
        miniGame.scissorsTip = ui.scissorsTip;
        miniGame.cutCheckpoints = ui.cutCheckpoints;
        miniGame.checkpointImages = ui.checkpointImages;
        miniGame.progressFill = ui.progressFill;
        miniGame.progressText = ui.progressText;
        miniGame.statusText = ui.statusText;
        miniGame.titleText = ui.titleText;
        miniGame.instructionText = ui.instructionText;
        miniGame.closeButtonText = ui.closeButtonText;
        miniGame.closeButton = ui.closeButton;
        miniGame.scissorsDrag = ui.scissorsDrag;
        miniGame.inventory = inventory;
        miniGame.interactionOwner = interaction;
        miniGame.checkpointRadius = 48f;
        miniGame.completionPauseDuration = 0.65f;
        miniGame.pauseGameDuringChallenge = true;
        miniGame.allowCancel = true;
        miniGame.rewardItemName = "key";
        miniGame.rewardIcon = keySprite;
        miniGame.rewardMessage =
            "You found a key inside the embroidered shoe.";
        miniGame.instructionMessage =
            "Drag the scissors through each marker on the embroidered shoe.";
        miniGame.cuttingMessage = "Keep cutting along the markers.";
        miniGame.completionMessage =
            "The embroidered shoe has been cut open.";

        ui.scissorsDrag.draggableRect = ui.scissorsRect;
        ui.scissorsDrag.dragArea = ui.frame;
        ui.scissorsDrag.miniGame = miniGame;
        ui.scissorsDrag.clampInsideDragArea = true;

        ui.panel.SetActive(false);
        SetDirtyRecursively(ui.canvas.gameObject);
        EditorUtility.SetDirty(incenseObject);
        EditorUtility.SetDirty(interaction);
        EditorUtility.SetDirty(miniGame);
        EditorUtility.SetDirty(ui.scissorsDrag);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log(
            "Configured xianglu to require xiuhuaxie and scissors, "
            + "open a mouse-drag cutting challenge, and grant the key.");
        return true;
    }

    static CanvasReferences EnsureChallengeCanvas(
        Sprite shoeSprite,
        Sprite scissorsSprite,
        Sprite weddingPaperSprite)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        GameObject canvasObject =
            FindSceneObjectByExactName(CanvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(CanvasName);
        }

        SetLayer(canvasObject, uiLayer);
        Canvas canvas = GetOrAddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 7100;
        CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        GetOrAddComponent<GraphicRaycaster>(canvasObject);

        GameObject panel =
            GetOrCreateUIChild(canvasObject.transform, PanelName, uiLayer);
        panel.SetActive(true);
        SetStretch(panel.GetComponent<RectTransform>());
        Image overlay = GetOrAddComponent<Image>(panel);
        overlay.color = new Color(0.025f, 0.01f, 0.012f, 0.92f);
        overlay.raycastTarget = true;

        GameObject frameObject =
            GetOrCreateUIChild(panel.transform, "Cutting Frame", uiLayer);
        RectTransform frame = frameObject.GetComponent<RectTransform>();
        SetCenteredRect(frame, Vector2.zero, new Vector2(1080f, 720f));
        Image frameImage = GetOrAddComponent<Image>(frameObject);
        frameImage.sprite =
            weddingPaperSprite != null
                ? weddingPaperSprite
                : GetBuiltinUISprite("UI/Skin/Background.psd");
        frameImage.color = Color.white;
        frameImage.preserveAspect = false;
        frameImage.type =
            weddingPaperSprite != null
                ? Image.Type.Simple
                : Image.Type.Sliced;
        frameImage.raycastTarget = true;

        Text titleText =
            EnsureText(
                frame,
                "Title",
            "CUT OPEN THE EMBROIDERED SHOE",
                45,
                new Vector2(0f, 304f),
                new Vector2(780f, 62f),
                uiLayer);
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.33f, 0.045f, 0.035f, 1f);

        Text instructionText =
            EnsureText(
                frame,
                "Instruction",
            "Drag the scissors through each marker on the embroidered shoe.",
                26,
                new Vector2(0f, 252f),
                new Vector2(850f, 48f),
                uiLayer);
        instructionText.color = new Color(0.28f, 0.09f, 0.06f, 1f);

        GameObject shoeObject =
            GetOrCreateUIChild(frame, "Embroidered Shoe", uiLayer);
        SetCenteredRect(
            shoeObject.GetComponent<RectTransform>(),
            new Vector2(80f, 35f),
            new Vector2(610f, 380f));
        Image shoeImage = GetOrAddComponent<Image>(shoeObject);
        shoeImage.sprite = shoeSprite;
        shoeImage.preserveAspect = true;
        shoeImage.color = Color.white;
        shoeImage.raycastTarget = false;

        GameObject cutGuideObject =
            GetOrCreateUIChild(frame, "Cut Guide", uiLayer);
        SetCenteredRect(
            cutGuideObject.GetComponent<RectTransform>(),
            new Vector2(80f, 38f),
            new Vector2(590f, 7f));
        Image cutGuide = GetOrAddComponent<Image>(cutGuideObject);
        cutGuide.color = new Color(0.5f, 0.02f, 0.015f, 0.5f);
        cutGuide.raycastTarget = false;

        float[] checkpointXs = { -155f, -38f, 80f, 198f, 315f };
        RectTransform[] checkpoints =
            new RectTransform[checkpointXs.Length];
        Image[] checkpointImages =
            new Image[checkpointXs.Length];
        for (int index = 0; index < checkpointXs.Length; index++)
        {
            GameObject checkpointObject =
                GetOrCreateUIChild(
                    frame,
                    "Cut Checkpoint " + (index + 1),
                    uiLayer);
            RectTransform checkpoint =
                checkpointObject.GetComponent<RectTransform>();
            SetCenteredRect(
                checkpoint,
                new Vector2(checkpointXs[index], 38f),
                new Vector2(34f, 34f));
            Image checkpointImage =
                GetOrAddComponent<Image>(checkpointObject);
            checkpointImage.sprite =
                GetBuiltinUISprite("UI/Skin/Knob.psd");
            checkpointImage.color =
                new Color(0.72f, 0.12f, 0.08f, 0.85f);
            checkpointImage.preserveAspect = true;
            checkpointImage.raycastTarget = false;
            checkpoints[index] = checkpoint;
            checkpointImages[index] = checkpointImage;
        }

        GameObject progressBackground =
            GetOrCreateUIChild(frame, "Progress Background", uiLayer);
        SetCenteredRect(
            progressBackground.GetComponent<RectTransform>(),
            new Vector2(80f, -218f),
            new Vector2(560f, 32f));
        Image progressBackgroundImage =
            GetOrAddComponent<Image>(progressBackground);
        progressBackgroundImage.color =
            new Color(0.24f, 0.08f, 0.06f, 0.9f);
        progressBackgroundImage.raycastTarget = false;

        GameObject progressFillObject =
            GetOrCreateUIChild(
                progressBackground.transform,
                "Progress Fill",
                uiLayer);
        SetStretch(progressFillObject.GetComponent<RectTransform>());
        Image progressFill =
            GetOrAddComponent<Image>(progressFillObject);
        progressFill.color =
            new Color(0.78f, 0.12f, 0.065f, 1f);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;
        progressFill.raycastTarget = false;

        Text progressText =
            EnsureText(
                frame,
                "Progress Text",
                "0 / 5",
                25,
                new Vector2(80f, -218f),
                new Vector2(560f, 42f),
                uiLayer);
        progressText.fontStyle = FontStyle.Bold;

        Text statusText =
            EnsureText(
                frame,
                "Status Text",
            "READY TO CUT",
                30,
                new Vector2(80f, -275f),
                new Vector2(700f, 50f),
                uiLayer);
        statusText.color = new Color(0.4f, 0.055f, 0.035f, 1f);
        statusText.fontStyle = FontStyle.Bold;

        GameObject scissorsObject =
            GetOrCreateUIChild(frame, "Draggable Scissors", uiLayer);
        RectTransform scissorsRect =
            scissorsObject.GetComponent<RectTransform>();
        SetCenteredRect(
            scissorsRect,
            new Vector2(-395f, -15f),
            new Vector2(190f, 260f));
        Image scissorsImage = GetOrAddComponent<Image>(scissorsObject);
        scissorsImage.sprite = scissorsSprite;
        scissorsImage.preserveAspect = true;
        scissorsImage.color = Color.white;
        scissorsImage.raycastTarget = true;
        DraggableScissorsUI2D scissorsDrag =
            GetOrAddComponent<DraggableScissorsUI2D>(scissorsObject);

        GameObject scissorsTipObject =
            GetOrCreateUIChild(
                scissorsObject.transform,
                "Scissors Tip",
                uiLayer);
        RectTransform scissorsTip =
            scissorsTipObject.GetComponent<RectTransform>();
        SetCenteredRect(
            scissorsTip,
            new Vector2(0f, 112f),
            new Vector2(12f, 12f));

        GameObject closeObject =
            GetOrCreateUIChild(frame, "Close Button", uiLayer);
        SetCenteredRect(
            closeObject.GetComponent<RectTransform>(),
            new Vector2(478f, 316f),
            new Vector2(92f, 52f));
        Image closeImage = GetOrAddComponent<Image>(closeObject);
        closeImage.sprite = GetBuiltinUISprite("UI/Skin/UISprite.psd");
        closeImage.type = Image.Type.Sliced;
        closeImage.color = new Color(0.45f, 0.055f, 0.04f, 0.95f);
        closeImage.raycastTarget = true;
        Button closeButton = GetOrAddComponent<Button>(closeObject);
        closeButton.targetGraphic = closeImage;
        Text closeButtonText =
            EnsureText(
                closeObject.transform,
                "Label",
            "CLOSE",
                24,
                Vector2.zero,
                new Vector2(88f, 48f),
                uiLayer);
        closeButtonText.color = Color.white;

        return new CanvasReferences
        {
            canvas = canvas,
            panel = panel,
            frame = frame,
            scissorsRect = scissorsRect,
            scissorsTip = scissorsTip,
            cutCheckpoints = checkpoints,
            checkpointImages = checkpointImages,
            progressFill = progressFill,
            progressText = progressText,
            statusText = statusText,
            titleText = titleText,
            instructionText = instructionText,
            closeButtonText = closeButtonText,
            closeButton = closeButton,
            scissorsDrag = scissorsDrag,
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
        GameObject textObject =
            GetOrCreateUIChild(parent, name, layer);
        SetCenteredRect(
            textObject.GetComponent<RectTransform>(),
            position,
            size);
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
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
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

        foreach (Component component in
                 root.GetComponentsInChildren<Component>(true))
        {
            if (component != null)
            {
                EditorUtility.SetDirty(component);
            }
        }
    }

    static Font GetLegacyRuntimeFont()
    {
        Font font =
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null
            ? font
            : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    static Sprite GetBuiltinUISprite(string path)
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
    }

    static Sprite LoadLargestSprite(string assetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<Sprite>()
            .OrderByDescending(
                candidate =>
                    candidate.rect.width * candidate.rect.height)
            .FirstOrDefault();
    }

    static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null
            ? component
            : target.AddComponent<T>();
    }

    static GameObject FindSceneObjectByExactName(string objectName)
    {
        GameObject[] sceneObjects =
            UnityEngine.Object.FindObjectsByType<GameObject>(
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

    class CanvasReferences
    {
        public Canvas canvas;
        public GameObject panel;
        public RectTransform frame;
        public RectTransform scissorsRect;
        public RectTransform scissorsTip;
        public RectTransform[] cutCheckpoints;
        public Image[] checkpointImages;
        public Image progressFill;
        public Text progressText;
        public Text statusText;
        public Text titleText;
        public Text instructionText;
        public Text closeButtonText;
        public Button closeButton;
        public DraggableScissorsUI2D scissorsDrag;
    }
}
