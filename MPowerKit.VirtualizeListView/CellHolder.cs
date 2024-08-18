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

    public CellHolder()
    {
        this.SizeChanged += CellHolder_SizeChanged;
    }

    private void CellHolder_SizeChanged(object? sender, EventArgs e)
    {
        Item?.OnCellSizeChanged();
    }
}