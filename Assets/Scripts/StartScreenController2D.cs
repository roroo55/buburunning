using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class StartScreenController2D : MonoBehaviour
{
    public GameObject startScreenRoot;
    public Button startButton;
    public Selectable defaultSelected;
    public bool showOnAwake = true;
    public bool pauseTimeScaleBeforeStart = true;
    public float gameplayTimeScale = 1f;
    public bool allowKeyboardStart = true;
    public KeyCode legacyStartKey = KeyCode.Space;
    public MonoBehaviour[] behavioursDisabledUntilStart;
    public GameObject[] objectsDisabledUntilStart;
    public bool autoDisableGameplayUntilStart = true;
    public bool pauseAudioUntilStart = true;

    readonly List<MonoBehaviour> autoDisabledBehaviours = new List<MonoBehaviour>();
    bool gameStarted;
    bool autoGameplayDisabled;
    float previousTimeScale = 1f;
    bool previousAudioPause;

    void Awake()
    {
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;

        if (showOnAwake)
        {
            ShowStartScreen();
        }
    }

    void OnEnable()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
            startButton.onClick.AddListener(StartGame);
        }
    }

    void OnDisable()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
        }

        if (!gameStarted && pauseTimeScaleBeforeStart)
        {
            Time.timeScale = previousTimeScale;
        }

        if (!gameStarted)
        {
            RestoreAudioPauseState();
            EnableAutoDisabledGameplay();
        }
    }

    void Update()
    {
        if (gameStarted || !allowKeyboardStart)
        {
            return;
        }

        if (WasStartKeyPressed())
        {
            StartGame();
        }
    }

    public void ShowStartScreen()
    {
        gameStarted = false;

        SetRootVisible(true);
        SetConfiguredGameplayActive(false);

        if (pauseAudioUntilStart)
        {
            AudioListener.pause = true;
        }

        if (pauseTimeScaleBeforeStart)
        {
            Time.timeScale = 0f;
        }

        SelectDefaultControl();
    }

    public void StartGame()
    {
        if (gameStarted)
        {
            return;
        }

        gameStarted = true;
        SetRootVisible(false);
        SetConfiguredGameplayActive(true);
        RestoreAudioPauseState();

        if (pauseTimeScaleBeforeStart)
        {
            Time.timeScale = Mathf.Max(0f, gameplayTimeScale);
        }
    }

    void SetRootVisible(bool visible)
    {
        if (startScreenRoot != null && startScreenRoot.activeSelf != visible)
        {
            startScreenRoot.SetActive(visible);
        }
    }

    void SetConfiguredGameplayActive(bool active)
    {
        if (!active)
        {
            DisableGameplayAutomatically();
        }

        if (behavioursDisabledUntilStart != null)
        {
            foreach (MonoBehaviour behaviour in behavioursDisabledUntilStart)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = active;
                }
            }
        }

        if (objectsDisabledUntilStart != null)
        {
            foreach (GameObject targetObject in objectsDisabledUntilStart)
            {
                if (targetObject != null)
                {
                    targetObject.SetActive(active);
                }
            }
        }

        if (active)
        {
            EnableAutoDisabledGameplay();
        }
    }

    void DisableGameplayAutomatically()
    {
        if (!autoDisableGameplayUntilStart || autoGameplayDisabled)
        {
            return;
        }

        autoDisabledBehaviours.Clear();
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (!ShouldAutoDisable(behaviour))
            {
                continue;
            }

            autoDisabledBehaviours.Add(behaviour);
            behaviour.enabled = false;
        }

        autoGameplayDisabled = true;
    }

    void EnableAutoDisabledGameplay()
    {
        if (!autoGameplayDisabled)
        {
            return;
        }

        foreach (MonoBehaviour behaviour in autoDisabledBehaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        autoDisabledBehaviours.Clear();
        autoGameplayDisabled = false;
    }

    bool ShouldAutoDisable(MonoBehaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled || behaviour == this)
        {
            return false;
        }

        if (behaviour.gameObject == gameObject)
        {
            return false;
        }

        if (startScreenRoot != null)
        {
            Transform screenRoot = startScreenRoot.transform;
            Transform behaviourTransform = behaviour.transform;
            if (behaviourTransform == screenRoot || behaviourTransform.IsChildOf(screenRoot))
            {
                return false;
            }
        }

        return behaviour is not UIBehaviour
            && behaviour is not EventSystem
            && behaviour is not BaseInputModule;
    }

    void RestoreAudioPauseState()
    {
        if (pauseAudioUntilStart)
        {
            AudioListener.pause = previousAudioPause;
        }
    }

    void SelectDefaultControl()
    {
        if (defaultSelected == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(defaultSelected.gameObject);
    }

    bool WasStartKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.spaceKey.wasPressedThisFrame
                || Keyboard.current.enterKey.wasPressedThisFrame
                || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(legacyStartKey) || Input.GetKeyDown(KeyCode.Return);
#else
        return false;
#endif
    }
}
