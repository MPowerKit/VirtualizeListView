using System.Runtime.InteropServices;

using static MPowerKit.VirtualizeListView.VirtualizeListViewItem;

namespace MPowerKit.VirtualizeListView;

public partial class GridItemsLayoutManager : VirtualizeItemsLayoutManager
{
    #region Span
    public int Span
    {
        get => (int)GetValue(SpanProperty);
        set => SetValue(SpanProperty, value);
    }

    public static readonly BindableProperty SpanProperty =
        BindableProperty.Create(
            nameof(Span),
            typeof(int),
            typeof(GridItemsLayoutManager),
            1);
    #endregion

    #region VerticalItemSpacing
    public double VerticalItemSpacing
    {
        get => (double)GetValue(VerticalItemSpacingProperty);
        set => SetValue(VerticalItemSpacingProperty, value);
    }

    public static readonly BindableProperty VerticalItemSpacingProperty =
        BindableProperty.Create(
            nameof(VerticalItemSpacing),
            typeof(double),
            typeof(GridItemsLayoutManager));
    #endregion

    #region HorizontalItemsSpacing
    public double HorizontalItemsSpacing
    {
        get => (double)GetValue(HorizontalItemsSpacingProperty);
        set => SetValue(HorizontalItemsSpacingProperty, value);
    }

    public static readonly BindableProperty HorizontalItemsSpacingProperty =
        BindableProperty.Create(
            nameof(HorizontalItemsSpacing),
            typeof(double),
            typeof(GridItemsLayoutManager));
    #endregion

    public override VirtualizeListViewItem CreateItemForPosition(int position, int totalCount)
    {
        var item = new VirtualizeListViewItem(this)
        {
            AdapterItem = Adapter!.Items[position],
            Template = Adapter.GetTemplate(position),
            Position = position,
        };

        SetupSpanForItem(LaidOutItems, item);
        SetupRowColumnForItem(LaidOutItems, item);

        item.Margin = GetItemMargin(item, totalCount);
        item.Size = GetEstimatedItemSize(item, AvailableSpace);

        return item;
    }

    protected virtual void SetupSpanForItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        if (GetOrientation() is ScrollOrientation.Both || item.Position < 0) return;

        item.Span = Adapter!.IsSupplementary(item.Position) ? Span : 1;
    }

    protected virtual void SetupRowColumnForItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both || item.Position < 0) return;

        var prevIndex = item.Position - 1;
        var prevItem = prevIndex == -1 ? null : items[prevIndex];

        if (prevItem is null)
        {
            item.Column = 0;
            item.Row = 0;
            return;
        }

        if (orientation is ScrollOrientation.Vertical)
        {
            var newColumn = prevItem.Column + prevItem.Span;
            if (newColumn + item.Span > Span)
            {
                item.Column = 0;
                item.Row = prevItem.Row + 1;
                return;
            }

            item.Column = newColumn;
            item.Row = prevItem.Row;
        }
        else
        {
            var newRow = prevItem.Row + prevItem.Span;
            if (newRow + item.Span > Span)
            {
                item.Row = 0;
                item.Column = prevItem.Column + 1;
                return;
            }

            item.Row = newRow;
            item.Column = prevItem.Column;
        }
    }

    protected override void RepositionItemsFromIndex(IReadOnlyList<VirtualizeListViewItem> items, int index)
    {
        var count = items.Count;

        if (count == 0 || index < 0 || index >= count) return;

        for (int i = index; i < count; i++)
        {
            var item = LaidOutItems[i];
            item.Position = i;

            SetupRowColumnForItem(items, item);

            item.Margin = GetItemMargin(item, count);
        }
    }

    protected override Size GetEstimatedItemSize(VirtualizeListViewItem item, Size availableSpace)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both || item.Position < 0) return new();

        var span = Span;
        var itemSpan = item.Span;
        if (orientation is ScrollOrientation.Vertical)
        {
            var horizontalSpacing = HorizontalItemsSpacing;
            var totalRowSpacing = horizontalSpacing * (span - 1);
            var itemWidthPerSpan = (availableSpace.Width - totalRowSpacing) / span;
            var totalItemSpacing = horizontalSpacing * (itemSpan - 1);
            var width = itemSpan * itemWidthPerSpan + totalItemSpacing;
            return new(width, EstimatedItemSize);
        }
        else
        {
            var verticalSpacing = VerticalItemSpacing;
            var totalColumnSpacing = verticalSpacing * (span - 1);
            var itemHeightPerSpan = (availableSpace.Height - totalColumnSpacing) / span;
            var totalItemSpacing = verticalSpacing * (itemSpan - 1);
            var height = itemSpan * itemHeightPerSpan + totalItemSpacing;
            return new(EstimatedItemSize, height);
        }
    }

    protected virtual Size GetEstimatedItemSizeVertical(VirtualizeListViewItem item, Size availableSpace)
    {
        var span = Span;
        var itemSpan = item.Span;
        var horizontalSpacing = HorizontalItemsSpacing;
        var totalRowSpacing = horizontalSpacing * (span - 1);
        var itemWidthPerSpan = (availableSpace.Width - totalRowSpacing) / span;
        var totalItemSpacing = horizontalSpacing * (itemSpan - 1);

        return new(itemSpan * itemWidthPerSpan + totalItemSpacing, EstimatedItemSize);
    }

    protected virtual Size GetEstimatedItemSizeHorizontal(VirtualizeListViewItem item, Size availableSpace)
    {
        var span = Span;
        var itemSpan = item.Span;
        var verticalSpacing = VerticalItemSpacing;
        var totalColumnSpacing = verticalSpacing * (span - 1);
        var itemHeightPerSpan = (availableSpace.Height - totalColumnSpacing) / span;
        var totalItemSpacing = verticalSpacing * (itemSpan - 1);

        return new(EstimatedItemSize, itemSpan * itemHeightPerSpan + totalItemSpacing);
    }

    protected override Thickness GetItemMargin(VirtualizeListViewItem item, int totalCount)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both || item.Position < 0 || item.Position >= totalCount - 1) return new();

        var horizontalSpacing = HorizontalItemsSpacing;
        var verticalSpacing = VerticalItemSpacing;

        if (orientation is ScrollOrientation.Vertical)
        {
            return item.Row == 0
                ? new(horizontalSpacing, 0d, 0d, 0d)
                : new(item.Column > 0 ? horizontalSpacing : 0d, verticalSpacing, 0d, 0d);
        }
        else
        {
            return item.Column == 0
               ? new(0d, verticalSpacing, 0d, 0d)
               : new(horizontalSpacing, item.Row > 0 ? verticalSpacing : 0d, 0d, 0d);
        }
    }

    protected virtual int GetItemsCountInRow(IReadOnlyList<VirtualizeListViewItem> items, int start)
    {
        var count = items.Count;
        var row = items[start].Row;
        int itemsCount = 0;
        for (int i = start; i < count; i++)
        {
            itemsCount++;
            if (i + 1 >= count || items[i + 1].Row != row) break;
        }
        return itemsCount;
    }

    protected virtual int GetItemsCountInColumn(IReadOnlyList<VirtualizeListViewItem> items, int start)
    {
        var count = items.Count;
        var column = items[start].Column;
        int itemsCount = 0;
        for (int i = start; i < count; i++)
        {
            itemsCount++;
            if (i + 1 >= count || items[i + 1].Column != column) break;
        }
        return itemsCount;
    }

    protected override void ShiftItemsChunk(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        ShiftItemsConsecutively(items, start, exclusiveEnd);
    }

    protected override void ShiftItemsConsecutively(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var count = items.Count;

        var orientation = GetOrientation();
        if (orientation is ScrollOrientation.Both || start < 0
            || start >= count || exclusiveEnd <= 0 || exclusiveEnd > count) return;

        if (orientation is ScrollOrientation.Vertical)
        {
            ShiftItemsConsecutivelyVertical(items, start, exclusiveEnd);
        }
        else
        {
            ShiftItemsConsecutivelyHorizontal(items, start, exclusiveEnd);
        }
    }

    protected virtual void ShiftItemsConsecutivelyVertical(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var prevIndex = start - 1;
        VirtualizeListViewItem? prevItem = null;
        Point prevItemRightBottom = new();
        Point prevItemLeftTop = new();

        if (prevIndex >= 0)
        {
            prevItem = items[prevIndex];
            prevItemRightBottom = prevItem.RightBottomWithMargin;
            prevItemLeftTop = prevItem.LeftTopWithMargin;
        }

        for (int i = start; i < exclusiveEnd; i++)
        {
            var item = items[i];
            item.LeftTopWithMargin = prevItem?.Row != item.Row
                ? new(0d, prevItemRightBottom.Y)
                : new(prevItemRightBottom.X, prevItemLeftTop.Y);

            prevItem = item;
            prevItemRightBottom = prevItem.RightBottomWithMargin;
            prevItemLeftTop = prevItem.LeftTopWithMargin;
        }
    }

    protected virtual void ShiftItemsConsecutivelyHorizontal(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd)
    {
        var prevIndex = start - 1;
        VirtualizeListViewItem? prevItem = null;
        Point prevItemRightBottom = new();
        Point prevItemLeftTop = new();

        if (prevIndex >= 0)
        {
            prevItem = items[prevIndex];
            prevItemRightBottom = prevItem.RightBottomWithMargin;
            prevItemLeftTop = prevItem.LeftTopWithMargin;
        }

        for (int i = start; i < exclusiveEnd; i++)
        {
            var item = items[i];
            item.LeftTopWithMargin = prevItem?.Column != item.Column
                ? new(prevItemRightBottom.X, 0d)
                : new(prevItemLeftTop.X, prevItemRightBottom.Y);

            prevItem = item;
            prevItemRightBottom = prevItem.RightBottomWithMargin;
            prevItemLeftTop = prevItem.LeftTopWithMargin;
        }
    }

    protected virtual double GetBiggestBottomInRow(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        var count = items.Count;
        var row = item.Row;
        for (int i = item.Position - 1; i >= 0; i--)
        {
            var prevItem = items[i];
            if (prevItem.Row != row) break;
            item = prevItem;
        }

        var biggestBottom = 0d;
        for (int i = item.Position; i < count; i++)
        {
            biggestBottom = Math.Max(biggestBottom, items[i].RightBottom.Y);
            if (i + 1 >= count || items[i + 1].Row != row) break;
        }
        return biggestBottom;
    }

    protected virtual double GetBiggestRightInColumn(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        var count = items.Count;
        var column = item.Column;
        for (int i = item.Position - 1; i >= 0; i--)
        {
            var prevItem = items[i];
            if (prevItem.Column != column) break;
            item = prevItem;
        }

        var biggestRight = 0d;
        for (int i = item.Position; i < count; i++)
        {
            biggestRight = Math.Max(biggestRight, items[i].RightBottom.X);
            if (i + 1 >= count || items[i + 1].Column != column) break;
        }
        return biggestRight;
    }

    protected override (double dx, double dy) AdjustScrollIfNeededOnInsert(
        IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both || item.Position == -1) return (0d, 0d);

        double dx = 0d, dy = 0d;

        if (orientation is ScrollOrientation.Vertical)
        {
            dy = item.RightBottom.Y - item.PrevBounds.Bottom;
            if (dy == 0d) return (0d, 0d);
        }
        else
        {
            dx = item.RightBottom.X - item.PrevBounds.Right;
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
            var biggestBottom = GetBiggestBottomInRow(items, item);

            dy = biggestBottom - prevBoundsOfItem.Bottom;
            if (dy == 0d) return false;
        }
        else
        {
            var biggestRight = GetBiggestRightInColumn(items, item);

            dx = biggestRight - prevBoundsOfItem.Right;
            if (dx == 0d) return false;
        }

        ListView!.AdjustScroll(dx, dy);

        return true;
    }

    protected virtual double GetItemOffsetToAdjustScrollVertical(VirtualizeListViewItem item, Rect prevBoundsOfItem)
    {
        var listView = ListView!;

        var bottom = item.RightBottom.Y;
        var top = item.LeftTop.Y + listView.Padding.Top;
        var dy = bottom - prevBoundsOfItem.Bottom;

        var scrollY = listView.ScrollY;

        return dy == 0d || (top < (scrollY + listView.Height) && top > scrollY) ? 0d : dy;
    }

    protected virtual double GetItemOffsetToAdjustScrollHorizontal(VirtualizeListViewItem item, Rect prevBoundsOfItem)
    {
        var listView = ListView!;

        var right = item.RightBottom.X;
        var left = item.LeftTop.X + listView.Padding.Left;
        var dx = right - prevBoundsOfItem.Right;

        var scrollX = listView.ScrollX;

        return dx == 0d || (left < (scrollX + listView.Width) && left > scrollX) ? 0d : dx;
    }

    protected virtual double GetFullRowOffsetToAdjustScrollVertical(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevBoundsOfItem)
    {
        var listView = ListView!;

        var biggestBottom = GetBiggestBottomInRow(items, item);

        var top = item.LeftTop.Y + listView.Padding.Top;
        var dy = biggestBottom - prevBoundsOfItem.Bottom;

        var scrollY = listView.ScrollY;

        return dy == 0d || (top < (scrollY + listView.Height) && top > scrollY) ? 0d : dy;
    }

    protected virtual double GetFullColumnOffsetToAdjustScrollHorizontal(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevBoundsOfItem)
    {
        var listView = ListView!;

        var biggestRight = GetBiggestRightInColumn(items, item);

        var left = item.LeftTop.X + listView.Padding.Left;
        var dx = biggestRight - prevBoundsOfItem.Right;

        var scrollX = listView.ScrollX;

        return dx == 0d || (left < (scrollX + listView.Width) && left > scrollX) ? 0d : dx;
    }

    protected override Size LayoutManagerMeasure(double widthConstraint, double heightConstraint)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both) return new();

        var needsAdjustScroll = NeedsAdjustScroll;
        var items = LaidOutItems;
        var span = CollectionsMarshal.AsSpan(LaidOutItems);
        var length = span.Length;
        var listView = ListView!;
        var padding = listView.Padding;
        var availableSpace = AvailableSpace;
        var viewport = Viewport;
        var prevViewport = PrevViewport;

        double totalOffsetToAdjustScrollX = 0d, totalOffsetToAdjustScrollY = 0d;
        Size desiredSize;

        if (orientation is ScrollOrientation.Vertical)
        {
            var availableWidth = availableSpace.Width;

            Func<VirtualizeListViewItem, Rect, double> offsetFunc = needsAdjustScroll
                ? GetItemOffsetToAdjustScrollVertical
                : static (item, prevBounds) => 0d;

            double? startY = null;
            for (int i = 0; i < length;)
            {
                double maxOffsetY = 0d;

                var itemsCountInRow = GetItemsCountInRow(items, i);

                var slice = span.Slice(i, itemsCountInRow);

                i += itemsCountInRow;

                bool allVisible = true;
                int j;

                for (j = 0; j < itemsCountInRow; j++)
                {
                    var item = slice[j];
                    if (!BeforeItemMeasure(item, viewport))
                    {
                        allVisible = false;
                        continue;
                    }

                    var cell = item.Cell!;
                    var iView = cell as IView;

                    var availableItemWidth = GetEstimatedItemSizeVertical(item, availableSpace).Width;

                    item.MeasuredSize = iView!.Measure(availableItemWidth, double.PositiveInfinity);
                }

                if (!allVisible)
                {
                    for (j = 0; j < itemsCountInRow; j++)
                    {
                        var item = slice[j];
                        startY = AnimateItemVertically(item, allVisible, viewport, prevViewport, startY);
                        item.PrevBounds = item.Bounds;
                    }
                    continue;
                }

                double maxHeight = 0d;
                for (j = 0; j < itemsCountInRow; j++)
                {
                    maxHeight = Math.Max(maxHeight, slice[j].MeasuredSize.Height);
                }

                for (j = 0; j < itemsCountInRow; j++)
                {
                    var item = slice[j];

                    try
                    {
                        var prevBounds = item.Bounds;

                        item.Size = new(item.MeasuredSize.Width, maxHeight);

                        if (item.Bounds == prevBounds) continue;

                        ShiftItemsConsecutivelyVertical(items, item.Position + 1, length);

                        maxOffsetY = Math.Max(maxOffsetY, offsetFunc(item, prevBounds));
                    }
                    finally
                    {
                        startY = AnimateItemVertically(item, allVisible, viewport, prevViewport, startY);
                        item.PrevBounds = item.Bounds;
                    }
                }

                totalOffsetToAdjustScrollY += maxOffsetY;
            }
            desiredSize = new(Math.Min(widthConstraint, availableWidth), length == 0 ? 0 : items[^1].RightBottomWithMargin.Y);
        }
        else
        {
            var availableHeight = availableSpace.Height;

            Func<VirtualizeListViewItem, Rect, double> offsetFunc = needsAdjustScroll
                ? GetItemOffsetToAdjustScrollHorizontal
                : static (item, prevBounds) => 0d;

            for (int i = 0; i < length;)
            {
                double maxOffsetX = 0d;

                var itemsCountInColumn = GetItemsCountInColumn(items, i);

                var slice = span.Slice(i, itemsCountInColumn);
                for (int j = 0; j < slice.Length; j++)
                {
                    var item = slice[j];
                    if (!BeforeItemMeasure(item, viewport)) continue;

                    var cell = item.Cell!;
                    var iView = cell as IView;

                    var availableItemHeight = GetEstimatedItemSizeHorizontal(item, availableSpace).Height;

                    item.MeasuredSize = iView!.Measure(double.PositiveInfinity, availableItemHeight);
                }

                double maxWidth = 0d;
                for (int j = 0; j < slice.Length; j++)
                {
                    maxWidth = Math.Max(maxWidth, slice[j].MeasuredSize.Width);
                }

                var finishIndex = i + itemsCountInColumn;
                for (; i < finishIndex; i++)
                {
                    var item = items[i];

                    var prevBounds = item.Bounds;

                    item.Size = new(maxWidth, item.MeasuredSize.Height);

                    if (item.Bounds == prevBounds) continue;

                    ShiftItemsConsecutivelyHorizontal(items, item.Position + 1, length);

                    maxOffsetX = Math.Max(maxOffsetX, offsetFunc(item, prevBounds));
                }
                totalOffsetToAdjustScrollX += maxOffsetX;
            }
            desiredSize = new(length == 0 ? 0 : items[^1].RightBottomWithMargin.X, Math.Min(heightConstraint, availableHeight));
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
        try
        {
            switch (item.State)
            {
                case ItemState.IsNew when item.Cell is CellHolder cell:
                    {
                        item.AddFadeInAnimation(OpacityAnimationDuration, ZeroAnimationDelay);
                        item.Animate<OpacityAnimation>(1d);

                        return (startY is null) ? item.LeftTop.Y : startY;
                    }
                case ItemState.IsInserted when item.Cell is CellHolder cell:
                    {
                        //var dY = viewport.Y - prevViewport.Y;
                        //if (dY != 0) cell.TranslationY = dY;

                        //var dX = item.PrevBounds.X - item.LeftTop.X;
                        //if (dX != 0) cell.TranslationX = dX;

                        item.AddFadeInAnimation(OpacityAnimationDuration, DefaultAnimationDelay);
                        item.Animate<OpacityAnimation>(1d);

                        return (startY is null) ? item.LeftTop.Y : startY;
                    }
                case ItemState.ShouldBeShiftedOnInsert:
                    {
                        Action detachAction;

                        var wasOnScreen = WasOnScreen(item, viewport);
                        if (isOnScreen)
                        {
                            detachAction = () => { };
                            if (!wasOnScreen)
                            {
                                item.PrevBounds = new(new(item.PrevBounds.X, item.LeftTop.Y - item.Margin.Top - item.Size.Height),
                                    item.Size);
                            }
                        }
                        else
                        {
                            if (!wasOnScreen) break;
                            detachAction = () => DetachCell(item);
                        }

                        item.AddTranslationYAnimation(item.PrevBounds.Y, ShiftAnimationDuration, ZeroAnimationDelay);
                        item.AddTranslationXAnimation(item.PrevBounds.X, ShiftAnimationDuration, ZeroAnimationDelay);
                        item.Animate<TranslationXAnimation>(item.LeftTop.X, detachAction);
                        double endY = item.LeftTop.Y;
                        if (startY is double sY && !isOnScreen && wasOnScreen)
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

                    }
                    break;
                case ItemState.ShouldBeRemoved:
                    {
                        if (isOnScreen) DetachCell(item);
                    }
                    break;
                case ItemState.Idle:
                default:
                    break;
            }
        }
        finally
        {
            item.State = ItemState.Idle;
        }

        return startY;
    }

    public override Size ArrangeChildren(Rect bounds)
    {
        var orientation = GetOrientation();

        if (orientation is ScrollOrientation.Both) return bounds.Size;

        foreach (var item in VisibleItems.OrderBy(static i => i.Position))
        {
            var cell = item.Cell!;
            var iView = cell as IView;

            Rect newBounds = item.Bounds;

#if MACIOS
            if (newBounds == cell.Bounds) continue;
#endif
            iView!.Arrange(newBounds);
        }

        return new(bounds.Width, bounds.Height);
    }

    //protected override void OnRemovedLeadingVisibleItems(List<(VirtualizeListViewItem item, Rect prevBounds)> itemsToShift, List<VirtualizeListViewItem> removedItems, Rect prevViewport, double scrollDeltaX, double scrollDeltaY)
    //{
    //    throw new NotImplementedException();
    //}

    //protected override void OnRemovedMiddleVisibleItems(List<(VirtualizeListViewItem item, Rect prevBounds)> itemsToShift, List<VirtualizeListViewItem> removedItems, Rect prevViewport, double scrollDeltaX, double scrollDeltaY)
    //{
    //    throw new NotImplementedException();
    //}

    //protected override void OnRemovedAllVisibleItems(List<(VirtualizeListViewItem item, Rect prevBounds)> itemsToShift, List<VirtualizeListViewItem> removedItems, Rect prevViewport, double scrollDeltaX, double scrollDeltaY)
    //{
    //    throw new NotImplementedException();
    //}

    //protected override void OnRemovedTrailingVisibleItems(List<(VirtualizeListViewItem item, Rect prevBounds)> itemsToShift, List<VirtualizeListViewItem> removedItems, Rect prevViewport, double scrollDeltaX, double scrollDeltaY)
    //{
    //    throw new NotImplementedException();
    //}

    protected override Size MeasureItem(VirtualizeListViewItem item, Rect viewport, Size availableSpace)
    {
        var adapter = Adapter!;

        var cell = ReuseCell(item, viewport);
        adapter.BindCell(cell, item.AdapterItem!, item.Position);

        var availableItemWidth = GetEstimatedItemSizeVertical(item, availableSpace).Width;

        var iView = cell as IView;
        var measure = iView!.Measure(availableItemWidth, double.PositiveInfinity);

        adapter.UnbindCell(cell, item.AdapterItem!, item.Position);
        CacheCell(item);

        return measure;
    }
}