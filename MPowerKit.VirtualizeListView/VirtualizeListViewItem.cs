using static MPowerKit.VirtualizeListView.DataAdapter;

namespace MPowerKit.VirtualizeListView;

public class VirtualizeListViewItem
{
    private CellHolder _cell;

    protected VirtualizeItemsLayoutManger LayoutManager { get; set; }

    public VirtualizeListViewItem(VirtualizeItemsLayoutManger layoutManager)
    {
        LayoutManager = layoutManager;
    }

    public int Position { get; set; } = -1;
    public virtual bool IsOnScreen => IntersectsWithScrollVisibleRect();
    public bool IsAttached => Cell?.Attached ?? false;
    public DataTemplate Template { get; set; }
    public AdapterItem AdapterItem { get; set; }
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

    public Size Size { get; set; }
    public Point LeftTopWithMargin { get; set; }
    public Rect Bounds => new(Margin.Left + LeftTopWithMargin.X, Margin.Top + LeftTopWithMargin.Y, Size.Width, Size.Height);
    public Point LeftTop => new(Bounds.Left, Bounds.Top);
    public Point RightBottom => new(Bounds.Right, Bounds.Bottom);
    public Point RightBottomWithMargin => new(RightBottom.X + Margin.Right, RightBottom.Y + Margin.Bottom);
    public Thickness Margin { get; set; }

    public int Span { get; set; }
    public int Row { get; set; } = -1;
    public int Column { get; set; } = -1;

    public virtual void OnCellSizeChanged()
    {
        if (Cell?.BindingContext is not null && Cell.BindingContext != AdapterItem.Data) return;

        LayoutManager?.OnItemSizeChanged(this);
    }

    protected virtual bool IntersectsWithScrollVisibleRect()
    {
        var control = LayoutManager.Control;

        Rect itemBoundsWithCollectionPadding = new(
            Bounds.X + control.Padding.Left,
            Bounds.Y + control.Padding.Top,
            Bounds.Width,
            Bounds.Height);

        Rect visibleRect = new(control.ScrollX, control.ScrollY, control.Width, control.Height);

        return itemBoundsWithCollectionPadding.IntersectsWith(visibleRect);
    }
}