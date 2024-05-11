namespace MPowerKit.VirtualizeListView;

public class VirtualizeListViewCell : ContentView
{
    public VirtualizeListViewCell()
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += Tap_Tapped;
        this.GestureRecognizers.Add(tap);
    }

    private void Tap_Tapped(object? sender, TappedEventArgs e)
    {
        if (this.BindingContext is null) return;

        var listview = this.FindParentOfType<VirtualizeListView>();

        if (listview is null) return;

        listview.OnItemTapped(this.BindingContext);
    }

    public void SendAppearing()
    {
        OnAppearing();
    }

    public void SendDisappearing()
    {
        OnDisappearing();
    }

    protected virtual void OnAppearing()
    {

    }

    protected virtual void OnDisappearing()
    {

    }
}