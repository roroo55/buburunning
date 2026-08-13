using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public static class CalibrationCaughtSceneSetup
{
    const string RequestPath =
        "Assets/Editor/CodexCalibrationCaughtSetupRequest.txt";
    const string TargetScenePath = "Assets/Scenes/关卡1.unity";
    const string CaughtCgPath = "Assets/Art/cg/beizhua cg.mp4";
    const string PresentationName =
        "Calibration Caught Presentation";
    const string CanvasName = "Calibration Caught Canvas";
    const string DisplayName = "Beizhua CG Display";

    [InitializeOnLoadMethod]
    static void RunAfterReload()
    {
        EditorApplication.delayCall += ProcessRequest;
    }

    [MenuItem("Bubu Running/Setup Calibration Caught Failure")]
    public static void SetupFromMenu()
    {
        SetupActiveScene();
    }

    static void ProcessRequest()
    {
        if (AssetDatabase.LoadAssetAtPath<TextAsset>(RequestPath) == null)
        {
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += ProcessRequest;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ProcessRequest;
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
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != TargetScenePath)
        {
            Debug.LogWarning(
                "Calibration caught setup is waiting for "
                + TargetScenePath
                + ".");
            return false;
        }

        GameObject gate = FindSceneObjectByExactName("Gate");
        GameObject player =
            FindSceneObjectByExactName(BubuRunningGame.PlayerRootName);
        VideoClip caughtCg =
            AssetDatabase.LoadAssetAtPath<VideoClip>(CaughtCgPath);
        if (gate == null || player == null || caughtCg == null)
        {
            Debug.LogError(
                "Calibration caught setup requires Gate, Bubu Player "
                + "and Assets/Art/cg/beizhua cg.mp4.");
            return false;
        }

        PrecisionNeedleChallenge2D challenge =
            gate.GetComponent<PrecisionNeedleChallenge2D>();
        if (challenge == null)
        {
            Debug.LogError(
                "Calibration caught setup requires the existing "
                + "PrecisionNeedleChallenge2D on Gate.");
            return false;
        }

        GameObject presentationObject =
            GetOrCreateSceneObject(PresentationName);
        CalibrationCaughtPresentation2D presentation =
            GetOrAddComponent<CalibrationCaughtPresentation2D>(
                presentationObject);
        VideoPlayer videoPlayer =
            GetOrAddComponent<VideoPlayer>(presentationObject);
        AudioSource optionalBgmSource =
            GetOrAddComponent<AudioSource>(presentationObject);

        GameObject canvasObject =
            GetOrCreateUiChild(
                presentationObject.transform,
                CanvasName);
        RectTransform canvasTransform =
            RequireRectTransform(canvasObject);
        Canvas canvas = GetOrAddComponent<Canvas>(canvasObject);
        CanvasScaler scaler =
            GetOrAddComponent<CanvasScaler>(canvasObject);
        GetOrAddComponent<GraphicRaycaster>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 21000;
        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        StretchToParent(canvasTransform);

        GameObject displayObject =
            GetOrCreateUiChild(canvasObject.transform, DisplayName);
        RectTransform displayTransform =
            RequireRectTransform(displayObject);
        RawImage display =
            GetOrAddComponent<RawImage>(displayObject);
        StretchToParent(displayTransform);
        display.color = Color.white;
        display.raycastTarget = false;

        videoPlayer.playOnAwake = false;
        videoPlayer.clip = caughtCg;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.isLooping = false;
        videoPlayer.timeUpdateMode =
            VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.sendFrameReadyEvents = true;

        optionalBgmSource.playOnAwake = false;
        optionalBgmSource.clip = null;
        optionalBgmSource.loop = true;
        optionalBgmSource.spatialBlend = 0f;
        optionalBgmSource.ignoreListenerPause = true;

        presentation.presentationRoot = canvasObject;
        presentation.cgDisplay = display;
        presentation.videoPlayer = videoPlayer;
        presentation.caughtCg = caughtCg;
        presentation.optionalBgmSource = optionalBgmSource;
        presentation.optionalBgm = null;
        presentation.loopOptionalBgm = true;
        presentation.loopCg = false;
        presentation.pauseGameplay = true;
        presentation.pauseOtherAudio = true;
        presentation.returnToStartOnMouseClick = true;
        presentation.mouseClickDelay = 0.25f;
        canvasObject.SetActive(false);

        CalibrationFailureDetection2D detection =
            GetOrAddComponent<CalibrationFailureDetection2D>(gate);
        detection.needleChallenge = challenge;
        detection.detectionOrigin = player.transform;
        detection.soldiers =
            UnityEngine.Object.FindObjectsByType<PatrollingSoldier2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        detection.messageUI =
            UnityEngine.Object.FindAnyObjectByType<GameplayMessageUI2D>(
                FindObjectsInactive.Include);
        detection.caughtPresentation = presentation;
        detection.failuresRequiredToBeCaught = 3;
        detection.soldierDetectionRadius = 12f;
        detection.requireActiveSoldierInRange = true;
        detection.resetFailuresWhenChallengeStarts = false;
        detection.resetFailuresWhenChallengeCompletes = true;
        detection.detectedMessageFormat =
            "A guard heard the noise ({0} / {1}).";
        detection.outsideDetectionRangeMessage =
            "The guards were too far away to hear that mistake.";
        detection.caughtMessage = "The guards have found you.";

        SetDirty(
            presentationObject,
            presentation,
            videoPlayer,
            optionalBgmSource,
            canvasObject,
            canvas,
            scaler,
            displayObject,
            display,
            gate,
            detection,
            challenge);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            "Configured calibration failure detection: active soldiers "
            + "within a 12-unit editable radius alert on misses, and "
            + "the third detected miss plays beizhua cg. Optional BGM "
            + "is intentionally unassigned.");
        return true;
    }

    static void StretchToParent(RectTransform target)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
        target.localScale = Vector3.one;
    }

    static GameObject GetOrCreateSceneObject(string objectName)
    {
        GameObject existing = FindSceneObjectByExactName(objectName);
        return existing != null ? existing : new GameObject(objectName);
    }

    static GameObject GetOrCreateUiChild(
        Transform parent,
        string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null
            && existing.TryGetComponent(out RectTransform _))
        {
            return existing.gameObject;
        }

        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        GameObject child =
            new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child;
    }

    static RectTransform RequireRectTransform(GameObject target)
    {
        RectTransform rectTransform =
            target.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            throw new InvalidOperationException(
                target.name + " requires a RectTransform.");
        }

        return rectTransform;
    }

    static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null
            ? component
            : target.AddComponent<T>();
    }

    static void SetDirty(params UnityEngine.Object[] objects)
    {
        foreach (UnityEngine.Object target in objects)
        {
            if (target != null)
            {
                EditorUtility.SetDirty(target);
            }
        }
    }

    static GameObject FindSceneObjectByExactName(string objectName)
    {
        GameObject[] objects =
            UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        foreach (GameObject target in objects)
        {
            if (target != null
                && string.Equals(
                    target.name,
                    objectName,
                    StringComparison.Ordinal))
            {
                return target;
            }
        }

        return null;
    }
}
