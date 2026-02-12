using static MPowerKit.VirtualizeListView.DataAdapter;

namespace MPowerKit.VirtualizeListView;

public enum ItemState : ushort
{
    IsNew,
    IsInserted,
    Idle,
    ShouldBeShiftedOnInsert,
    ShouldBeShiftedOnRemove,
    ShouldBeRemoved
}

public class VirtualizeListViewItem
{
    public event EventHandler<CellHolder?> OnCellAttached;

    private CellHolder? _cell;

    protected VirtualizeItemsLayoutManager LayoutManager { get; set; }
    public Guid Id { get; }

    public override string ToString()
    {
        return $"Position={Position}, Bounds={Bounds}, Data={AdapterItem?.Data}";
    }

    public VirtualizeListViewItem(VirtualizeItemsLayoutManager layoutManager)
    {
        LayoutManager = layoutManager;
        Id = Guid.CreateVersion7();
    }

    public int Position { get; set; } = -1;
    public bool IsAttached => Cell?.Attached ?? false;
    public DataTemplate? Template { get; set; }
    public AdapterItem? AdapterItem { get; set; }
    public ItemState State { get; set; } = ItemState.IsNew;
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

            OnCellAttached?.Invoke(this, value);
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
    public Size SizeWithMargin
    {
        get
        {
            var size = Size;
            var margin = Margin;
            return new(size.Width + margin.HorizontalThickness, size.Height + margin.VerticalThickness);
        }
    }
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
    public Rect PrevBounds { get; set; }
    //public bool WasMoved => !PrevBounds.Equals(Bounds);
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

    #region Animations

    protected List<ItemAnimation> PendingAnimations = new(3);
    protected List<ItemAnimation> AnimatingAnimations = new(3);

    protected int OpacityAnimationsCount;
    protected int TranslationAnimationsCount;

    public bool PreTranslationAnimation { get; set; }
    public bool AnyOpacityAnimation => OpacityAnimationsCount > 0;
    public bool AnyTranslationAnimation => TranslationAnimationsCount > 0;
    public bool AnyAnimation => AnyOpacityAnimation || AnyTranslationAnimation;

    public bool AnyPendingAnimation => PendingAnimations.Count > 0;
    public bool AnyAnimatingAnimation => AnimatingAnimations.Count > 0;

    public async Task AnimateAll(Action? finishCallback = null)
    {
        List<Task<ItemAnimation>> animationTasks = new(3);

        var animations = PendingAnimations.ToArray();
        lock (PendingAnimations)
        {
            PendingAnimations.Clear();
        }

        foreach (var animation in animations)
        {
            lock (AnimatingAnimations)
            {
                AnimatingAnimations.Add(animation);
            }

            switch (animation)
            {
                case FadeInAnimation:
                    animationTasks.Add(animation.Animate(1d));
                    break;
                case FadeOutAnimation:
                    animationTasks.Add(animation.Animate(0d));
                    break;
                case TranslationXAnimation:
                    animationTasks.Add(animation.Animate(this.LeftTop.X));
                    break;
                case TranslationYAnimation:
                    animationTasks.Add(animation.Animate(this.LeftTop.Y));
                    break;
            }
        }

        if (animationTasks.Count != 0)
        {
            await foreach (var task in Task.WhenEach(animationTasks))
            {
                var animation = await task;

                lock (AnimatingAnimations)
                {
                    AnimatingAnimations.Remove(animation);
                    switch (animation)
                    {
                        case OpacityAnimation:
                            OpacityAnimationsCount--;
                            break;
                        case TranslationAnimation:
                            TranslationAnimationsCount--;
                            break;
                    }
                }
            }
        }

        if (finishCallback is not null)
        {
            LayoutManager.Dispatcher.Dispatch(() =>
            {
                finishCallback();
            });
        }
    }

    public async Task Animate<T>(double currentValue, Action? finishCallback = null)
       where T : ItemAnimation
    {
        List<Task<ItemAnimation>> animationTasks = new(1);

        var animations = PendingAnimations.ToArray();
        lock (PendingAnimations)
        {
            foreach (var animation in animations)
            {
                lock (AnimatingAnimations)
                {
                    AnimatingAnimations.Add(animation);
                }

                switch (animation)
                {
                    case T:
                        animationTasks.Add(animation.Animate(currentValue));
                        PendingAnimations.Remove(animation);
                        break;
                }
            }
        }

        if (animationTasks.Count != 0)
        {
            await foreach (var task in Task.WhenEach(animationTasks))
            {
                var animation = await task;

                lock (AnimatingAnimations)
                {
                    AnimatingAnimations.Remove(animation);
                    switch (animation)
                    {
                        case OpacityAnimation:
                            OpacityAnimationsCount--;
                            break;
                        case TranslationAnimation:
                            TranslationAnimationsCount--;
                            break;
                    }
                }
            }
        }

        if (finishCallback is not null)
        {
            LayoutManager.Dispatcher.Dispatch(() =>
            {
                finishCallback();
            });
        }
    }

    public void AddFadeInAnimation(TimeSpan duration, TimeSpan delay)
    {
        lock (PendingAnimations)
        {
            PendingAnimations.Add(new FadeInAnimation(
                LayoutManager,
                this,
                duration,
                delay));
            OpacityAnimationsCount++;
        }
    }

    public void AddFadeOutAnimation(TimeSpan duration, TimeSpan delay)
    {
        lock (PendingAnimations)
        {
            PendingAnimations.Add(new FadeOutAnimation(
                LayoutManager,
                this,
                duration,
                delay));
            OpacityAnimationsCount++;
        }
    }

    public void AddTranslationXAnimation(double previousRealX, TimeSpan duration, TimeSpan delay)
    {
        lock (PendingAnimations)
        {
            PendingAnimations.Add(new TranslationXAnimation(
                LayoutManager,
                this,
                duration,
                previousRealX,
                delay));
            TranslationAnimationsCount++;
        }
    }

    public void AddTranslationYAnimation(double previousRealY, TimeSpan duration, TimeSpan delay)
    {
        lock (PendingAnimations)
        {
            PendingAnimations.Add(new TranslationYAnimation(
                LayoutManager,
                this,
                duration,
                previousRealY,
                delay));
            TranslationAnimationsCount++;
        }
    }

    public class ItemAnimation
    {
        protected readonly VirtualizeItemsLayoutManager _layoutManager;
        protected readonly VirtualizeListViewItem _item;
        protected readonly TimeSpan _duration;
        protected readonly string _name;
        protected readonly Action<double> _animateAction;
        protected double _start;
        protected readonly Action<double, bool> _finished;
        protected readonly TimeSpan _delay;

        public ItemAnimation(
            VirtualizeItemsLayoutManager layoutManager,
            VirtualizeListViewItem item,
            string name,
            TimeSpan duration,
            Action<double> animateAction,
            double start,
            Action<double, bool> finished,
            TimeSpan delay)
        {
            _layoutManager = layoutManager;
            _item = item;
            _duration = duration;
            _name = name + item.Id;
            _animateAction = animateAction;
            _start = start;
            _finished = finished;
            _delay = delay;
        }

        public virtual Task<ItemAnimation> Animate(double end)
        {
            if (_start - end == 0)
            {
                _finished(end, true);
                return Task.FromResult(this);
            }

            var tcs = new TaskCompletionSource<ItemAnimation>();

            _layoutManager.Dispatcher.DispatchDelayed(_delay, () =>
            {
                _layoutManager.Animate(_name, _animateAction, start: _start, end: end, length: (uint)_duration.TotalMilliseconds, finished: (v, a) =>
                {
                    _finished(v, a);
                    tcs.SetResult(this);
                });
            });

            return tcs.Task;
        }
    }

    public class OpacityAnimation : ItemAnimation
    {
        public OpacityAnimation(
            VirtualizeItemsLayoutManager layoutManager,
            VirtualizeListViewItem item,
            TimeSpan duration,
            double start,
            TimeSpan delay)
            : base(
                layoutManager,
                item,
                "OpacityAnimation",
                duration,
                v =>
                {
                    if (item.Cell is not null)
                    {
                        item.Cell.Opacity = v;
                    }
                },
                start,
                (v, a) =>
                {
                    //if (item.Cell is not null)
                    //{
                    //    item.Cell.Opacity = 1d;
                    //}
                },
                delay)
        {
        }
    }

    public class FadeInAnimation : OpacityAnimation
    {
        public FadeInAnimation(
            VirtualizeItemsLayoutManager layoutManager,
            VirtualizeListViewItem item,
            TimeSpan duration,
            TimeSpan delay)
            : base(
                layoutManager,
                item,
                duration,
                0d,
                delay)
        {
        }

        public override Task<ItemAnimation> Animate(double end)
        {
            if (_item.Cell is not null) _item.Cell.Opacity = 0d;
            _layoutManager.Dispatcher.Dispatch(() =>
            {
                if (_item.Cell is not null) _item.Cell.Opacity = 0d;
            });
            return base.Animate(1d);
        }
    }

    public class FadeOutAnimation : OpacityAnimation
    {
        public FadeOutAnimation(
            VirtualizeItemsLayoutManager layoutManager,
            VirtualizeListViewItem item,
            TimeSpan duration,
            TimeSpan delay)
            : base(
                layoutManager,
                item,
                duration,
                1d,
                delay)
        {
        }

        public override Task<ItemAnimation> Animate(double end)
        {
            if (_item.Cell is not null) _item.Cell.Opacity = 1d;
            _layoutManager.Dispatcher.Dispatch(() =>
            {
                if (_item.Cell is not null) _item.Cell.Opacity = 1d;
            });
            return base.Animate(0d);
        }
    }

    public class TranslationAnimation : ItemAnimation
    {
        public TranslationAnimation(
            VirtualizeItemsLayoutManager layoutManager,
            VirtualizeListViewItem item,
            string name,
            TimeSpan duration,
            Action<double> animateAction,
            double startCoord,
            Action<double, bool> finished,
            TimeSpan delay)
            : base(
                layoutManager,
                item,
                name,
                duration,
                animateAction,
                startCoord,
                finished,
                delay)
        {
        }

        public override async Task<ItemAnimation> Animate(double endCoord)
        {
            var start = _start - endCoord;

            if (start == 0d)
            {
                _finished(0d, true);
                return this;
            }

            var tcs = new TaskCompletionSource<ItemAnimation>();

            _layoutManager.Dispatcher.DispatchDelayed(_delay, () =>
            {
                _layoutManager.Animate(_name, _animateAction, start: _start, end: endCoord, length: (uint)_duration.TotalMilliseconds, finished: (v, a) =>
                {
                    _finished(v, a);
                    tcs.SetResult(this);
                });
            });

            return await tcs.Task;
        }
    }

    public class TranslationXAnimation : TranslationAnimation
    {
        public TranslationXAnimation(
            VirtualizeItemsLayoutManager layoutManager,
            VirtualizeListViewItem item,
            TimeSpan duration,
            double startX,
            TimeSpan delay)
            : base(
                layoutManager,
                item,
                "TranslationX",
                duration,
                v =>
                {
                    if (item.Cell is not null)
                    {
                        item.Cell.TranslationX = v;
                    }
                },
                startX,
                (v, a) =>
                {
                    if (item.Cell is not null)
                    {
                        item.Cell.TranslationX = 0d;
                    }
                },
                delay)
        {
        }

        public override Task<ItemAnimation> Animate(double endX)
        {
            _start -= _item.LeftTop.X;

            if (_item.Cell is not null)
                _item.Cell.TranslationX = _start;
            _layoutManager.Dispatcher.Dispatch(() =>
            {
                if (_item.Cell is not null)
                    _item.Cell.TranslationX = _start;
            });
            return base.Animate(endX - _item.LeftTop.X);
        }
    }

    public class TranslationYAnimation : TranslationAnimation
    {
        public TranslationYAnimation(
            VirtualizeItemsLayoutManager layoutManager,
            VirtualizeListViewItem item,
            TimeSpan duration,
            double startY,
            TimeSpan delay)
            : base(
                layoutManager,
                item,
                "TranslationY",
                duration,
                v =>
                {
                    if (item.Cell is not null)
                    {
                        item.Cell.TranslationY = v;
                    }
                },
                startY,
                (v, a) =>
                {
                    if (item.Cell is not null)
                    {
                        item.Cell.TranslationY = 0d;
                    }
                },
                delay)
        {
        }

        public override Task<ItemAnimation> Animate(double endY)
        {
            _start -= _item.LeftTop.Y;

            if (_item.Cell is not null)
                _item.Cell.TranslationY = _start;
            _layoutManager.Dispatcher.Dispatch(() =>
            {
                if (_item.Cell is not null)
                    _item.Cell.TranslationY = _start;
            });
            return base.Animate(endY - _item.LeftTop.Y);
        }
    }

    #endregion
}