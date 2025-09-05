using System;
using UnityEngine;

public class Slot : MonoBehaviour
{
    public static Action<int> OnClickSlot;
    public SpriteRenderer curImage;
    public int slotIndex;

    void OnMouseDown()
    {
        OnClickSlot?.Invoke(slotIndex);
    }

    public void SetSprite(Sprite newSprite)
    {
        if (curImage != null) curImage.sprite = newSprite;
    }

    public void SetClickable(bool enabled)
    {
        var col = GetComponent<BoxCollider2D>();
        if (col != null) col.enabled = enabled;
    }
}
