using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class Bag : MonoBehaviour
{
    [SerializeField] private int size = 8;
    [SerializeField] private int inactiveBorderLines = 2;
    [SerializeField] private Slot slotPrefab;
    [SerializeField] private float gridLineThickness = 2f;
    [SerializeField] private Color gridLineColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

    private RectTransform slotsRoot;
    private RectTransform gridsRoot;
    private RectTransform itemsRoot;
    private GridLayoutGroup gridLayout;
    private Slot[] slots;
    private float cellSize;

    public int Size => size;
    public float CellSize => cellSize;
    public Slot[] Slots => slots;
    public Transform Items => itemsRoot;

    private void Awake()
    {
        slotsRoot = transform.Find("Slots") as RectTransform;
        gridsRoot = transform.Find("Grids") as RectTransform;
        itemsRoot = transform.Find("Items") as RectTransform;
        gridLayout = slotsRoot.GetComponent<GridLayoutGroup>();

        Rebuild();
    }

    public void SetSize(int newSize)
    {
        size = Mathf.Max(1, newSize);
        Rebuild();
    }

    public void Rebuild()
    {
        size = Mathf.Max(1, size);
        cellSize = slotsRoot.rect.width / size;

        ClearChildren(slotsRoot);
        ClearChildren(gridsRoot);

        ConfigureGridLayout();
        CreateSlots();
        CreateGridLines();
    }

    public void SetExpandableOnLockedSlots(bool expandable)
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (!slot.IsLocked) continue;
            slot.SetExpandable(expandable);
        }
    }

    public bool HasLockedSlots()
    {
        if (slots == null) return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].IsLocked) return true;
        }

        return false;
    }

    public bool TryGetSlotIndex(Vector2 screenPosition, Camera eventCamera, out int index)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    slots[i].RectTransform,
                    screenPosition,
                    eventCamera))
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    private void ConfigureGridLayout()
    {
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = size;
        gridLayout.cellSize = new Vector2(cellSize, cellSize);
        gridLayout.spacing = Vector2.zero;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        gridLayout.padding = new RectOffset(0, 0, 0, 0);
    }

    private void CreateSlots()
    {
        int count = size * size;
        slots = new Slot[count];

        for (int i = 0; i < count; i++)
        {
            Slot slot = Instantiate(slotPrefab, slotsRoot);
            slot.name = i == 0 ? "Slot" : $"Slot ({i})";
            int col = i % size;
            int row = i / size;
            bool startsActive = IsInsideActiveRegion(row, col);
            slot.SetLocked(!startsActive);
            slot.SetActivated(startsActive);
            slots[i] = slot;
        }
    }

    private bool IsInsideActiveRegion(int row, int col)
    {
        int border = Mathf.Max(0, inactiveBorderLines);
        if (size <= border * 2) return false;

        return row >= border && row < size - border
            && col >= border && col < size - border;
    }

    private void CreateGridLines()
    {
        float gridSize = cellSize * size;
        float origin = -gridSize * 0.5f;

        for (int i = 0; i <= size; i++)
        {
            float pos = origin + i * cellSize;
            CreateLine($"VLine_{i}", true, pos, gridSize);
            CreateLine($"HLine_{i}", false, pos, gridSize);
        }
    }

    private void CreateLine(string name, bool vertical, float position, float length)
    {
        GameObject lineObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        lineObject.transform.SetParent(gridsRoot, false);

        Image image = lineObject.GetComponent<Image>();
        image.color = gridLineColor;
        image.raycastTarget = false;

        RectTransform rect = lineObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        if (vertical)
        {
            rect.sizeDelta = new Vector2(gridLineThickness, length);
            rect.anchoredPosition = new Vector2(position, 0f);
        }
        else
        {
            rect.sizeDelta = new Vector2(length, gridLineThickness);
            rect.anchoredPosition = new Vector2(0f, position);
        }
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }
}
