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
    public virtual bool IsOnScreen => IntersectsWithViewport();
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

    public Size MeasuredSize { get; set; }
    public Size MeasuredSizeWithMargin
    {
        get
        {
            var measuredSize = MeasuredSize;
            var margin = Margin;
            return new(measuredSize.Width + margin.HorizontalThickness, measuredSize.Height + margin.VerticalThickness);
        }
    }

    public Size Size { get; set; }
    public Point LeftTopWithMargin { get; set; }
    public Thickness Margin { get; set; }
    public Point LeftTop
    {
        get
        {
            var leftTopWithMargin = LeftTopWithMargin;
            var margin = Margin;
            return new(leftTopWithMargin.X + margin.Left, leftTopWithMargin.Y + margin.Top);
        }
    }
    public Rect Bounds
    {
        get
        {
            var leftTop = LeftTop;
            var size = Size;
            return new(leftTop.X, leftTop.Y, size.Width, size.Height);
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
            var margin = Margin;
            return new(rightBottom.X + margin.Right, rightBottom.Y + margin.Bottom);
        }
    }

    public int Span { get; set; }
    public int Row { get; set; } = -1;
    public int Column { get; set; } = -1;

    public virtual void OnCellSizeChanged()
    {
        var bindingContext = Cell?.BindingContext;

        if (bindingContext is not null && !ReferenceEquals(bindingContext, AdapterItem?.Data)) return;

        LayoutManager?.OnItemSizeChanged(this);
    }

    protected virtual bool IntersectsWithViewport()
    {
        //var listview = LayoutManager.ListView!;

        //var availableSpace = listview.Bounds.Size;// LayoutManager.AvailableSpace;
        //var listViewPadding = listview.Padding;

        //Rect visibleRect = new(listview.ScrollX - listViewPadding.Left, listview.ScrollY - listViewPadding.Top, availableSpace.Width, availableSpace.Height);

        return Bounds.IntersectsWith(LayoutManager.Viewport);
    }
}