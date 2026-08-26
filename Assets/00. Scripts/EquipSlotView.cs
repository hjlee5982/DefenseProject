using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EquipSlotView : MonoBehaviour
{
    public void ShowItems(IReadOnlyList<Item> items)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Image image = transform.GetChild(i).GetComponent<Image>();
            if (image == null) continue;

            if (i < items.Count)
            {
                Item item = items[i];
                image.sprite = item.Icon;
                image.color = item.IconColor;
                image.enabled = true;
            }
            else
            {
                image.sprite = null;
                image.color = Color.white;
            }
        }
    }
}
