using UnityEngine;
using UnityEngine.UI;

public class KeyCountUI : MonoBehaviour
{
    [SerializeField]
    private Text countText;

    private KeyCounter counter;

    private void Start()
    {
        counter = FindObjectOfType<KeyCounter>();

        if (counter != null)
            SetCountText(counter.KeyCount, counter.MaxCount);
        else
            SetCountText(0, 0);
    }

    public void SetCountText(int keyCount, int maxCount)
    {
        if (countText == null)
            return;

        countText.text = $"{keyCount} / {maxCount}";
        countText.color = keyCount == maxCount && maxCount > 0 ? Color.yellow : Color.white;
    }
}
