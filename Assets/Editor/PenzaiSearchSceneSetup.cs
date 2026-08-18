using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PenzaiSearchSceneSetup
{
    const string RequestPath = "Assets/Editor/CodexPenzaiSearchSetupRequest.txt";
    const string ControllerName = "Penzai Search Controller";
    const string KeyName = "key";
    const string FrontPenzaiName = "penzai (1)";
    const int FrontPenzaiSortingOrder = 12;

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Setup Penzai Search")]
    public static void SetupPenzaiSearchFromMenu()
    {
        SetupActiveScene();
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

        SetupActiveScene();
        AssetDatabase.DeleteAsset(RequestPath);
        AssetDatabase.SaveAssets();
    }

    static bool SetupRequested()
    {
        return AssetDatabase.LoadAssetAtPath<TextAsset>(RequestPath) != null;
    }

    static void SetupActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogWarning("Penzai search setup skipped because there is no valid active scene.");
            return;
        }

        GameObject controllerObject = FindSceneObjectByExactName(ControllerName);
        if (controllerObject == null)
        {
            controllerObject = new GameObject(ControllerName);
        }

        PenzaiSearchController2D controller = controllerObject.GetComponent<PenzaiSearchController2D>();
        if (controller == null)
        {
            controller = controllerObject.AddComponent<PenzaiSearchController2D>();
        }

        controller.playerObjectName = BubuRunningGame.PlayerRootName;
        controller.searchPointPrefix = "penzai";
        if (controller.interactionRadius <= 0f)
        {
            controller.interactionRadius = 0.6f;
        }
        controller.randomizeItemsOnStart = true;
        controller.hideItemsOnStart = true;
        controller.player = FindTransformByExactName(BubuRunningGame.PlayerRootName);
        controller.RefreshSearchPointsFromScene();
        EnsureKeyItem(controller);
        ConfigureFrontPenzaiSorting();

        EditorUtility.SetDirty(controllerObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log("Configured penzai search controller with " + controller.searchPoints.Count + " search point(s).");
    }

    static void EnsureKeyItem(PenzaiSearchController2D controller)
    {
        GameObject keyObject = FindSceneObjectByExactName(KeyName);

        if (controller.hiddenItems == null || controller.hiddenItems.Length == 0)
        {
            controller.hiddenItems = new PenzaiSearchController2D.HiddenItem[]
            {
                new PenzaiSearchController2D.HiddenItem
                {
                    itemName = KeyName,
                    itemObjectName = KeyName,
                    itemObject = keyObject,
                }
            };
        }
        else
        {
            bool hasKeyDefinition = false;
            foreach (PenzaiSearchController2D.HiddenItem item in controller.hiddenItems)
            {
                if (item == null || item.itemName != KeyName)
                {
                    continue;
                }

                hasKeyDefinition = true;
                item.itemObjectName = KeyName;
                item.itemObject = keyObject;
            }

            if (!hasKeyDefinition)
            {
                PenzaiSearchController2D.HiddenItem[] expandedItems = new PenzaiSearchController2D.HiddenItem[controller.hiddenItems.Length + 1];
                controller.hiddenItems.CopyTo(expandedItems, 0);
                expandedItems[expandedItems.Length - 1] = new PenzaiSearchController2D.HiddenItem
                {
                    itemName = KeyName,
                    itemObjectName = KeyName,
                    itemObject = keyObject,
                };
                controller.hiddenItems = expandedItems;
            }
        }

        HideItemObject(keyObject);
    }

    static void ConfigureFrontPenzaiSorting()
    {
        GameObject frontPenzai = FindSceneObjectByExactName(FrontPenzaiName);
        if (frontPenzai == null)
        {
            Debug.LogWarning("Penzai sorting setup skipped because penzai (1) was not found.");
            return;
        }

        SpriteRenderer[] spriteRenderers = frontPenzai.GetComponentsInChildren<SpriteRenderer>(true);
        if (spriteRenderers.Length == 0)
        {
            Debug.LogWarning("Penzai sorting setup skipped because penzai (1) has no SpriteRenderer.");
            return;
        }

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null)
            {
                continue;
            }

            spriteRenderer.sortingOrder = FrontPenzaiSortingOrder;
            EditorUtility.SetDirty(spriteRenderer);
        }
    }

    static Transform FindTransformByExactName(string objectName)
    {
        GameObject sceneObject = FindSceneObjectByExactName(objectName);
        return sceneObject != null ? sceneObject.transform : null;
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

    static void HideItemObject(GameObject itemObject)
    {
        if (itemObject == null)
        {
            return;
        }

        Renderer[] renderers = itemObject.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer itemRenderer in renderers)
        {
            itemRenderer.enabled = false;
            EditorUtility.SetDirty(itemRenderer);
        }

        Collider2D[] colliders2D = itemObject.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D itemCollider in colliders2D)
        {
            itemCollider.enabled = false;
            EditorUtility.SetDirty(itemCollider);
        }

        Collider[] colliders3D = itemObject.GetComponentsInChildren<Collider>(true);
        foreach (Collider itemCollider in colliders3D)
        {
            itemCollider.enabled = false;
            EditorUtility.SetDirty(itemCollider);
        }
    }
}
