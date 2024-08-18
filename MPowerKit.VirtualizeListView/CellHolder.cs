namespace MPowerKit.VirtualizeListView;

public class CellHolder : Grid
{
    public VirtualizeListViewItem? Item { get; set; }
    public bool IsCached => Item is null;
    public bool WasArranged { get; protected set; }
    public bool WasMeasured { get; protected set; }

    protected override Size ArrangeOverride(Rect bounds)
    {
        WasArranged = true;
        return base.ArrangeOverride(bounds);
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        WasMeasured = true;
        return base.MeasureOverride(widthConstraint, heightConstraint);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        Item?.OnCellSizeChanged();
    }
}