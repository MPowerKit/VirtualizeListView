using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MPowerKit.VirtualizeListView;

public class FixedRefreshView : RefreshView
{
    public new event EventHandler? Refreshing;

    protected bool PrevEnabled { get; set; }
    protected bool PrevPullRefreshEnabled { get; set; }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

#if ANDROID || IOS
        //(Content as IView).Arrange(new Rect(Padding.Left, Padding.Top, width - Padding.HorizontalThickness, height - Padding.VerticalThickness));
#endif
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        if (this.Content is not null)
        {
            var padding = this.Padding;
            var margin = this.Margin;

            (this.Content as IView)!.Measure(
                widthConstraint - padding.HorizontalThickness - margin.HorizontalThickness,
                heightConstraint - padding.VerticalThickness - margin.VerticalThickness);
        }

        return base.MeasureOverride(widthConstraint, heightConstraint);
    }

    protected override Size ArrangeOverride(Rect bounds)
    {
        var size = base.ArrangeOverride(bounds);

        if (this.Content is not null)
        {
            var measure = this.Content.DesiredSize;
            var padding = this.Padding;

            (this.Content as IView).Arrange(new(new(padding.Left, padding.Top), measure));
        }

        return size;
    }

    protected virtual void Refresh()
    {
        Refreshing?.Invoke(this, EventArgs.Empty);
        if (Command?.CanExecute(null) is not true) return;
        Command.Execute(null);
    }

    protected virtual void EndRefreshing()
    {
        if (!IsRefreshing) return;

        IsRefreshing = false;
    }

    protected virtual void UpdateEnabled()
    {
        if (IsRefreshing)
        {
            PrevPullRefreshEnabled = IsPullToRefreshEnabled;
            IsPullToRefreshEnabled = false;
            PrevEnabled = IsEnabled;
            IsEnabled = false;
        }
        else
        {
            IsPullToRefreshEnabled = PrevPullRefreshEnabled;
            IsEnabled = PrevEnabled;
        }
    }

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == IsRefreshingProperty.PropertyName)
        {
            if (IsRefreshing && IsPullToRefreshEnabled && IsEnabled)
            {
                Refresh();
            }
            else EndRefreshing();

            UpdateEnabled();
        }
    }

    #region IsRefreshing
    public new bool IsRefreshing
    {
        get => (bool)GetValue(IsRefreshingProperty);
        set { SetValue(IsRefreshingProperty, value); }
    }

    public static readonly new BindableProperty IsRefreshingProperty =
            BindableProperty.Create(
                nameof(IsRefreshing),
                typeof(bool),
                typeof(FixedRefreshView));
    #endregion

    #region Command
    public new ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set { SetValue(CommandProperty, value); }
    }

    public static readonly new BindableProperty CommandProperty =
            BindableProperty.Create(
                nameof(Command),
                typeof(ICommand),
                typeof(FixedRefreshView));
    #endregion

    #region IsPullToRefreshEnabled
    public bool IsPullToRefreshEnabled
    {
        get { return (bool)GetValue(IsPullToRefreshEnabledProperty); }
        set { SetValue(IsPullToRefreshEnabledProperty, value); }
    }

    public static readonly BindableProperty IsPullToRefreshEnabledProperty =
        BindableProperty.Create(
            nameof(IsPullToRefreshEnabled),
            typeof(bool),
            typeof(FixedRefreshView),
            true);
    #endregion
}