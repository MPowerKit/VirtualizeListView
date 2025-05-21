using static MPowerKit.VirtualizeListView.DataAdapter;

namespace MPowerKit.VirtualizeListView;

public class VirtualizeListViewItem
{
    private CellHolder? _cell;

    protected VirtualizeItemsLayoutManger LayoutManager { get; set; }

    public VirtualizeListViewItem(VirtualizeItemsLayoutManger layoutManager)
    {
        LayoutManager = layoutManager;
    }

    public int Position { get; set; } = -1;
    public virtual bool IsOnScreen => IntersectsWithScrollVisibleRect();
    public bool IsAttached => Cell?.Attached ?? false;
    public DataTemplate? Template { get; set; }
    public AdapterItem? AdapterItem { get; set; }
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
    public Thickness Margin { get; set; }
    public Point LeftTop => new(LeftTopWithMargin.X + Margin.Left, LeftTopWithMargin.Y + Margin.Top);
    public Rect Bounds
    {
        get
        {
            var leftTop = LeftTop;
            return new(leftTop.X, leftTop.Y, Size.Width, Size.Height);
        }
    }
    public Point RightBottom
    {
        get
        {
            var bounds = Bounds;
            return new(bounds.Right, bounds.Bottom);
        }
    }
    public Point RightBottomWithMargin
    {
        get
        {
            var rightBottom = RightBottom;
            return new(rightBottom.X + Margin.Right, rightBottom.Y + Margin.Bottom);
        }
    }

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
        var listview = LayoutManager.ListView!;

        Rect itemBoundsWithCollectionPadding = new(
            Bounds.X,
            Bounds.Y,
            Bounds.Width,
            Bounds.Height);

        Rect visibleRect = new(listview.ScrollX - listview.Padding.Left, listview.ScrollY - listview.Padding.Top, listview.Width, listview.Height);

        return itemBoundsWithCollectionPadding.IntersectsWith(visibleRect);
    }
}