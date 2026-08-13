using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameplayMessageUI2D : MonoBehaviour
{
    public GameObject messagePanel;
    public Text messageText;
    public Image itemIcon;
    public RectTransform extraUIContent;

    [Header("Item Icon Layout")]
    public bool resizePanelForItemIcon = true;
    [Min(0f)]
    public float panelHeightWithoutIcon = 370f;
    [Min(0f)]
    public float panelHeightWithIcon = 370f;
    public Vector2 textPositionWithoutIcon = new Vector2(0f, -130f);
    public Vector2 textPositionWithIcon = new Vector2(0f, -178f);

    [Header("Responsive Text Layout")]
    public bool autoWrapMessageText = true;
    [Min(0f)]
    public float messageTextHorizontalInset = 240f;
    [Min(1f)]
    public float messageTextHeight = 104f;
    [Min(1)]
    public int minimumMessageFontSize = 18;
    [Min(1)]
    public int maximumMessageFontSize = 36;
    [Range(0.5f, 1.5f)]
    public float messageLineSpacing = 0.9f;

    [TextArea]
    public string keyFoundMessage = "You found the key.";
    [TextArea]
    public string nothingFoundMessage = "Nothing was found.";
    [TextArea]
    public string missingKeyMessage =
        "You cannot open this door without the key.";
    [Min(0f)]
    public float displayDuration = 2.5f;
    public bool useUnscaledTime = true;
    public bool hideOnAwake = true;
    public Font fontOverride;
    public bool useSystemChineseFontFallback = true;

    Coroutine hideRoutine;
    Font runtimeFont;

    void Awake()
    {
        CacheReferences();
        ConfigureFont();
        ConfigureMessageTextLayout();
        ConfigureItemIcon(null);

        if (hideOnAwake)
        {
            SetPanelVisible(false);
        }
    }

    void OnDisable()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }
    }

    public void ShowKeyFoundMessage()
    {
        ShowMessage(keyFoundMessage);
    }

    public void ShowNothingFoundMessage()
    {
        ShowMessage(nothingFoundMessage);
    }

    public void ShowMissingKeyMessage()
    {
        ShowMessage(missingKeyMessage);
    }

    public void ShowMessage(string message)
    {
        ShowMessageInternal(message, null, true);
    }

    public void ShowItemMessage(string message, Sprite icon)
    {
        ShowMessageInternal(message, icon, true);
    }

    public void ShowPersistentMessage(string message)
    {
        ShowMessageInternal(message, null, false);
    }

    void ShowMessageInternal(string message, Sprite icon, bool autoHide)
    {
        CacheReferences();
        ConfigureFont();
        ConfigureMessageTextLayout();

        if (messageText == null)
        {
            Debug.LogWarning("Gameplay message UI has no Text component assigned.");
            return;
        }

        messageText.text = message ?? string.Empty;
        ConfigureItemIcon(icon);
        SetPanelVisible(true);

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (autoHide && displayDuration > 0f)
        {
            hideRoutine = StartCoroutine(HideAfterDelay(displayDuration));
        }
    }

    public void HideMessage()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        SetPanelVisible(false);
    }

    IEnumerator HideAfterDelay(float duration)
    {
        if (useUnscaledTime)
        {
            yield return new WaitForSecondsRealtime(duration);
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }

        hideRoutine = null;
        SetPanelVisible(false);
    }

    void CacheReferences()
    {
        if (messagePanel == null)
        {
            Transform panel = transform.Find("Gameplay Message Panel");
            if (panel != null)
            {
                messagePanel = panel.gameObject;
            }
        }

        if (messageText == null && messagePanel != null)
        {
            messageText = messagePanel.GetComponentInChildren<Text>(true);
        }

        if (itemIcon == null && messagePanel != null)
        {
            Transform icon = messagePanel.transform.Find("Item Icon");
            if (icon != null)
            {
                itemIcon = icon.GetComponent<Image>();
            }
        }

        if (extraUIContent == null && messagePanel != null)
        {
            Transform content = messagePanel.transform.Find("Extra UI Content");
            if (content != null)
            {
                extraUIContent = content as RectTransform;
            }
        }
    }

    void ConfigureItemIcon(Sprite icon)
    {
        bool showIcon = itemIcon != null && icon != null;
        if (itemIcon != null)
        {
            itemIcon.sprite = icon;
            itemIcon.preserveAspect = true;
            if (itemIcon.gameObject.activeSelf != showIcon)
            {
                itemIcon.gameObject.SetActive(showIcon);
            }
        }

        if (!resizePanelForItemIcon || messagePanel == null)
        {
            return;
        }

        RectTransform panelRect = messagePanel.transform as RectTransform;
        if (panelRect != null)
        {
            panelRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                showIcon ? panelHeightWithIcon : panelHeightWithoutIcon);
        }

        if (messageText != null)
        {
            messageText.rectTransform.anchoredPosition =
                showIcon ? textPositionWithIcon : textPositionWithoutIcon;
        }
    }

    void ConfigureMessageTextLayout()
    {
        if (!autoWrapMessageText
            || messageText == null
            || messagePanel == null)
        {
            return;
        }

        RectTransform panelRect = messagePanel.transform as RectTransform;
        RectTransform textRect = messageText.rectTransform;
        if (panelRect != null && textRect != null)
        {
            float availableWidth =
                Mathf.Max(
                    1f,
                    panelRect.rect.width
                    - Mathf.Max(0f, messageTextHorizontalInset));
            textRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                availableWidth);
            textRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(1f, messageTextHeight));
        }

        int minimumSize = Mathf.Max(1, minimumMessageFontSize);
        int maximumSize =
            Mathf.Max(minimumSize, maximumMessageFontSize);
        messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
        messageText.verticalOverflow = VerticalWrapMode.Truncate;
        messageText.resizeTextForBestFit = true;
        messageText.resizeTextMinSize = minimumSize;
        messageText.resizeTextMaxSize = maximumSize;
        messageText.fontSize = maximumSize;
        messageText.lineSpacing =
            Mathf.Clamp(messageLineSpacing, 0.5f, 1.5f);
        messageText.alignment = TextAnchor.MiddleCenter;
    }

    void ConfigureFont()
    {
        if (messageText == null)
        {
            return;
        }

        if (fontOverride != null)
        {
            messageText.font = fontOverride;
            return;
        }

        if (!useSystemChineseFontFallback || runtimeFont != null)
        {
            if (runtimeFont != null)
            {
                messageText.font = runtimeFont;
            }

            return;
        }

        string[] preferredFonts =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "PingFang SC",
            "Noto Sans CJK SC",
            "Source Han Sans SC",
            "SimHei",
            "Arial Unicode MS",
        };

        runtimeFont = Font.CreateDynamicFontFromOSFont(preferredFonts, Mathf.Max(14, messageText.fontSize));
        if (runtimeFont != null)
        {
            messageText.font = runtimeFont;
        }
    }

    void SetPanelVisible(bool visible)
    {
        if (messagePanel != null && messagePanel.activeSelf != visible)
        {
            messagePanel.SetActive(visible);
        }
    }
}
