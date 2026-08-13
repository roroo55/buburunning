using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YinbingSoldierSceneSetup
{
    const string RequestPath = "Assets/Editor/CodexYinbingSoldierSetupRequest.txt";
    const string TargetScenePath = "Assets/Scenes/关卡1.unity";
    const string VisualName = "Yinbing Visual";
    const string MovingDownSpritePath = "Assets/Art/charactor/yinbing.png";
    const string MovingUpSpritePath = "Assets/Art/charactor/yinbing back.png";

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Setup Yinbing Soldier Visuals")]
    public static void SetupFromMenu()
    {
        SetupTargetScene();
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
            SetupTargetScene();
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

    static void SetupTargetScene()
    {
        Scene targetScene = GetLoadedSceneByPath(TargetScenePath);
        if (!targetScene.IsValid())
        {
            targetScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        }

        if (!targetScene.IsValid())
        {
            Debug.LogWarning("Yinbing soldier visual setup skipped because " + TargetScenePath + " could not be opened.");
            return;
        }

        Sprite movingDownSprite = LoadMainSprite(MovingDownSpritePath);
        Sprite movingUpSprite = LoadMainSprite(MovingUpSpritePath);
        if (movingDownSprite == null || movingUpSprite == null)
        {
            Debug.LogError("Yinbing soldier visual setup failed because yinbing sprites could not be loaded.");
            return;
        }

        int configuredCount = 0;
        foreach (GameObject rootObject in targetScene.GetRootGameObjects())
        {
            PatrollingSoldier2D[] soldiers = rootObject.GetComponentsInChildren<PatrollingSoldier2D>(true);
            foreach (PatrollingSoldier2D soldier in soldiers)
            {
                ConfigureSoldier(soldier, movingDownSprite, movingUpSprite);
                configuredCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);
        Debug.Log("Configured yinbing visuals for " + configuredCount + " patrolling soldier(s).");
    }

    static void ConfigureSoldier(PatrollingSoldier2D soldier, Sprite movingDownSprite, Sprite movingUpSprite)
    {
        if (soldier == null)
        {
            return;
        }

        SpriteRenderer rootRenderer = soldier.GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
        }

        Transform visualTransform = soldier.transform.Find(VisualName);
        if (visualTransform == null)
        {
            GameObject visualObject = new GameObject(VisualName);
            visualTransform = visualObject.transform;
            visualTransform.SetParent(soldier.transform, false);
        }

        SpriteRenderer visualRenderer = visualTransform.GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
        {
            visualRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        visualRenderer.sprite = soldier.startMovingUp ? movingUpSprite : movingDownSprite;
        visualRenderer.color = Color.white;

        if (rootRenderer != null)
        {
            visualRenderer.sortingLayerID = rootRenderer.sortingLayerID;
            visualRenderer.sortingOrder = rootRenderer.sortingOrder;
        }
        else
        {
            visualRenderer.sortingOrder = 20;
        }

        PatrollingSoldierVisual2D visual = soldier.GetComponent<PatrollingSoldierVisual2D>();
        if (visual == null)
        {
            visual = soldier.gameObject.AddComponent<PatrollingSoldierVisual2D>();
        }

        visual.visualRenderer = visualRenderer;
        visual.movingDownSprite = movingDownSprite;
        visual.movingUpSprite = movingUpSprite;
        visual.hideRootRenderer = true;
        visual.SetInitialDirection(soldier.startMovingUp);

        EditorUtility.SetDirty(soldier.gameObject);
        EditorUtility.SetDirty(visualTransform.gameObject);
        EditorUtility.SetDirty(visualRenderer);
        EditorUtility.SetDirty(visual);
        if (rootRenderer != null)
        {
            EditorUtility.SetDirty(rootRenderer);
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

    static Scene GetLoadedSceneByPath(string scenePath)
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (scene.IsValid() && scene.path == scenePath)
            {
                return scene;
            }
        }

        return default;
    }
}
