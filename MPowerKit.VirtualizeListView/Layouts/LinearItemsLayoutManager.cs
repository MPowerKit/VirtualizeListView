namespace MPowerKit.VirtualizeListView;

public partial class LinearItemsLayoutManager : VirtualizeItemsLayoutManager
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

        Func<VirtualizeListViewItem, double, Point> leftTopFunc;
        double delta;
        if (orientation is ScrollOrientation.Vertical)
        {
            var dy = prevItemRightBottom.Y - startItem.LeftTopWithMargin.Y;
            if (dy == 0d) return;

            delta = dy;
            leftTopFunc = static (item, deltaY) => new(item.LeftTopWithMargin.X, item.LeftTopWithMargin.Y + deltaY);
        }
        else
        {
            var dx = prevItemRightBottom.X - startItem.LeftTopWithMargin.X;
            if (dx == 0d) return;

            delta = dx;
            leftTopFunc = static (item, deltaX) => new(item.LeftTopWithMargin.X + deltaX, item.LeftTopWithMargin.Y);
        }

        for (int i = start; i < exclusiveEnd; i++)
        {
            var item = items[i];

            item.LeftTopWithMargin = leftTopFunc(item, delta);
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
            ? static (item, rightBottom) => new(item.LeftTopWithMargin.X, rightBottom.Y)
            : static (item, rightBottom) => new(rightBottom.X, item.LeftTopWithMargin.Y);

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

    protected virtual Size MeasureItem(VirtualizeListViewItem item, Rect viewport, double availableWidth, double availableHeight)
    {
        var adapter = Adapter!;

        var cell = ReuseCell(item, viewport);
        adapter.BindCell(cell, item.AdapterItem!, item.Position);

        var iview = cell as IView;
        var measure = iview!.Measure(availableWidth - item.Margin.HorizontalThickness, availableHeight - item.Margin.VerticalThickness);

        adapter.UnbindCell(cell, item.AdapterItem!, item.Position);
        CacheCell(item);

        return measure;
    }

    protected virtual Size MeasureCellVertical(VirtualizeListViewItem item, double availableWidth)
    {
        var cell = item.Cell!;
        var iview = cell as IView;

        return iview!.Measure(availableWidth - item.Margin.HorizontalThickness, double.PositiveInfinity);
    }

    protected virtual Size MeasureCellHorizontal(VirtualizeListViewItem item, double availableHeight)
    {
        var cell = item.Cell!;
        var iview = cell as IView;

        return iview!.Measure(double.PositiveInfinity, availableHeight - item.Margin.VerticalThickness);
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
        var viewport = Viewport;

        double totalOffsetToAdjustScrollX = 0d, totalOffsetToAdjustScrollY = 0d;
        Size desiredSize;

        if (orientation is ScrollOrientation.Vertical)
        {
            var maxItemsWidth = 0d;
            var availableWidth = availableSpace.Width;

            Func<VirtualizeListViewItem, Rect, double> offsetFunc = needsAdjustScroll
               ? GetOffsetToAdjustScrollVertical
               : static (item, prevBounds) => 0d;

            for (int i = 0; i < length; i++)
            {
                var item = items[i];

                if (!BeforeItemMeasure(item, viewport)) continue;

                var prevBounds = item.Bounds;

                var measure = MeasureCellVertical(item, availableWidth);

                item.MeasuredSize = measure;
                item.Size = new(availableWidth, measure.Height);
                maxItemsWidth = Math.Max(maxItemsWidth, item.MeasuredSizeWithMargin.Width);

                if (item.Bounds == prevBounds) continue;

                ShiftItemsChunkVertical(items, item.Position + 1, length);

                totalOffsetToAdjustScrollY += offsetFunc(item, prevBounds);
            }

            desiredSize = new(
                Math.Min(widthConstraint, ListViewHorizontalOptions == LayoutOptions.Fill
                    ? availableWidth
                    : maxItemsWidth),
                length == 0 ? 0 : items[^1].RightBottomWithMargin.Y);
        }
        else // Horizontal
        {
            var maxItemsHeight = 0d;
            var availableHeight = availableSpace.Height;

            Func<VirtualizeListViewItem, Rect, double> offsetFunc = NeedsAdjustScroll
                ? GetOffsetToAdjustScrollHorizontal
                : static (item, prevBounds) => 0d;

            for (int i = 0; i < length; i++)
            {
                var item = items[i];

                if (!BeforeItemMeasure(item, viewport)) continue;

                var prevBounds = item.Bounds;

                var measure = MeasureCellHorizontal(item, availableHeight);

                item.MeasuredSize = measure;
                item.Size = new(measure.Width, availableHeight);
                maxItemsHeight = Math.Max(maxItemsHeight, item.MeasuredSizeWithMargin.Height);

                if (item.Bounds == prevBounds) continue;

                ShiftItemsChunkHorizontal(items, item.Position + 1, length);

                totalOffsetToAdjustScrollX += offsetFunc(item, prevBounds);
            }

            desiredSize = new(
                length == 0 ? 0 : items[^1].RightBottomWithMargin.X,
                Math.Min(heightConstraint, ListViewVerticalOptions == LayoutOptions.Fill
                    ? availableHeight
                    : maxItemsHeight));
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

    public override Size ArrangeChildren(Rect bounds)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both) return bounds.Size;

        var items = VisibleItems.OrderBy(static i => i.Position).ToList();
        var length = items.Count;

        double maxWidthWithMargin, maxHeightWithMargin, maxOppositeDirectionSize;
        Func<VirtualizeListViewItem, double, Rect> arrangeFunc;

        if (orientation is ScrollOrientation.Vertical)
        {
            maxWidthWithMargin = bounds.Width;
            maxHeightWithMargin = bounds.Height;

            if (ListViewHorizontalOptions != LayoutOptions.Fill)
            {
                maxWidthWithMargin = Math.Min(length == 0 ? 0d : items.Max(i => i.MeasuredSizeWithMargin.Width), maxWidthWithMargin);
            }
            maxOppositeDirectionSize = maxWidthWithMargin;

            arrangeFunc = static (item, maxWidth) => new(item.LeftTop, new(maxWidth - item.Margin.HorizontalThickness, item.MeasuredSize.Height));
        }
        else // Horizontal
        {
            maxWidthWithMargin = bounds.Width;
            maxHeightWithMargin = bounds.Height;

            if (ListViewVerticalOptions != LayoutOptions.Fill)
            {
                maxHeightWithMargin = Math.Min(length == 0 ? 0d : items.Max(i => i.MeasuredSizeWithMargin.Height), maxHeightWithMargin);
            }
            maxOppositeDirectionSize = maxHeightWithMargin;

            arrangeFunc = static (item, maxHeight) => new(item.LeftTop, new(item.MeasuredSize.Width, maxHeight - item.Margin.VerticalThickness));
        }

        foreach (var item in items)
        {
            var cell = item.Cell!;
            var iview = cell as IView;

            Rect newBounds = arrangeFunc(item, maxOppositeDirectionSize);

#if MACIOS
            if (newBounds == cell.Bounds) continue;
#endif
            iview!.Arrange(newBounds);
        }

        return new(maxWidthWithMargin, maxHeightWithMargin);
    }

    #region Items Animation

    protected override void OnRemovedLeadingVisibleItems(List<(VirtualizeListViewItem item, Rect prevBounds)> itemsToShift,
        List<VirtualizeListViewItem> removedItems,
        Rect prevViewport, double scrollDeltaX, double scrollDeltaY)
    {
        var orientation = GetOrientation();
        if (orientation is ScrollOrientation.Both) return;

        var listView = ListView!;
        var padding = listView.Padding;
        var scrollX = listView.ScrollX;
        var scrollY = listView.ScrollY;

        var availableSpace = AvailableSpace;
        var viewport = Viewport;

        var firstRemovedItemPosition = removedItems[0].Position;

        List<(VirtualizeListViewItem item, Rect prevBounds)> itemsBeforeRemoved = [];
        List<(VirtualizeListViewItem item, Rect prevBounds)> itemsAfterRemoved = [];
        List<(VirtualizeListViewItem item, Rect prevBounds)> visibleItemsAfterRemoved = [];
        List<(VirtualizeListViewItem item, Rect prevBounds)> invisibleItemsAfterRemoved = [];
        foreach (var tpl in itemsToShift)
        {
            if (tpl.item.Position < firstRemovedItemPosition)
            {
                itemsBeforeRemoved.Add(tpl);
                continue;
            }

            itemsAfterRemoved.Add(tpl);

            if (IsOnScreen(tpl.item, viewport) && tpl.item.Cell is not null)
            {
                visibleItemsAfterRemoved.Add(tpl);
                continue;
            }

            invisibleItemsAfterRemoved.Add(tpl);
        }

        var adapter = Adapter!;

        var firstItemAfterRemoved = itemsAfterRemoved[0];

        double totalAdjustY = 0d, totalAdjustX = 0d;
        double totalHeightToFill = 0d, totalWidthToFill = 0d;

        if (orientation is ScrollOrientation.Vertical)
        {
            totalHeightToFill = firstItemAfterRemoved.prevBounds.Y - firstItemAfterRemoved.item.Margin.Top - prevViewport.Y;

            var availableWidth = availableSpace.Width;

            foreach (var tpl in itemsBeforeRemoved)
            {
                if (totalHeightToFill <= 0) break;

                var prevItemBounds = tpl.prevBounds;

                var item = tpl.item;
                var measure = MeasureItem(item, viewport, availableWidth, double.PositiveInfinity);

                totalAdjustY += measure.Height - prevItemBounds.Height;
                totalHeightToFill -= measure.Height + item.Margin.VerticalThickness;
            }

            totalHeightToFill -= padding.Top;
            if (totalHeightToFill > 0)
            {
                scrollDeltaY = 0;

                foreach (var item in visibleItemsAfterRemoved)
                {
                    if (item.item.Cell is CellHolder cell)
                    {
                        cell.TranslationY += totalHeightToFill;
                    }
                }

                listView.AdjustScroll(totalAdjustX, totalAdjustY);
            }
            else
            {
                totalHeightToFill = 0;
                scrollDeltaY = totalAdjustY;
            }
        }
        else
        {
            totalWidthToFill = firstItemAfterRemoved.prevBounds.X - firstItemAfterRemoved.item.Margin.Left - prevViewport.X;

            var availableHeight = availableSpace.Height;

            foreach (var tpl in itemsBeforeRemoved)
            {
                if (totalWidthToFill <= 0) break;

                var prevItemBounds = tpl.prevBounds;

                var item = tpl.item;
                var measure = MeasureItem(item, viewport, double.PositiveInfinity, availableHeight);

                totalAdjustX += measure.Width - prevItemBounds.Width;
                totalWidthToFill -= measure.Width + item.Margin.HorizontalThickness;
            }

            totalWidthToFill -= padding.Left;
            if (totalWidthToFill > 0)
            {
                scrollDeltaX = 0;

                foreach (var item in visibleItemsAfterRemoved)
                {
                    if (item.item.Cell is CellHolder cell)
                    {
                        cell.TranslationX += totalWidthToFill;
                    }
                }

                listView.AdjustScroll(totalAdjustX, totalAdjustY);
            }
            else
            {
                totalWidthToFill = 0;
                scrollDeltaX = totalAdjustX;
            }
        }

        viewport = Viewport;
        var scrollDeltaXFinal = viewport.X - prevViewport.X + totalAdjustX;
        var scrollDeltaYFinal = viewport.Y - prevViewport.Y + scrollDeltaY;

        ApplyScrollDeltaToRemovedItems(removedItems, prevViewport, scrollDeltaXFinal, scrollDeltaYFinal);

        var itemsToPrepare = itemsBeforeRemoved.Concat(invisibleItemsAfterRemoved)
            .Select(tpl => tpl.item).ToList();

        PrepareItemsForPreTranslationAnimation(itemsToPrepare);

        (this as IView).InvalidateMeasure();

        this.Dispatcher.Dispatch(() =>
        {
            CleanupItemsPreTranslationAnimation(itemsToPrepare);

            viewport = Viewport;

            var lastItemRightBottom = itemsBeforeRemoved[^1].item.RightBottom;

            Func<VirtualizeListViewItem, (double newX, double newY)> beforeRemovedTranslationFunc;
            Func<VirtualizeListViewItem, (double newX, double newY)> afterRemovedTranslationFunc;

            if (orientation is ScrollOrientation.Vertical)
            {
                var extraTopOffsetY = scrollY - padding.Top - lastItemRightBottom.Y + totalAdjustY;

                beforeRemovedTranslationFunc = item =>
                {
                    var leftTop = item.LeftTop;
                    var newY = leftTop.Y + extraTopOffsetY;
                    return (leftTop.X, newY);
                };

                afterRemovedTranslationFunc = item =>
                {
                    var leftTop = item.LeftTop;
                    var newY = leftTop.Y + totalHeightToFill;
                    return (leftTop.X, newY);
                };
            }
            else
            {
                var extraLeftOffsetX = scrollX - padding.Left - lastItemRightBottom.X;

                beforeRemovedTranslationFunc = item =>
                {
                    var leftTop = item.LeftTop;
                    var newX = leftTop.X + extraLeftOffsetX;
                    return (newX, leftTop.Y);
                };

                afterRemovedTranslationFunc = item =>
                {
                    var leftTop = item.LeftTop;
                    var newX = leftTop.X + totalWidthToFill;
                    return (newX, leftTop.Y);
                };
            }

            foreach (var (item, prevBounds) in itemsBeforeRemoved)
            {
                if (item.AnyAnimatingAnimation) continue;

                var (newX, newY) = beforeRemovedTranslationFunc(item);

                item.AddTranslationXAnimation(newX, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AddTranslationYAnimation(newY, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AnimateAll();
            }

            foreach (var (item, prevBounds) in itemsAfterRemoved.Where(i => IsOnScreen(i.item, viewport)))
            {
                if (item.AnyAnimatingAnimation) continue;

                var (newX, newY) = afterRemovedTranslationFunc(item);

                item.AddTranslationXAnimation(newX, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AddTranslationYAnimation(newY, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AnimateAll();
            }
        });
    }

    protected override void OnRemovedMiddleVisibleItems(List<(VirtualizeListViewItem item, Rect prevBounds)> itemsToShift,
        List<VirtualizeListViewItem> removedItems,
        Rect prevViewport, double scrollDeltaX, double scrollDeltaY)
    {
        var orientation = GetOrientation();
        if (orientation is ScrollOrientation.Both) return;

        var padding = ListView!.Padding;
        var scrollX = ListView.ScrollX;
        var scrollY = ListView.ScrollY;

        var viewport = Viewport;

        List<(VirtualizeListViewItem item, Rect prevBounds)> invisibleItemsToShift = [];
        List<(VirtualizeListViewItem item, Rect prevBounds)> visibleItemsToShift = [];

        foreach (var tpl in itemsToShift)
        {
            if (IsOnScreen(tpl.item, viewport) && tpl.item.Cell is not null)
            {
                visibleItemsToShift.Add(tpl);
                continue;
            }

            invisibleItemsToShift.Add(tpl);
        }

        var itemsToPrepare = invisibleItemsToShift.Select(tpl => tpl.item).ToList();

        PrepareItemsForPreTranslationAnimation(itemsToPrepare);

        double totalRemovedHeight = 0d, totalRemovedWidth = 0d;
        if (orientation is ScrollOrientation.Vertical)
        {
            totalRemovedHeight = removedItems.Sum(i => i.Size.Height + i.Margin.VerticalThickness);
        }
        else
        {
            totalRemovedWidth = removedItems.Sum(i => i.Size.Width + i.Margin.HorizontalThickness);
        }

        foreach (var (item, prevBounds) in visibleItemsToShift)
        {
            if (item.Cell is CellHolder cell)
            {
                cell.TranslationX += totalRemovedWidth;
                cell.TranslationY += totalRemovedHeight;
            }
        }

        (this as IView).InvalidateMeasure();

        this.Dispatcher.Dispatch(() =>
        {
            CleanupItemsPreTranslationAnimation(itemsToPrepare);

            var viewport = Viewport;

            foreach (var (item, prevBounds) in itemsToShift.Where(tpl => IsOnScreen(tpl.item, viewport) && !tpl.item.AnyAnimatingAnimation))
            {
                item.AddTranslationXAnimation(prevBounds.X, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AddTranslationYAnimation(prevBounds.Y, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AnimateAll();
            }
        });
    }

    protected override void OnRemovedAllVisibleItems(List<(VirtualizeListViewItem item, Rect prevBounds)> itemsToShift,
        List<VirtualizeListViewItem> removedItems,
        Rect prevViewport, double scrollDeltaX, double scrollDeltaY)
    {
        var orientation = GetOrientation();
        if (orientation is ScrollOrientation.Both) return;

        var padding = ListView!.Padding;
        var scrollX = ListView.ScrollX;
        var scrollY = ListView.ScrollY;

        ApplyScrollDeltaToRemovedItems(removedItems, prevViewport, scrollDeltaX, scrollDeltaY);

        var itemsToPrepare = itemsToShift.Select(tpl => tpl.item).ToList();

        PrepareItemsForPreTranslationAnimation(itemsToPrepare);

        (this as IView).InvalidateMeasure();

        this.Dispatcher.Dispatch(() =>
        {
            CleanupItemsPreTranslationAnimation(itemsToPrepare);

            var viewport = Viewport;

            itemsToShift = [.. itemsToShift.Where(tpl => IsOnScreen(tpl.item, viewport))];

            var firstItemLeftTop = itemsToShift[0].item.LeftTop;

            Func<VirtualizeListViewItem, (double newX, double newY)> translationFunc;

            if (orientation is ScrollOrientation.Vertical)
            {
                var extraOffsetY = Viewport.Bottom - firstItemLeftTop.Y;

                translationFunc = item =>
                {
                    var leftTop = item.LeftTop;
                    var newY = leftTop.Y + extraOffsetY;
                    return (leftTop.X, newY);
                };
            }
            else
            {
                var extraOffsetX = Viewport.Right - firstItemLeftTop.X;

                translationFunc = item =>
                {
                    var leftTop = item.LeftTop;
                    var newX = leftTop.X + extraOffsetX;
                    return (newX, leftTop.Y);
                };
            }

            foreach (var (item, prevBounds) in itemsToShift)
            {
                if (item.AnyAnimatingAnimation) continue;

                var (newX, newY) = translationFunc(item);

                item.AddTranslationYAnimation(newY, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AddTranslationXAnimation(newX, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AnimateAll();
            }
        });
    }

    protected override void OnRemovedTrailingVisibleItems(List<(VirtualizeListViewItem item, Rect prevBounds)> itemsToShift,
        List<VirtualizeListViewItem> removedItems,
        Rect prevViewport, double scrollDeltaX, double scrollDeltaY)
    {
        OnRemovedAllVisibleItems(itemsToShift, removedItems, prevViewport, scrollDeltaX, scrollDeltaY);
    }

    #endregion
}