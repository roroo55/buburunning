using System;
using UnityEditor;
using UnityEngine;

public static class BubuRunningPrototypeValidator
{
    [InitializeOnLoadMethod]
    static void ValidateAfterScriptReload()
    {
        EditorApplication.delayCall += () =>
        {
            try
            {
                ValidateCoreLogic();
                Debug.Log("Bubu Running simplified prototype validation passed.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Bubu Running simplified prototype validation failed: " + exception.Message);
            }
        };
    }

    public static void ValidateCoreLogic()
    {
        AssertVector("W moves up", Vector2.up, BubuRunningGame.GetWasdInput(true, false, false, false));
        AssertVector("S moves down", Vector2.down, BubuRunningGame.GetWasdInput(false, false, true, false));
        AssertVector("A moves left", Vector2.left, BubuRunningGame.GetWasdInput(false, true, false, false));
        AssertVector("D moves right", Vector2.right, BubuRunningGame.GetWasdInput(false, false, false, true));
        AssertVector("No key gives no movement", Vector2.zero, BubuRunningGame.GetWasdInput(false, false, false, false));

        Vector2 diagonal = BubuRunningGame.GetWasdInput(true, false, false, true);
        AssertClose("Diagonal WASD input is normalized", 1f, diagonal.magnitude);

        bool soldierTouch = BubuRunningGame.RectsTouch(
            Vector2.zero,
            BubuRunningGame.PlayerWidth,
            BubuRunningGame.PlayerHeight,
            new Vector2(0.1f, 0.1f),
            BubuRunningGame.SoldierWidth,
            BubuRunningGame.SoldierHeight);
        AssertTrue("Soldier touch condition", soldierTouch);

        bool separated = BubuRunningGame.RectsTouch(
            Vector2.zero,
            BubuRunningGame.PlayerWidth,
            BubuRunningGame.PlayerHeight,
            new Vector2(4f, 0f),
            BubuRunningGame.SoldierWidth,
            BubuRunningGame.SoldierHeight);
        AssertTrue("Separated objects do not touch", !separated);
    }

    static void AssertVector(string label, Vector2 expected, Vector2 actual)
    {
        if (Vector2.Distance(expected, actual) > 0.001f)
        {
            throw new InvalidOperationException(label + " expected " + expected + " but got " + actual + ".");
        }
    }

    static void AssertClose(string label, float expected, float actual)
    {
        if (Mathf.Abs(expected - actual) > 0.001f)
        {
            throw new InvalidOperationException(label + " expected " + expected + " but got " + actual + ".");
        }
    }

    static void AssertTrue(string label, bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException(label + " failed.");
        }
    }
}
