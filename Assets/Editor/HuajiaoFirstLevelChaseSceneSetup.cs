using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public static class HuajiaoFirstLevelChaseSceneSetup
{
    const string RequestPath =
        "Assets/Editor/CodexHuajiaoFirstLevelChaseSetupRequest.txt";
    const string TargetScenePath = "Assets/Scenes/关卡1.unity";
    const string HuajiaoCgPath = "Assets/Art/cg/huajiao cg2.mp4";
    const string HuajiaoBgmPath = "Assets/Art/cg/huajiao bgm.mp3";
    const string PresentationName = "Huajiao Failure Presentation";
    const string CanvasName = "Huajiao Failure Canvas";
    const string DisplayName = "Huajiao CG Display";

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Setup First-Level Huajiao Chase")]
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
                "First-level huajiao chase setup is waiting for "
                + TargetScenePath
                + ".");
            return false;
        }

        GameObject huajiao = FindSceneObjectByExactName("huajiao");
        GameObject firstLevelDoor = FindSceneObjectByExactName("Door");
        VideoClip cgClip =
            AssetDatabase.LoadAssetAtPath<VideoClip>(HuajiaoCgPath);
        AudioClip bgmClip =
            AssetDatabase.LoadAssetAtPath<AudioClip>(HuajiaoBgmPath);
        if (huajiao == null
            || firstLevelDoor == null
            || cgClip == null
            || bgmClip == null)
        {
            Debug.LogError(
                "First-level huajiao chase setup requires huajiao, "
                + "Door, huajiao cg2.mp4 and huajiao bgm.mp3.");
            return false;
        }

        GameObject presentationObject =
            GetOrCreateSceneObject(PresentationName);
        HuajiaoFailurePresentation2D presentation =
            GetOrAddComponent<HuajiaoFailurePresentation2D>(
                presentationObject);
        VideoPlayer videoPlayer =
            GetOrAddComponent<VideoPlayer>(presentationObject);
        AudioSource bgmSource =
            GetOrAddComponent<AudioSource>(presentationObject);

        GameObject canvasObject =
            GetOrCreateUiChild(presentationObject.transform, CanvasName);
        RectTransform canvasTransform =
            GetOrAddRectTransform(canvasObject);
        Canvas canvas = GetOrAddComponent<Canvas>(canvasObject);
        CanvasScaler scaler =
            GetOrAddComponent<CanvasScaler>(canvasObject);
        GetOrAddComponent<GraphicRaycaster>(canvasObject);

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasTransform.anchorMin = Vector2.zero;
        canvasTransform.anchorMax = Vector2.one;
        canvasTransform.offsetMin = Vector2.zero;
        canvasTransform.offsetMax = Vector2.zero;
        canvasTransform.localScale = Vector3.one;

        GameObject displayObject =
            GetOrCreateUiChild(canvasObject.transform, DisplayName);
        RectTransform displayTransform =
            GetOrAddRectTransform(displayObject);
        RawImage display = GetOrAddComponent<RawImage>(displayObject);
        displayTransform.anchorMin = Vector2.zero;
        displayTransform.anchorMax = Vector2.one;
        displayTransform.offsetMin = Vector2.zero;
        displayTransform.offsetMax = Vector2.zero;
        displayTransform.localScale = Vector3.one;
        // RawImage tint multiplies every video pixel. White preserves the
        // decoded frame; black made the previous CG appear completely black.
        display.color = Color.white;
        display.raycastTarget = false;

        videoPlayer.playOnAwake = false;
        videoPlayer.clip = cgClip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.isLooping = false;
        videoPlayer.timeUpdateMode =
            VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.sendFrameReadyEvents = true;

        bgmSource.playOnAwake = false;
        bgmSource.clip = bgmClip;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;
        bgmSource.ignoreListenerPause = true;

        presentation.presentationRoot = canvasObject;
        presentation.cgDisplay = display;
        presentation.videoPlayer = videoPlayer;
        presentation.huajiaoCg = cgClip;
        presentation.bgmSource = bgmSource;
        presentation.huajiaoBgm = bgmClip;
        presentation.loopCg = false;
        presentation.loopBgm = true;
        presentation.pauseGameplay = true;
        presentation.pauseOtherAudio = true;
        presentation.returnOnMouseClick = true;
        presentation.mouseClickDelay = 0.25f;
        canvasObject.SetActive(false);

        huajiao.SetActive(true);
        HuajiaoMovement chase =
            GetOrAddComponent<HuajiaoMovement>(huajiao);
        chase.startDelaySeconds = 30f;
        chase.failurePresentation = presentation;
        chase.disableWhenPlayerLeavesFirstLevel = true;
        chase.firstLevelEndMarker = firstLevelDoor.transform;
        chase.firstLevelEndPadding = 0.15f;
        chase.continueWhilePuzzlePaused = true;

        LevelSegmentTransition2D transition =
            UnityEngine.Object
                .FindAnyObjectByType<LevelSegmentTransition2D>(
                    FindObjectsInactive.Include);
        if (transition != null)
        {
            transition.objectsEnabledInSecondLevel =
                RemoveObject(
                    transition.objectsEnabledInSecondLevel,
                    huajiao);
        }

        SetDirty(
            presentationObject,
            presentation,
            videoPlayer,
            bgmSource,
            canvasObject,
            canvas,
            scaler,
            displayObject,
            display,
            huajiao,
            chase,
            transition);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log(
            "Configured the first-level huajiao chase: 30-second "
            + "delay, huajiao cg2 failure video, looping BGM and "
            + "mouse-click return to the start screen.");
        return true;
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

    static RectTransform GetOrAddRectTransform(GameObject target)
    {
        RectTransform rectTransform = target.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            return rectTransform;
        }

        throw new InvalidOperationException(
            target.name + " must be created with a RectTransform.");
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
        GameObject[] sceneObjects =
            UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject != null
                && string.Equals(
                    sceneObject.name,
                    objectName,
                    StringComparison.Ordinal))
            {
                return sceneObject;
            }
        }

        return null;
    }

    static GameObject[] RemoveObject(
        GameObject[] source,
        GameObject objectToRemove)
    {
        if (source == null || source.Length == 0)
        {
            return Array.Empty<GameObject>();
        }

        int retainedCount = 0;
        foreach (GameObject item in source)
        {
            if (item != null && item != objectToRemove)
            {
                retainedCount++;
            }
        }

        GameObject[] result = new GameObject[retainedCount];
        int index = 0;
        foreach (GameObject item in source)
        {
            if (item != null && item != objectToRemove)
            {
                result[index++] = item;
            }
        }

        return result;
    }
}
