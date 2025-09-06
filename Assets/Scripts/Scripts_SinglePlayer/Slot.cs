using System;
using UnityEngine;

public class Slot : MonoBehaviour
{
    public static Action<int> OnClickSlot;
    public SpriteRenderer curImage;
    public int slotIndex;
    [SerializeField] private GameObject _highlight;
    void Awake()
    {
        _highlight.SetActive(false);
    }

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
    void OnMouseEnter()
    {
        _highlight.SetActive(true);
    }
    void OnMouseExit()
    {
        _highlight.SetActive(false);
    }
}
