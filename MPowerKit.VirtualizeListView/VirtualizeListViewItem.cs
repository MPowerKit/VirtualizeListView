namespace MPowerKit.VirtualizeListView;

public class VirtualizeListViewItem
{
    protected ItemsLayoutManager LayoutManager { get; set; }

    public VirtualizeListViewItem(ItemsLayoutManager layoutManager)
    {
        LayoutManager = layoutManager;
    }

    private CellHolder _cell;

    public int Position { get; set; } = -1;
    public virtual bool IsOnScreen => IntersectsWithScrollVisibleRect();
    public bool PendingSizeChange { get; set; }
    public bool IsAttached { get; set; }
    public DataTemplate Template { get; set; }
    public object BindingContext { get; set; }
    public CellHolder? Cell
    {
        get => _cell;
        set
        {
            if (value is null && _cell is not null)
            {
                _cell.Item = null;
            }

            _cell = value;

            if (_cell is not null)
            {
                _cell.Item = this;
            }
        }
    }
    public Rect CellBounds { get; set; }
    public Rect Bounds { get; set; }

    public virtual void OnCellSizeChanged()
    {
        if (PendingSizeChange)
        {
            PendingSizeChange = false;
            return;
        }

        LayoutManager?.OnItemSizeChanged(this);
    }

    protected virtual bool IntersectsWithScrollVisibleRect()
    {
        var control = LayoutManager.Control;

        var itemBoundsWithCollectionPadding = new Rect(
            CellBounds.X + control.Padding.Left,
            CellBounds.Y + control.Padding.Top,
            CellBounds.Width,
            CellBounds.Height);

        var visibleRect = new Rect(control.ScrollX, control.ScrollY, control.Width, control.Height);

        return itemBoundsWithCollectionPadding.IntersectsWith(visibleRect);
    }
}