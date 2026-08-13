using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GateNeedleChallengeSceneSetup
{
    const string RequestPath =
        "Assets/Editor/CodexGateNeedleChallengeSetupRequest.txt";
    const string TargetScenePath = "Assets/Scenes/关卡1.unity";
    const string GateName = "Gate";
    const string CanvasName = "Gate Needle Challenge Canvas";
    const string PanelName = "Needle Challenge Panel";
    const string DollSpritePath = "Assets/Art/puzzles/doll.png";
    const string NeedleSpritePath = "Assets/Art/puzzles/yinzhen.png";
    const string DollItemName = "doll";

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Setup Gate Needle Challenge")]
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
                "Gate needle challenge setup is waiting for the active scene to be "
                + TargetScenePath
                + ".");
            return false;
        }

        GameObject gateObject = FindSceneObjectByExactName(GateName);
        if (gateObject == null)
        {
            Debug.LogError("Gate needle challenge setup could not find Gate.");
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

        SpriteRenderer gateRenderer =
            GetOrAddComponent<SpriteRenderer>(gateObject);
        BoxCollider2D gateBlocker = GetOrAddComponent<BoxCollider2D>(gateObject);
        gateBlocker.enabled = true;
        gateBlocker.isTrigger = false;
        if (gateRenderer.sprite != null)
        {
            gateBlocker.size = gateRenderer.sprite.bounds.size;
            gateBlocker.offset = gateRenderer.sprite.bounds.center;
        }

        CanvasReferences ui = EnsureChallengeCanvas();
        Sprite dollSprite = LoadLargestSprite(DollSpritePath);
        Sprite needleSprite = LoadLargestSprite(NeedleSpritePath);

        PrecisionNeedleChallenge2D challenge =
            GetOrAddComponent<PrecisionNeedleChallenge2D>(gateObject);
        challenge.requiredSuccessfulNeedles = 3;
        challenge.baseRotationSpeed = 540f;
        challenge.randomizeStartingAngle = true;
        challenge.resetProgressOnFailure = false;
        challenge.resultPauseDuration = 0.55f;
        challenge.completionDisplayDuration = 0.9f;
        challenge.pauseGameDuringChallenge = true;
        challenge.allowCancel = true;
        if (challenge.rounds == null || challenge.rounds.Length == 0)
        {
            challenge.rounds =
                new[]
                {
                    new PrecisionNeedleChallenge2D.PrecisionRound
                    {
                        targetAngle = 35f,
                        precisionWindow = 14f,
                        speedMultiplier = 1f,
                    },
                    new PrecisionNeedleChallenge2D.PrecisionRound
                    {
                        targetAngle = 155f,
                        precisionWindow = 12f,
                        speedMultiplier = 1.12f,
                    },
                    new PrecisionNeedleChallenge2D.PrecisionRound
                    {
                        targetAngle = 278f,
                        precisionWindow = 10f,
                        speedMultiplier = 1.25f,
                    },
                };
        }

        challenge.challengePanel = ui.panel;
        challenge.pointer = ui.pointer;
        challenge.pointerImage = ui.pointerImage;
        challenge.targetZoneImage = ui.targetZone;
        challenge.dollImage = ui.dollImage;
        challenge.progressFillImage = ui.progressFill;
        challenge.progressText = ui.progressText;
        challenge.statusText = ui.statusText;
        challenge.instructionText = ui.instructionText;
        challenge.placedNeedleImages = ui.placedNeedles;
        challenge.needleSprite = needleSprite;
        challenge.dollSprite = dollSprite;
        challenge.instructionMessage =
            "Press Space to place the needle inside the red target zone.";
        challenge.precisionSuccessMessage = "Perfect hit.";
        challenge.precisionFailureMessage =
            "Missed the target. Calibrate again.";
        challenge.completionMessage =
            "All three needles placed successfully.";

        if (ui.dollImage != null)
        {
            ui.dollImage.sprite = dollSprite;
            ui.dollImage.preserveAspect = true;
        }

        if (ui.pointerImage != null)
        {
            ui.pointerImage.sprite = needleSprite;
            ui.pointerImage.preserveAspect = true;
        }

        GateNeedleChallengeController2D gateController =
            GetOrAddComponent<GateNeedleChallengeController2D>(gateObject);
        gateController.playerObjectName = BubuRunningGame.PlayerRootName;
        gateController.player =
            playerObject != null ? playerObject.transform : null;
        gateController.playerCollider =
            playerObject != null
                ? playerObject.GetComponentInChildren<Collider2D>(true)
                : null;
        gateController.gateBlocker = gateBlocker;
        gateController.gateVisuals =
            gateRenderer != null
                ? new Renderer[] { gateRenderer }
                : Array.Empty<Renderer>();
        gateController.itemInventory = inventory;
        gateController.needleChallenge = challenge;
        gateController.messageUI = messageUI;
        gateController.interactionPadding = 0.8f;
        gateController.fallbackInteractionDistance = 2.6f;
        gateController.requireNeedleChallengeCompletion = true;
        gateController.disableGateBlockerWhenUnlocked = true;
        gateController.hideGateVisualsWhenUnlocked = false;
        gateController.challengeItemRequirements =
            EnsureItemRequirement(
                gateController.challengeItemRequirements,
                DollItemName,
                "Find the doll before starting needle calibration.");
        gateController.gateItemRequirements =
            EnsureItemRequirement(
                gateController.gateItemRequirements,
                DollItemName,
                "The doll is required to open the gate.");
        gateController.approachMessage =
            "Press P to use the doll for needle calibration.";
        gateController.missingChallengeItemMessage =
            "Required ritual items are missing.";
        gateController.pendingRequirementMessage =
            "Calibration complete, but another gate requirement is still missing.";
        gateController.gateOpenedMessage = "The gate is open.";

        ui.panel.SetActive(false);
        SetDirtyRecursively(ui.canvas.gameObject);
        EditorUtility.SetDirty(gateObject);
        EditorUtility.SetDirty(gateRenderer);
        EditorUtility.SetDirty(gateBlocker);
        EditorUtility.SetDirty(challenge);
        EditorUtility.SetDirty(gateController);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log(
            "Configured Gate as a three-hit precision needle challenge with "
            + "editable item and external unlock requirements.");
        return true;
    }

    static GateNeedleChallengeController2D.ItemRequirement[]
        EnsureItemRequirement(
            GateNeedleChallengeController2D.ItemRequirement[] requirements,
            string itemName,
            string missingMessage)
    {
        List<GateNeedleChallengeController2D.ItemRequirement> items =
            requirements == null
                ? new List<GateNeedleChallengeController2D.ItemRequirement>()
                : requirements.Where(item => item != null).ToList();

        GateNeedleChallengeController2D.ItemRequirement existing =
            items.FirstOrDefault(
                item =>
                    string.Equals(
                        item.itemName,
                        itemName,
                        StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            existing = new GateNeedleChallengeController2D.ItemRequirement();
            items.Add(existing);
        }

        existing.itemName = itemName;
        existing.required = true;
        if (string.IsNullOrWhiteSpace(existing.missingMessage))
        {
            existing.missingMessage = missingMessage;
        }

        return items.ToArray();
    }

    static CanvasReferences EnsureChallengeCanvas()
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        GameObject canvasObject = FindSceneObjectByExactName(CanvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(CanvasName);
        }

        SetLayer(canvasObject, uiLayer);
        Canvas canvas = GetOrAddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 7000;

        CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        GetOrAddComponent<GraphicRaycaster>(canvasObject);

        GameObject panel = GetOrCreateUIChild(canvasObject.transform, PanelName, uiLayer);
        panel.SetActive(true);
        SetStretch(panel.GetComponent<RectTransform>());
        Image overlay = GetOrAddComponent<Image>(panel);
        overlay.color = new Color(0.025f, 0.01f, 0.015f, 0.9f);
        overlay.raycastTarget = true;

        GameObject frame = GetOrCreateUIChild(panel.transform, "Challenge Frame", uiLayer);
        SetCenteredRect(
            frame.GetComponent<RectTransform>(),
            Vector2.zero,
            new Vector2(760f, 920f));
        Image frameImage = GetOrAddComponent<Image>(frame);
        frameImage.sprite = GetBuiltinUISprite("UI/Skin/Background.psd");
        frameImage.type = Image.Type.Sliced;
        frameImage.color = new Color(0.19f, 0.035f, 0.035f, 0.98f);

        Text title =
            EnsureText(
                frame.transform,
                "Title",
            "NEEDLE CALIBRATION",
                46,
                new Vector2(0f, 390f),
                new Vector2(650f, 70f),
                uiLayer);
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(1f, 0.83f, 0.56f, 1f);

        GameObject dial =
            GetOrCreateUIChild(frame.transform, "Precision Dial", uiLayer);
        SetCenteredRect(
            dial.GetComponent<RectTransform>(),
            new Vector2(0f, 95f),
            new Vector2(470f, 470f));
        Image dialImage = GetOrAddComponent<Image>(dial);
        dialImage.sprite = GetBuiltinUISprite("UI/Skin/Knob.psd");
        dialImage.color = new Color(0.07f, 0.055f, 0.06f, 1f);
        dialImage.preserveAspect = true;

        GameObject targetObject =
            GetOrCreateUIChild(dial.transform, "Precision Target Zone", uiLayer);
        SetStretch(targetObject.GetComponent<RectTransform>());
        Image targetZone = GetOrAddComponent<Image>(targetObject);
        targetZone.sprite = GetBuiltinUISprite("UI/Skin/Knob.psd");
        targetZone.color = new Color(0.9f, 0.09f, 0.06f, 0.92f);
        targetZone.raycastTarget = false;
        targetZone.type = Image.Type.Filled;
        targetZone.fillMethod = Image.FillMethod.Radial360;
        targetZone.fillOrigin = (int)Image.Origin360.Top;
        targetZone.fillClockwise = true;

        GameObject dollObject =
            GetOrCreateUIChild(dial.transform, "Challenge Doll", uiLayer);
        SetCenteredRect(
            dollObject.GetComponent<RectTransform>(),
            new Vector2(0f, -10f),
            new Vector2(210f, 275f));
        Image dollImage = GetOrAddComponent<Image>(dollObject);
        dollImage.color = Color.white;
        dollImage.preserveAspect = true;
        dollImage.raycastTarget = false;

        GameObject pointerObject =
            GetOrCreateUIChild(dial.transform, "Needle Pointer", uiLayer);
        RectTransform pointer = pointerObject.GetComponent<RectTransform>();
        pointer.anchorMin = new Vector2(0.5f, 0.5f);
        pointer.anchorMax = new Vector2(0.5f, 0.5f);
        pointer.pivot = new Vector2(0.5f, 0f);
        pointer.anchoredPosition = Vector2.zero;
        pointer.sizeDelta = new Vector2(12f, 220f);
        Image pointerImage = GetOrAddComponent<Image>(pointerObject);
        pointerImage.color = new Color(0.88f, 0.9f, 0.94f, 1f);
        pointerImage.raycastTarget = false;

        Image[] placedNeedles = new Image[3];
        for (int index = 0; index < placedNeedles.Length; index++)
        {
            GameObject needleObject =
                GetOrCreateUIChild(
                    frame.transform,
                    "Placed Needle " + (index + 1),
                    uiLayer);
            SetCenteredRect(
                needleObject.GetComponent<RectTransform>(),
                new Vector2((index - 1) * 74f, -188f),
                new Vector2(18f, 90f));
            Image needleImage = GetOrAddComponent<Image>(needleObject);
            needleImage.color = new Color(0.9f, 0.92f, 0.96f, 1f);
            needleImage.raycastTarget = false;
            needleObject.SetActive(false);
            placedNeedles[index] = needleImage;
        }

        GameObject progressBackground =
            GetOrCreateUIChild(frame.transform, "Progress Background", uiLayer);
        SetCenteredRect(
            progressBackground.GetComponent<RectTransform>(),
            new Vector2(0f, -282f),
            new Vector2(520f, 34f));
        Image progressBackgroundImage =
            GetOrAddComponent<Image>(progressBackground);
        progressBackgroundImage.color =
            new Color(0.055f, 0.035f, 0.04f, 1f);

        GameObject progressFillObject =
            GetOrCreateUIChild(progressBackground.transform, "Progress Fill", uiLayer);
        SetStretch(progressFillObject.GetComponent<RectTransform>());
        Image progressFill = GetOrAddComponent<Image>(progressFillObject);
        progressFill.color = new Color(0.82f, 0.08f, 0.06f, 1f);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;

        Text progressText =
            EnsureText(
                frame.transform,
                "Progress Text",
                "0 / 3",
                28,
                new Vector2(0f, -282f),
                new Vector2(520f, 48f),
                uiLayer);
        progressText.fontStyle = FontStyle.Bold;

        Text statusText =
            EnsureText(
                frame.transform,
                "Status Text",
            "READY",
                34,
                new Vector2(0f, -342f),
                new Vector2(650f, 54f),
                uiLayer);
        statusText.color = new Color(1f, 0.78f, 0.48f, 1f);

        Text instructionText =
            EnsureText(
                frame.transform,
                "Instruction Text",
            "Press Space to place the needle inside the red target zone.",
                26,
                new Vector2(0f, -400f),
                new Vector2(650f, 70f),
                uiLayer);
        instructionText.color = new Color(0.92f, 0.88f, 0.84f, 1f);

        return new CanvasReferences
        {
            canvas = canvas,
            panel = panel,
            pointer = pointer,
            pointerImage = pointerImage,
            targetZone = targetZone,
            dollImage = dollImage,
            progressFill = progressFill,
            progressText = progressText,
            statusText = statusText,
            instructionText = instructionText,
            placedNeedles = placedNeedles,
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

    static Sprite GetBuiltinUISprite(string path)
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
    }

    static Sprite LoadLargestSprite(string assetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<Sprite>()
            .OrderByDescending(
                candidate => candidate.rect.width * candidate.rect.height)
            .FirstOrDefault();
    }

    static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
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
        public RectTransform pointer;
        public Image pointerImage;
        public Image targetZone;
        public Image dollImage;
        public Image progressFill;
        public Text progressText;
        public Text statusText;
        public Text instructionText;
        public Image[] placedNeedles;
    }
}
