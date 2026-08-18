using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PenzaiSearchFeedback2D : MonoBehaviour
{
    [Tooltip("隐藏物品会被放到这个位置。可以在场景中单独移动它。")]
    public Transform itemSpawnPoint;

    [Tooltip("留空时会自动查找场景中的 GameplayMessageUI2D。")]
    public GameplayMessageUI2D messageUI;

    [TextArea]
    public string nothingFoundMessage = "Nothing was found.";

    [Tooltip("每次搜索这个盆栽时触发。")]
    public UnityEvent onSearched = new UnityEvent();

    [Tooltip("在这个盆栽找到物品时触发，可在下方添加 UI 或其他反馈。")]
    public UnityEvent onItemFound = new UnityEvent();

    [Tooltip("这个盆栽没有找到物品时触发，可在下方添加 UI 或其他反馈。")]
    public UnityEvent onNothingFound = new UnityEvent();

    public void ShowItemFound(string message)
    {
        ShowItemFound(message, null);
    }

    public void ShowItemFound(string message, Sprite icon)
    {
        CacheMessageUI();
        onSearched?.Invoke();
        onItemFound?.Invoke();

        if (messageUI != null)
        {
            messageUI.ShowItemMessage(message, icon);
        }
    }

    public void ShowNothingFound()
    {
        CacheMessageUI();
        onSearched?.Invoke();
        onNothingFound?.Invoke();

        if (messageUI != null)
        {
            messageUI.ShowNothingFoundMessage(nothingFoundMessage);
        }
    }

    void CacheMessageUI()
    {
        if (messageUI == null)
        {
            messageUI = FindAnyObjectByType<GameplayMessageUI2D>(FindObjectsInactive.Include);
        }
    }
}
