using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HuajiaoArtSceneSetup
{
    const string RequestPath = "Assets/Editor/CodexHuajiaoArtSetupRequest.txt";
    const string HuajiaoSpritePath = "Assets/Art/charactor/huajiao.png";
    const string HuajiaoName = "huajiao";
    const string HuajiaoVisualName = "huajiao visual";

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Replace Huajiao Art")]
    public static void ReplaceHuajiaoArtFromMenu()
    {
        ReplaceHuajiaoArtInActiveScene();
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

        ReplaceHuajiaoArtInActiveScene();
        DeleteSetupRequest();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static bool SetupRequested()
    {
        return File.Exists(GetProjectRelativeAbsolutePath(RequestPath))
            || AssetDatabase.LoadAssetAtPath<TextAsset>(RequestPath) != null;
    }

    static void ReplaceHuajiaoArtInActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogWarning("Huajiao art replacement skipped because there is no valid active scene.");
            return;
        }

        Sprite huajiaoSprite = LoadLargestSpriteAtPath(HuajiaoSpritePath);
        if (huajiaoSprite == null)
        {
            Debug.LogWarning("Huajiao art replacement skipped because huajiao sprite was not found.");
            return;
        }

        GameObject huajiaoObject = FindSceneObjectByExactName(HuajiaoName);
        if (huajiaoObject == null)
        {
            Debug.LogWarning("Huajiao art replacement skipped because huajiao object was not found.");
            return;
        }

        Transform visualTransform = FindChildByExactName(huajiaoObject.transform, HuajiaoVisualName);
        SpriteRenderer spriteRenderer = visualTransform != null
            ? visualTransform.GetComponent<SpriteRenderer>()
            : huajiaoObject.GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer == null)
        {
            Debug.LogWarning("Huajiao art replacement skipped because no SpriteRenderer was found.");
            return;
        }

        spriteRenderer.sprite = huajiaoSprite;
        EditorUtility.SetDirty(spriteRenderer);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log("Replaced huajiao visual sprite with Assets/Art/charactor/huajiao.png.");
    }

    static Sprite LoadLargestSpriteAtPath(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        Sprite largestSprite = null;
        float largestArea = -1f;

        foreach (Object asset in assets)
        {
            Sprite sprite = asset as Sprite;
            if (sprite == null)
            {
                continue;
            }

            float area = sprite.rect.width * sprite.rect.height;
            if (area > largestArea)
            {
                largestArea = area;
                largestSprite = sprite;
            }
        }

        return largestSprite;
    }

    static GameObject FindSceneObjectByExactName(string objectName)
    {
        GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject != null && sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }

    static Transform FindChildByExactName(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    static void DeleteSetupRequest()
    {
        if (AssetDatabase.DeleteAsset(RequestPath))
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
