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

    protected override Size GetEstimatedItemSize(VirtualizeListViewItem item, Size availableSpace)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both || item.Position < 0) return new();

        return orientation is ScrollOrientation.Vertical
            ? new(availableSpace.Width - item.Margin.HorizontalThickness, EstimatedItemSize)
            : new(EstimatedItemSize, availableSpace.Height - item.Margin.VerticalThickness);
    }

    protected override Thickness GetItemMargin(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both || item.Position <= 0) return new();

        return orientation is ScrollOrientation.Vertical
            ? new(0d, ItemSpacing, 0d, 0d)
            : new(ItemSpacing, 0d, 0d, 0d);
    }

    public override VirtualizeListViewItem CreateItemForPosition(int position)
    {
        var item = new VirtualizeListViewItem(this)
        {
            AdapterItem = Adapter!.Items[position],
            Template = Adapter.GetTemplate(position),
            Position = position
        };

        item.Margin = GetItemMargin(LaidOutItems, item);
        item.Size = GetEstimatedItemSize(item, AvailableSpace);

        return item;
    }

    protected override void RepositionItemsFromIndex(IReadOnlyList<VirtualizeListViewItem> items, int index)
    {
        var count = items.Count;

        if (count == 0 || index < 0 || index >= count) return;

        for (int i = index; i < count; i++)
        {
            var item = items[i];
            item.Position = i;
            item.Margin = GetItemMargin(items, item);
        }
    }

    protected override void ShiftItemsChunk(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var count = items.Count;
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both || start < 0
            || start >= count || exclusiveEnd <= 0 || exclusiveEnd > count) return;

        var startItem = items[start];
        var prevIndex = start - 1;
        var prevItemRightBottom = prevIndex == -1 ? new() : items[prevIndex].RightBottomWithMargin;

        Func<VirtualizeListViewItem, Point> leftTopFunc;

        if (orientation is ScrollOrientation.Vertical)
        {
            var dy = prevItemRightBottom.Y - startItem.LeftTopWithMargin.Y;
            if (dy == 0d) return;

            leftTopFunc = item => new(item.LeftTopWithMargin.X, item.LeftTopWithMargin.Y + dy);
        }
        else
        {
            var dx = prevItemRightBottom.X - startItem.LeftTopWithMargin.X;
            if (dx == 0d) return;

            leftTopFunc = item => new(item.LeftTopWithMargin.X + dx, item.LeftTopWithMargin.Y);
        }

        for (int i = start; i < exclusiveEnd; i++)
        {
            var item = items[i];

            item.LeftTopWithMargin = leftTopFunc(item);
        }
    }

    protected virtual void ShiftItemsChunkVertical(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var startItem = items[start];
        var prevIndex = start - 1;
        var prevItemRightBottom = prevIndex == -1 ? new() : items[prevIndex].RightBottomWithMargin;

        var dy = prevItemRightBottom.Y - startItem.LeftTopWithMargin.Y;
        if (dy == 0d) return;

        for (int i = start; i < exclusiveEnd; i++)
        {
            var item = items[i];

            var leftTopWithMargin = item.LeftTopWithMargin;

            item.LeftTopWithMargin = new(leftTopWithMargin.X, leftTopWithMargin.Y + dy);
        }
    }

    protected virtual void ShiftItemsChunkHorizontal(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var startItem = items[start];
        var prevIndex = start - 1;
        var prevItemRightBottom = prevIndex == -1 ? new() : items[prevIndex].RightBottomWithMargin;

        var dx = prevItemRightBottom.X - startItem.LeftTopWithMargin.X;
        if (dx == 0d) return;

        for (int i = start; i < exclusiveEnd; i++)
        {
            var item = items[i];

            var leftTopWithMargin = item.LeftTopWithMargin;

            item.LeftTopWithMargin = new(leftTopWithMargin.X + dx, leftTopWithMargin.Y);
        }
    }

    protected override void ShiftItemsConsecutively(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var count = items.Count;
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both || start < 0
            || start >= count || exclusiveEnd <= 0 || exclusiveEnd > count) return;

        var prevIndex = start - 1;
        var prevItemRightBottom = prevIndex == -1 ? new() : items[prevIndex].RightBottomWithMargin;

        Func<VirtualizeListViewItem, Point, Point> leftTopFunc = orientation is ScrollOrientation.Vertical
            ? (item, rightBottom) => new(item.LeftTopWithMargin.X, rightBottom.Y)
            : (item, rightBottom) => new(rightBottom.X, item.LeftTopWithMargin.Y);

        for (int i = start; i < exclusiveEnd; i++)
        {
            var item = items[i];

            item.LeftTopWithMargin = leftTopFunc(item, prevItemRightBottom);

            prevItemRightBottom = item.RightBottomWithMargin;
        }
    }

    protected override bool AdjustScrollIfNeeded(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevBoundsOfItem)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both || item.Position == -1) return false;

        double dx = 0d, dy = 0d;

        if (orientation is ScrollOrientation.Vertical)
        {
            dy = item.RightBottom.Y - prevBoundsOfItem.Bottom;

            if (dy == 0d) return false;
        }
        else
        {
            dx = item.RightBottom.X - prevBoundsOfItem.Right;

            if (dx == 0d) return false;
        }

        ListView!.AdjustScroll(dx, dy);

        return true;
    }

    protected override Point GetOffsetToAdjustScroll(VirtualizeListViewItem item, Rect prevBoundsOfItem)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both || item.Position == -1) return new(0d, 0d);

        double dx = 0d, dy = 0d;

        var listView = ListView!;

        if (orientation is ScrollOrientation.Vertical)
        {
            var bottom = item.RightBottom.Y;
            var top = item.LeftTop.Y + listView.Padding.Top;
            dy = bottom - prevBoundsOfItem.Bottom;

            var scrollY = listView.ScrollY;

            if (dy == 0d || (top < (scrollY + listView.Height) && top > scrollY)) return new(0d, 0d);
        }
        else
        {
            var right = item.RightBottom.X;
            var left = item.LeftTop.X + listView.Padding.Left;
            dx = right - prevBoundsOfItem.Right;

            var scrollX = listView.ScrollX;

            if (dx == 0d || (left < (scrollX + listView.Width) && left > scrollX)) return new(0d, 0d);
        }

        return new(dx, dy);
    }


    protected virtual double GetOffsetToAdjustScrollVertical(VirtualizeListViewItem item, Rect prevBoundsOfItem)
    {
        var listView = ListView!;

        var bottom = item.RightBottom.Y;
        var top = item.LeftTop.Y + listView.Padding.Top;
        var dy = bottom - prevBoundsOfItem.Bottom;

        var scrollY = listView.ScrollY;

        return dy == 0d || (top < (scrollY + listView.Height) && top > scrollY) ? 0d : dy;
    }

    protected virtual double GetOffsetToAdjustScrollHorizontal(VirtualizeListViewItem item, Rect prevBoundsOfItem)
    {
        var listView = ListView!;

        var right = item.RightBottom.X;
        var left = item.LeftTop.X + listView.Padding.Left;
        var dx = right - prevBoundsOfItem.Right;

        var scrollX = listView.ScrollX;

        return dx == 0d || (left < (scrollX + listView.Width) && left > scrollX) ? 0d : dx;
    }

    protected override Size LayoutManagerMeasure(double widthConstraint, double heightConstraint)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both) return new();

        var needsAdjustScroll = NeedsAdjustScroll;
        var items = LaidOutItems;
        var length = items.Count;
        var listView = ListView!;
        var padding = listView.Padding;
        var availableSpace = AvailableSpace;
        Size listViewBounds = new(availableSpace.Width + padding.HorizontalThickness, availableSpace.Height + padding.VerticalThickness);

        Viewport = new(listView.ScrollX - padding.Left, listView.ScrollY - padding.Top, listViewBounds.Width, listViewBounds.Height);

        double totalOffsetToAdjustScrollX = 0d, totalOffsetToAdjustScrollY = 0d;
        Size desiredSize;

        if (orientation is ScrollOrientation.Vertical)
        {
            var maxWidth = 0d;
            var width = availableSpace.Width;

            Func<VirtualizeListViewItem, Rect, double> offsetFunc = needsAdjustScroll
               ? GetOffsetToAdjustScrollHorizontal
               : (item, prevBounds) => 0d;

            for (int i = 0; i < length; i++)
            {
                var item = items[i];

                if (!BeforeItemMeasure(item)) continue;

                var cell = item.Cell!;
                var iview = cell as IView;

                var prevBounds = item.Bounds;

                var measure = iview!.Measure(width - item.Margin.HorizontalThickness, double.PositiveInfinity);

                item.MeasuredSize = measure;
                item.Size = new(width, measure.Height);
                maxWidth = Math.Max(maxWidth, item.MeasuredSizeWithMargin.Width);

                if (item.Bounds == prevBounds) continue;

                ShiftItemsChunkVertical(items, item.Position + 1, length);

                totalOffsetToAdjustScrollY += offsetFunc(item, prevBounds);
            }

            desiredSize = new(
                Math.Min(widthConstraint, ListViewHorizontalOptions == LayoutOptions.Fill
                    ? width
                    : maxWidth),
                length == 0 ? 0 : items[^1].RightBottomWithMargin.Y);
        }
        else // Horizontal
        {
            var maxHeight = 0d;
            var height = availableSpace.Height;

            Func<VirtualizeListViewItem, Rect, double> offsetFunc = NeedsAdjustScroll
                ? GetOffsetToAdjustScrollHorizontal
                : (item, prevBounds) => 0d;

            for (int i = 0; i < length; i++)
            {
                var item = items[i];

                if (!BeforeItemMeasure(item)) continue;

                var cell = item.Cell!;
                var iview = cell as IView;

                var prevBounds = item.Bounds;

                var measure = iview!.Measure(double.PositiveInfinity, height - item.Margin.VerticalThickness);

                item.MeasuredSize = measure;
                item.Size = new(measure.Width, height);
                maxHeight = Math.Max(maxHeight, item.MeasuredSizeWithMargin.Height);

                if (item.Bounds == prevBounds) continue;

                ShiftItemsChunkHorizontal(items, item.Position + 1, length);

                totalOffsetToAdjustScrollX += offsetFunc(item, prevBounds);
            }

            desiredSize = new(
                length == 0 ? 0 : items[^1].RightBottomWithMargin.X,
                Math.Min(heightConstraint, ListViewVerticalOptions == LayoutOptions.Fill
                    ? height
                    : maxHeight));
        }

        if (needsAdjustScroll)
        {
            NeedsAdjustScroll = false;
            if (totalOffsetToAdjustScrollX != 0 || totalOffsetToAdjustScrollY != 0)
            {
                listView.AdjustScroll(totalOffsetToAdjustScrollX, totalOffsetToAdjustScrollY);
            }
        }

        return desiredSize;
    }

    protected virtual bool BeforeItemMeasure(VirtualizeListViewItem item)
    {
        if (!item.IsOnScreen)
        {
            DetachCell(item);
            return false;
        }

        if (item.Cell is null) ReuseCell(item);
        return true;
    }

    protected virtual Point AfterItemMeasure(List<VirtualizeListViewItem> items, int length, VirtualizeListViewItem item, Rect prevBounds)
    {
        if (item.Bounds == prevBounds) return new();

        ShiftItemsChunk(items, item.Position + 1, length);

        if (!NeedsAdjustScroll) return new();

        return GetOffsetToAdjustScroll(item, prevBounds);
    }

    public override Size ArrangeChildren(Rect bounds)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both) return bounds.Size;

        var items = VisibleItems.OrderBy(i => i.Position).ToList();
        var length = items.Count;

        double maxWidthWithMargin, maxHeightWithMargin;
        Func<VirtualizeListViewItem, Rect> arrangeFunc;

        if (orientation is ScrollOrientation.Vertical)
        {
            maxWidthWithMargin = bounds.Width;
            if (ListViewHorizontalOptions != LayoutOptions.Fill)
            {
                maxWidthWithMargin = Math.Min(length == 0 ? 0d : items.Max(i => i.MeasuredSizeWithMargin.Width), maxWidthWithMargin);
            }
            maxHeightWithMargin = bounds.Height;

            arrangeFunc = item => new(item.LeftTop, new(maxWidthWithMargin - item.Margin.HorizontalThickness, item.MeasuredSize.Height));
        }
        else // Horizontal
        {
            maxWidthWithMargin = bounds.Width;
            maxHeightWithMargin = bounds.Height;
            if (ListViewVerticalOptions != LayoutOptions.Fill)
            {
                maxHeightWithMargin = Math.Min(length == 0 ? 0d : items.Max(i => i.MeasuredSizeWithMargin.Height), maxHeightWithMargin);
            }

            arrangeFunc = item => new(item.LeftTop, new(item.MeasuredSize.Width, maxHeightWithMargin - item.Margin.VerticalThickness));
        }

        foreach (var item in items)
        {
            var cell = item.Cell!;
            var iview = cell as IView;

            Rect newBounds = arrangeFunc(item);

#if MACIOS
            if (newBounds == cell.Bounds) continue;
#endif
            iview!.Arrange(newBounds);
        }

        return new(maxWidthWithMargin, maxHeightWithMargin);
    }
}