using System.Runtime.InteropServices;

using Microsoft.Maui.Layouts;

namespace MPowerKit.VirtualizeListView;

public class ItemsLayoutManager : LayoutManager
{
    protected VirtualizeItemsLayoutManger Layout { get; }

    public ItemsLayoutManager(VirtualizeItemsLayoutManger virtualizeItemsLayout) : base(virtualizeItemsLayout)
    {
        Layout = virtualizeItemsLayout;
    }

    public override Size Measure(double widthConstraint, double heightConstraint)
    {
        var measuredHeight = 0d;
        var measuredWidth = 0d;

        var items = CollectionsMarshal.AsSpan((Layout as IBindableLayout).Children as List<IView>);
        var length = items.Length;

        for (int n = 0; n < length; n++)
        {
            var child = items[n];
            var view = child as CellHolder;

            if (view.Item!.IsCached || !view.Item.IsAttached) continue;

            var bounds = view.Item.CellBounds;

            var measure = child.Measure(double.PositiveInfinity, double.PositiveInfinity);

            measuredWidth = Math.Max(measuredWidth, bounds.Left + view.Item.Margin.Right + measure.Width);
            measuredHeight = Math.Max(measuredHeight, bounds.Top + view.Item.Margin.Bottom + measure.Height);
        }

        var finalWidth = GetFinalLength(Layout.Width, widthConstraint, measuredWidth);
        var finalHeight = GetFinalLength(Layout.Height, heightConstraint, measuredHeight);

        return new(finalWidth, finalHeight);
    }

    public override Size ArrangeChildren(Rect bounds)
    {
        var padding = Layout.Padding;

        var availableWidth = bounds.Width - padding.HorizontalThickness;
        var availableHeight = bounds.Height - padding.VerticalThickness;

        var items = CollectionsMarshal.AsSpan((Layout as IBindableLayout).Children as List<IView>);
        var length = items.Length;

        for (int n = 0; n < length; n++)
        {
            var child = items[n];
            var view = child as CellHolder;
#if !ANDROID
            if (view.Item.IsCached || !view.Item.IsAttached) continue;
#else
            if (!view.Item.IsCached && !view.Item.IsAttached) continue;
#endif

            var (x, y) =
#if !ANDROID
            (0d, 0d);
#else
            (view.Item.CellBounds.X, view.Item.CellBounds.Y);
#endif

            var newBounds = new Rect(x, y, view.DesiredSize.Width, view.DesiredSize.Height);

#if !WINDOWS
            if (view.Bounds == newBounds) continue;
#endif

            child.Arrange(newBounds);
        }

        return new(availableWidth, availableHeight);
    }

    private double GetFinalLength(double explicitLength, double externalConstraint, double measuredLength)
    {
        var length = Math.Min(double.IsNaN(explicitLength) ? measuredLength : explicitLength, externalConstraint);

        return Math.Max(length, 0);
    }
}