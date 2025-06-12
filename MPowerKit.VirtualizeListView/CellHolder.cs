namespace MPowerKit.VirtualizeListView;

public class CellHolder : Grid
{
    public VirtualizeListViewItem? Item { get; set; }
    public bool IsCached => Item is null;
    public bool WasArranged { get; protected set; }
    public bool WasMeasured { get; protected set; }
    public bool Attached { get; set; }

    public View? Content
    {
        get => this.ElementAtOrDefault(0) as View;
        set
        {
            if (value == Content) return;

            DisconnectChildren();

            if (value is null) return;

            this.Add(value);
        }
    }

    private void DisconnectChildren()
    {
        var items = this.Children.OfType<VisualElement>().ToList();
        this.Clear();
        foreach (var item in items)
        {
            item.DisconnectItem();
        }
    }

    protected override void OnParentChanging(ParentChangingEventArgs args)
    {
        base.OnParentChanging(args);

        if (args.OldParent is null) return;

        this.BindingContext = null;
#if NET9_0_OR_GREATER
        this.DisconnectHandlers();
#endif
    }

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