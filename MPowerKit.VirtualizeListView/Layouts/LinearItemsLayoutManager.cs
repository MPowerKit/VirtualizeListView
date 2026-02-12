using static MPowerKit.VirtualizeListView.VirtualizeListViewItem;

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

    protected override Thickness GetItemMargin(VirtualizeListViewItem item, int totalCount)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both || item.Position < 0 || item.Position >= totalCount - 1) return new();

        return orientation is ScrollOrientation.Vertical
            ? new(0d, 0d, 0d, ItemSpacing)
            : new(0d, 0d, ItemSpacing, 0d);
    }

    public override VirtualizeListViewItem CreateItemForPosition(int position, int totalCount)
    {
        var item = new VirtualizeListViewItem(this)
        {
            AdapterItem = Adapter!.Items[position],
            Template = Adapter.GetTemplate(position),
            Position = position
        };

        item.Margin = GetItemMargin(item, totalCount);
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
            item.Margin = GetItemMargin(item, count);
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
        var count = items.Count;
        if (start < 0 || start >= count || exclusiveEnd < 1 || exclusiveEnd > count) return;

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
        var count = items.Count;
        if (start < 0 || start >= count || exclusiveEnd < 1 || exclusiveEnd > count) return;

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

    protected override (double dx, double dy) AdjustScrollIfNeededOnInsert(
        IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both || item.Position == -1) return (0d, 0d);

        double dx = 0d, dy = 0d;

        if (orientation is ScrollOrientation.Vertical)
        {
            dy = item.LeftTop.Y - item.PrevBounds.Top;
            if (dy == 0d) return (0d, 0d);
        }
        else
        {
            dx = item.LeftTop.X - item.PrevBounds.Left;
            if (dx == 0d) return (0d, 0d);
        }

        var prevScrollY = ListView!.ScrollY;

        ListView!.AdjustScroll(dx, dy);

        var newScrollY = ListView!.ScrollY;

        return (dx, newScrollY - prevScrollY);
    }

    protected override (double dx, double dy) AdjustScrollIfNeededOnInsert(IReadOnlyList<VirtualizeListViewItem> items,
        IReadOnlyList<VirtualizeListViewItem> insertedItems, VirtualizeListViewItem firstVisibleItem)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both || firstVisibleItem.Position == -1
            || insertedItems.Count == 0) return (0d, 0d);

        double dx = 0d, dy = 0d;

        if (orientation is ScrollOrientation.Vertical)
        {
            dy = firstVisibleItem.LeftTop.Y - firstVisibleItem.PrevBounds.Top;
            if (dy == 0d) return (0d, 0d);
        }
        else
        {
            dx = firstVisibleItem.LeftTop.X - firstVisibleItem.PrevBounds.Left;
            if (dx == 0d) return (0d, 0d);
        }

        var prevScrollY = ListView!.ScrollY;

        ListView!.AdjustScroll(dx, dy);

        var newScrollY = ListView!.ScrollY;

        return (dx, newScrollY - prevScrollY);
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

    protected override Size MeasureItem(VirtualizeListViewItem item, Rect viewport, Size availableSpace)
    {
        var adapter = Adapter!;

        var cell = ReuseCell(item, viewport);
        adapter.BindCell(cell, item.AdapterItem!, item.Position);

        var iView = cell as IView;
        var measure = GetOrientation() is ScrollOrientation.Vertical
            ? iView!.Measure(availableSpace.Width - item.Margin.HorizontalThickness, double.PositiveInfinity)
            : iView!.Measure(double.PositiveInfinity, availableSpace.Height - item.Margin.VerticalThickness);

        adapter.UnbindCell(cell, item.AdapterItem!, item.Position);
        CacheCell(item);

        return measure;
    }

    protected virtual Size MeasureCellVertical(VirtualizeListViewItem item, double availableWidth)
    {
        var cell = item.Cell!;
        var iView = cell as IView;

        return iView!.Measure(availableWidth - item.Margin.HorizontalThickness, double.PositiveInfinity);
    }

    protected virtual Size MeasureCellHorizontal(VirtualizeListViewItem item, double availableHeight)
    {
        var cell = item.Cell!;
        var iView = cell as IView;

        return iView!.Measure(double.PositiveInfinity, availableHeight - item.Margin.VerticalThickness);
    }

    protected override Size LayoutManagerMeasure(double widthConstraint, double heightConstraint)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both) return new();

        var needsAdjustScroll = NeedsAdjustScroll;
        var items = LaidOutItems;
        var removedItems = RemovedItems ?? [];
        RemovedItems = null;
        var length = items.Count;
        var listView = ListView!;
        var padding = listView.Padding;
        var availableSpace = AvailableSpace;
        var viewport = Viewport;
        var prevViewport = PrevViewport;

        double totalOffsetToAdjustScrollX = 0d, totalOffsetToAdjustScrollY = 0d;
        Size desiredSize;

        if (orientation is ScrollOrientation.Vertical)
        {
            var maxItemsWidth = 0d;
            var availableWidth = availableSpace.Width;

            if (AwaitingNextLayoutPass)
            {
                for (int i = 0; i < length; i++)
                {
                    var item = items[i];

                    if (item.State is not ItemState.IsInserted) continue;

                    var prevBounds = item.Bounds;

                    var measure = MeasureItem(item, viewport, availableSpace);

                    item.MeasuredSize = measure;
                    item.Size = new(availableWidth, measure.Height);
                    maxItemsWidth = Math.Max(maxItemsWidth, item.MeasuredSizeWithMargin.Width);

                    if (item.Bounds == prevBounds) continue;

                    ShiftItemsChunkVertical(items, item.Position + 1, length);
                }
            }
            else
            {
                Func<VirtualizeListViewItem, Rect, double> offsetFunc = needsAdjustScroll
                    ? GetOffsetToAdjustScrollVertical
                    : static (item, prevBounds) => 0d;

                for (int i = 0; i < removedItems.Count; i++)
                {
                    var item = removedItems[i];
                    AnimateItemVertically(item, IsOnScreen(item, prevViewport), viewport, prevViewport, null);
                }

                double? startY = null;
                for (int i = 0; i < length; i++)
                {
                    var item = items[i];

                    bool isOnScreen = false;
                    try
                    {
                        if (!BeforeItemMeasure(item, viewport)) continue;

                        isOnScreen = true;

                        var prevBounds = item.Bounds;

                        var measure = MeasureCellVertical(item, availableWidth);

                        item.MeasuredSize = measure;
                        item.Size = new(availableWidth, measure.Height);
                        maxItemsWidth = Math.Max(maxItemsWidth, item.MeasuredSizeWithMargin.Width);

                        if (item.Bounds == prevBounds) continue;

                        ShiftItemsChunkVertical(items, item.Position + 1, length);

                        totalOffsetToAdjustScrollY += offsetFunc(item, prevBounds);
                    }
                    finally
                    {
                        startY = AnimateItemVertically(item, isOnScreen, viewport, prevViewport, startY);
                    }
                }
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

        PrevViewport = viewport;

        return desiredSize;
    }

    protected virtual double? AnimateItemVertically(VirtualizeListViewItem item, bool isOnScreen, Rect viewport, Rect prevViewport, double? startY)
    {
        if (item.State is ItemState.Idle) return startY;

        switch (item.State)
        {
            case ItemState.IsNew when item.Cell is CellHolder cell:
                {
                    item.AddFadeInAnimation(OpacityAnimationDuration, ZeroAnimationDelay);
                    item.Animate<OpacityAnimation>(1d);

                    startY = (startY is null) ? item.LeftTop.Y : startY;
                }
                break;
            case ItemState.IsInserted when item.Cell is CellHolder cell:
                {
                    //var dY = viewport.Y - prevViewport.Y;
                    //if (dY != 0) cell.TranslationY = dY;

                    item.AddFadeInAnimation(OpacityAnimationDuration, DefaultAnimationDelay);
                    item.Animate<OpacityAnimation>(1d);

                    startY = (startY is null) ? item.LeftTop.Y : startY;
                }
                break;
            case ItemState.ShouldBeShiftedOnInsert:
                {
                    Action detachAction = !isOnScreen ? () => DetachCell(item) : () => { };

                    item.AddTranslationYAnimation(item.PrevBounds.Y, ShiftAnimationDuration, ZeroAnimationDelay);
                    double endY = item.LeftTop.Y;
                    if (startY is double sY)
                    {
                        var extraOffsetY = prevViewport.Bottom - sY;
                        if (extraOffsetY <= item.LeftTop.Y - item.PrevBounds.Y)
                        {
                            endY = item.PrevBounds.Y + extraOffsetY;
                        }
                    }
                    item.Animate<TranslationYAnimation>(endY, detachAction);
                }
                break;
            case ItemState.ShouldBeShiftedOnRemove:
                {
                    //var wasOnScreen = WasOnScreen(item, viewport);
                    //if(!wasOnScreen && !isOnScreen)
                    //{

                    //}
                }
                break;
            case ItemState.ShouldBeRemoved:
                {
                    if (isOnScreen) DetachCell(item);
                }
                break;
            default:
                break;
        }

        item.PrevBounds = item.Bounds;
        item.State = ItemState.Idle;

        return startY;
    }

    public override Size ArrangeChildren(Rect bounds)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both
            || AwaitingNextLayoutPass) return bounds.Size;

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
            var iView = cell as IView;

            Rect newBounds = arrangeFunc(item, maxOppositeDirectionSize);

#if MACIOS
            if (newBounds == cell.Bounds) continue;
#endif
            iView!.Arrange(newBounds);
        }

        return new(maxWidthWithMargin, maxHeightWithMargin);
    }
}