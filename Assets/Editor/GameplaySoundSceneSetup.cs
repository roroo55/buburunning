using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplaySoundSceneSetup
{
    const string RequestPath =
        "Assets/Editor/CodexGameplaySoundSetupRequest.txt";
    const string TargetScenePath = "Assets/Scenes/关卡1.unity";
    const string SearchClipPath = "Assets/Art/sound/search.mp3";
    const string OpenDoorClipPath =
        "Assets/Art/sound/open the door.mp3";

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Setup Search And Door Sounds")]
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
                "Gameplay sound setup is waiting for "
                + TargetScenePath
                + ".");
            return false;
        }

        AudioClip searchClip =
            AssetDatabase.LoadAssetAtPath<AudioClip>(SearchClipPath);
        AudioClip openDoorClip =
            AssetDatabase.LoadAssetAtPath<AudioClip>(OpenDoorClipPath);
        if (searchClip == null || openDoorClip == null)
        {
            Debug.LogError(
                "Gameplay sound setup could not load search.mp3 "
                + "or open the door.mp3.");
            return false;
        }

        PenzaiSearchController2D searchController =
            UnityEngine.Object.FindAnyObjectByType<PenzaiSearchController2D>(
                FindObjectsInactive.Include);
        DoorProgressGate2D doorGate =
            UnityEngine.Object.FindAnyObjectByType<DoorProgressGate2D>(
                FindObjectsInactive.Include);
        if (searchController == null || doorGate == null)
        {
            Debug.LogError(
                "Gameplay sound setup requires the penzai search "
                + "controller and first-level DoorProgressGate2D.");
            return false;
        }

        AudioSource searchSource =
            GetOrAddComponent<AudioSource>(searchController.gameObject);
        ConfigureTwoDimensionalOneShotSource(searchSource);
        searchController.searchAudioClip = searchClip;
        searchController.searchAudioSource = searchSource;
        searchController.searchAudioVolume = 1f;

        AudioSource doorSource =
            GetOrAddComponent<AudioSource>(doorGate.gameObject);
        ConfigureTwoDimensionalOneShotSource(doorSource);
        doorGate.openDoorAudioClip = openDoorClip;
        doorGate.openDoorAudioSource = doorSource;
        doorGate.openDoorAudioVolume = 1f;
        doorGate.playOpenAudioOnlyOnce = true;

        EditorUtility.SetDirty(searchController.gameObject);
        EditorUtility.SetDirty(searchController);
        EditorUtility.SetDirty(searchSource);
        EditorUtility.SetDirty(doorGate.gameObject);
        EditorUtility.SetDirty(doorGate);
        EditorUtility.SetDirty(doorSource);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log(
            "Configured search.mp3 for valid penzai searches and "
            + "open the door.mp3 for the first-level door unlock transition.");
        return true;
    }

    static void ConfigureTwoDimensionalOneShotSource(
        AudioSource audioSource)
    {
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
    }

    static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null
            ? component
            : target.AddComponent<T>();
    }
}
