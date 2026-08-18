using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class PenzaiSearchDebugOverlay2D : MonoBehaviour
{
    public PenzaiSearchController2D controller;
    public bool visibleOnStart = true;
    public bool showAssignedLocations = true;
    public bool showUnsearchedObjects = true;
    public Rect windowRect = new Rect(16f, 16f, 470f, 650f);

    bool visible;
    Vector2 scrollPosition;

    void Awake()
    {
        visible = visibleOnStart;
        CacheController();
    }

    void Update()
    {
        CacheController();
        if (WasTogglePressed())
        {
            visible = !visible;
        }

        if (WasDumpPressed())
        {
            DumpStateToConsole();
        }
    }

    void OnGUI()
    {
        if (!visible || controller == null || controller.IsSearchSuppressed)
        {
            return;
        }

        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Search Debug (F3 hide / F4 dump)");
    }

    public void SetVisible(bool value)
    {
        visibleOnStart = value;
        visible = value;
    }

    void DrawWindow(int id)
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        DrawSummary();
        GUILayout.Space(8f);
        DrawItems();
        GUILayout.Space(8f);
        DrawSearchPoints();
        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
    }

    void DrawSummary()
    {
        int searched = 0;
        int found = 0;
        foreach (Transform point in controller.searchPoints)
        {
            if (controller.WasSearchPointExplored(point))
            {
                searched++;
            }
        }

        foreach (PenzaiSearchController2D.HiddenItem item in controller.hiddenItems)
        {
            if (item != null && item.collected)
            {
                found++;
            }
        }

        GUILayout.Label("SUMMARY");
        GUILayout.Label("Search points: " + controller.searchPoints.Count);
        GUILayout.Label("Searched: " + searched + "   Unsearched: " + (controller.searchPoints.Count - searched));
        GUILayout.Label("Items found: " + found + " / " + controller.hiddenItems.Length);
    }

    void DrawItems()
    {
        GUILayout.Label("ITEMS");
        foreach (PenzaiSearchController2D.HiddenItem item in controller.hiddenItems)
        {
            if (item == null)
            {
                continue;
            }

            string state = item.collected ? "FOUND" : "NOT FOUND";
            string location = item.assignedSearchPoint != null ? item.assignedSearchPoint.name : "UNASSIGNED";
            GUILayout.Label("[" + state + "] " + item.itemName);
            if (showAssignedLocations)
            {
                GUILayout.Label("    assigned to: " + location);
            }
        }
    }

    void DrawSearchPoints()
    {
        GUILayout.Label("SEARCH OBJECTS");
        foreach (Transform point in controller.searchPoints)
        {
            if (point == null)
            {
                GUILayout.Label("[INVALID] Missing object");
                continue;
            }

            bool explored = controller.WasSearchPointExplored(point);
            if (!explored && !showUnsearchedObjects)
            {
                continue;
            }

            List<string> assignedItems = GetAssignedItemNames(point);
            if (!explored)
            {
                GUILayout.Label("[UNSEARCHED] " + point.name);
            }
            else if (assignedItems.Count == 0)
            {
                GUILayout.Label("[SEARCHED - EMPTY] " + point.name);
            }
            else
            {
                GUILayout.Label("[SEARCHED - FOUND " + string.Join(", ", assignedItems) + "] " + point.name);
            }
        }
    }

    List<string> GetAssignedItemNames(Transform point)
    {
        List<string> names = new List<string>();
        foreach (PenzaiSearchController2D.HiddenItem item in controller.hiddenItems)
        {
            if (item != null && item.assignedSearchPoint == point)
            {
                names.Add(item.itemName);
            }
        }

        return names;
    }

    public void DumpStateToConsole()
    {
        CacheController();
        if (controller == null)
        {
            Debug.LogWarning("[SearchDebug] No PenzaiSearchController2D found.");
            return;
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine("[SearchDebug] COMPLETE STATE");
        foreach (PenzaiSearchController2D.HiddenItem item in controller.hiddenItems)
        {
            if (item == null)
            {
                continue;
            }

            report.Append("ITEM ").Append(item.itemName)
                .Append(" | ").Append(item.collected ? "FOUND" : "NOT FOUND")
                .Append(" | location=")
                .AppendLine(item.assignedSearchPoint != null ? item.assignedSearchPoint.name : "UNASSIGNED");
        }

        foreach (Transform point in controller.searchPoints)
        {
            if (point == null)
            {
                report.AppendLine("OBJECT <missing> | INVALID");
                continue;
            }

            bool explored = controller.WasSearchPointExplored(point);
            List<string> assigned = GetAssignedItemNames(point);
            report.Append("OBJECT ").Append(point.name)
                .Append(" | ").Append(explored ? "SEARCHED" : "UNSEARCHED")
                .Append(" | items=")
                .AppendLine(assigned.Count > 0 ? string.Join(",", assigned) : "EMPTY");
        }

        Debug.Log(report.ToString());
    }

    void CacheController()
    {
        if (controller == null)
        {
            controller = GetComponent<PenzaiSearchController2D>();
        }
    }

    static bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F3);
#else
        return false;
#endif
    }

    static bool WasDumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F4);
#else
        return false;
#endif
    }
}
