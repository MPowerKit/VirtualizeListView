namespace MPowerKit.VirtualizeListView;

public class LinearItemsLayoutManager : VirtualizeItemsLayoutManger
{
    #region ItemSpacing
    public double ItemSpacing
    {
        get => (double)GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    public static readonly BindableProperty ItemSpacingProperty =
        BindableProperty.Create(
            nameof(ItemSpacing),
            typeof(double),
            typeof(LinearItemsLayoutManager));
    #endregion

    protected override Size GetDesiredLayoutSize(double widthConstraint, double heightConstraint)
    {
        if (IsOrientation(ScrollOrientation.Both) || LaidOutItems.Count == 0) return new();

        return IsOrientation(ScrollOrientation.Vertical)
            ? new(widthConstraint, LaidOutItems[^1].Bounds.Bottom)
            : new(LaidOutItems[^1].Bounds.Right, heightConstraint);
    }

    protected override Size GetEstimatedItemSize(VirtualizeListViewItem item)
    {
        if (IsOrientation(ScrollOrientation.Both) || item.Position < 0) return new();

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            return new Size(AvailableSpace.Width, 200d);
        }
        else
        {
            return new Size(200d, AvailableSpace.Height);
        }
    }

    protected override Size MeasureItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || items.Count == 0 || item.Position == -1) return new Size();

        var iview = (item.Cell as IView)!;

        return IsOrientation(ScrollOrientation.Vertical)
            ? iview.Measure(availableSpace.Width, double.PositiveInfinity)
            : iview.Measure(double.PositiveInfinity, availableSpace.Height);
    }

    protected override void ArrangeItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace)
    {
        var count = items.Count;

        if (IsOrientation(ScrollOrientation.Both)
            || count == 0 || item.Position == -1) return;

        var prevIndex = item.Position - 1;
        var prevItemBounds = prevIndex == -1 ? new() : items[prevIndex].Bounds;

        var margin = GetItemMargin(items, item);

        item.Margin = margin;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var top = prevItemBounds.Bottom;

            var newAvailableSpace = new Size(availableSpace.Width, availableSpace.Height);

            var request = MeasureItem(items, item, newAvailableSpace);

            item.CellBounds = new Rect(margin.Left, top + margin.Top, newAvailableSpace.Width, request.Height);
            item.Bounds = new Rect(0d, top, newAvailableSpace.Width, request.Height + margin.VerticalThickness);
        }
        else
        {
            var left = prevItemBounds.Right;

            var newAvailableSpace = new Size(availableSpace.Width, availableSpace.Height);

            var request = MeasureItem(items, item, newAvailableSpace);

            item.CellBounds = new Rect(left + margin.Left, margin.Top, request.Width, newAvailableSpace.Height);
            item.Bounds = new Rect(left, 0d, request.Width + margin.HorizontalThickness, newAvailableSpace.Height);
        }
    }

    protected override void ShiftAllItems(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var count = items.Count;

        if (IsOrientation(ScrollOrientation.Both) || start < 0
            || start >= count || exclusiveEnd <= 0 || exclusiveEnd > count) return;

        var item = items[start];
        var prevIndex = start - 1;
        var prevBounds = prevIndex == -1 ? new() : items[prevIndex].Bounds;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var dy = prevBounds.Bottom - item.Bounds.Y;
            if (dy == 0d) return;

            for (int i = start; i < exclusiveEnd; i++)
            {
                var currentItem = items[i];

                currentItem.CellBounds = new Rect(currentItem.CellBounds.X, currentItem.CellBounds.Y + dy, currentItem.CellBounds.Width, currentItem.CellBounds.Height);
                currentItem.Bounds = new Rect(currentItem.Bounds.X, currentItem.Bounds.Y + dy, currentItem.Bounds.Width, currentItem.Bounds.Height);
            }
        }
        else
        {
            var dx = prevBounds.Right - item.Bounds.X;
            if (dx == 0d) return;

            for (int i = start; i < exclusiveEnd; i++)
            {
                var currentItem = items[i];
                currentItem.CellBounds = new Rect(currentItem.CellBounds.X + dx, currentItem.CellBounds.Y, currentItem.CellBounds.Width, currentItem.CellBounds.Height);
                currentItem.Bounds = new Rect(currentItem.Bounds.X + dx, currentItem.Bounds.Y, currentItem.Bounds.Width, currentItem.Bounds.Height);
            }
        }
    }

    protected override void ShiftItemsConsecutively(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var count = items.Count;

        if (IsOrientation(ScrollOrientation.Both) || start < 0
            || start >= count || exclusiveEnd <= 0 || exclusiveEnd > count) return;

        var prevIndex = start - 1;
        var prevBounds = prevIndex == -1 ? new() : items[prevIndex].Bounds;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            for (int i = start; i < exclusiveEnd; i++)
            {
                var item = items[i];

                var dy = prevBounds.Bottom - item.Bounds.Y;
                if (dy == 0d)
                {
                    prevBounds = item.Bounds;
                    continue;
                }

                item.CellBounds = new Rect(item.CellBounds.X, item.CellBounds.Y + dy, item.CellBounds.Width, item.CellBounds.Height);
                item.Bounds = new Rect(item.Bounds.X, item.Bounds.Y + dy, item.Bounds.Width, item.Bounds.Height);

                prevBounds = item.Bounds;
            }
        }
        else
        {
            for (int i = start; i < exclusiveEnd; i++)
            {
                var item = items[i];

                var dx = prevBounds.Right - item.Bounds.X;
                if (dx == 0d)
                {
                    prevBounds = item.Bounds;
                    continue;
                }

                item.CellBounds = new Rect(item.CellBounds.X + dx, item.CellBounds.Y, item.CellBounds.Width, item.CellBounds.Height);
                item.Bounds = new Rect(item.Bounds.X + dx, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height);
            }
        }
    }

    protected override bool AdjustScrollIfNeeded(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem prevItem, Rect prevCellBounds)
    {
        if (IsOrientation(ScrollOrientation.Both) || prevItem.Position == -1) return false;

        bool needs;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var dy = prevItem.CellBounds.Bottom - prevCellBounds.Bottom;

            needs = dy != 0d;
            if (!needs) return needs;

            Control!.AdjustScroll(0d, dy);
        }
        else
        {
            var dx = prevItem.CellBounds.Right - prevCellBounds.Right;

            needs = dx != 0d;
            if (!needs) return needs;

            Control!.AdjustScroll(dx, 0d);
        }

        return needs;
    }

    protected override void AdjustScrollForItemBoundsChange(VirtualizeListViewItem item, Rect prevCellBounds)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || item.Position == -1) return;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            var bottom = item.Bounds.Bottom;
            var top = item.Bounds.Y + Control!.Padding.Top;
            var dy = bottom - prevCellBounds.Bottom;

            var scrollY = Control.ScrollY;

            if (dy == 0d || (top < (scrollY + Control.Height) && top > scrollY)) return;

            Control.AdjustScroll(0d, dy);
        }
        else
        {
            var right = item.Bounds.Right;
            var left = item.Bounds.X + Control!.Padding.Left;
            var dx = right - prevCellBounds.Right;

            var scrollX = Control.ScrollX;

            if (dx == 0d || (left < (scrollX + Control.Width) && left > scrollX)) return;

            Control.AdjustScroll(dx, 0d);
        }
    }

    protected override Thickness GetItemMargin(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        if (IsOrientation(ScrollOrientation.Both)
            || item.Position <= 0) return base.GetItemMargin(items, item);

        return IsOrientation(ScrollOrientation.Vertical)
            ? new(0d, ItemSpacing, 0d, 0d)
            : new(ItemSpacing, 0d, 0d, 0d);
    }
}