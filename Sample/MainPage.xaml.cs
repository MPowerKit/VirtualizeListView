using CommunityToolkit.Mvvm.ComponentModel;

using MPowerKit.VirtualizeListView;

namespace Sample;

public partial class Item : ObservableObject
{
    [ObservableProperty]
    private int _id;
    [ObservableProperty]
    private string _image;
    [ObservableProperty]
    private string _title;
    [ObservableProperty]
    private string _description;
}

public partial class MainPage
{
    public MainPage()
    {
        InitializeComponent();

        FillItems();
    }

    private void FillItems()
    {
        var items = new ObservableRangeCollection<Item>();
        for (int i = 0; i < 500; i++)
        {
            items.Add(new Item()
            {
                Id = i,
                Title = $"This is title for {i}",
                Description = $"This is the long description for {i}, alsdnhkjadfng askdsn kdag ndfnb ksdfhk adfkj ndkfg akfn bkjdfng kjdfhg kjadfnkjandf kgjankgj",
                //Image = $"https://picsum.photos/id/{i}/300/200"
            });
        }

        listView.ItemsSource = items/*.GroupBy(i => (int)(i.Id / 10.0))*/;
    }

    private async void FixedRefreshView_Refreshing(object sender, EventArgs e)
    {
        await Task.Delay(5000);

        FillItems();

        (sender as FixedRefreshView).IsRefreshing = false;
    }

    private void Button_Clicked_Add(object sender, EventArgs e)
    {
        var source = listView.ItemsSource as ObservableRangeCollection<Item>;

        ObservableRangeCollection<Item> list = [];
        for (int i = 0; i < 5; i++)
        {
            list.Add(new Item()
            {
                Id = i + 500,
                Title = $"This is title for {i + 500}",
                Description = $"This is the long description for {i + 500}, alsdnhkjadfng askdsn kdag ndfnb ksdfhk adfkj ndkfg akfn bkjdfng kjdfhg kjadfnkjandf kgjankgj",
                Image = $"https://picsum.photos/id/{i + 500}/300/200"
            });
        }

        source.InsertRange(1, list);
    }

    private void Button_Clicked_Remove(object sender, EventArgs e)
    {
        var source = listView.ItemsSource as ObservableRangeCollection<Item>;
        source.RemoveRange(1, 10);
    }

    private void Button_Clicked_Replace(object sender, EventArgs e)
    {
        var source = listView.ItemsSource as ObservableRangeCollection<Item>;

        ObservableRangeCollection<Item> list = [];
        for (int i = 0; i < 5; i++)
        {
            list.Add(new Item()
            {
                Id = i + 500,
                Title = $"This is title for {i + 500}",
                Description = $"This is the long description for {i + 500}, alsdnhkjadfng askdsn kdag ndfnb ksdfhk adfkj ndkfg akfn bkjdfng kjdfhg kjadfnkjandf kgjankgj",
                Image = $"https://picsum.photos/id/{i + 500}/300/200"
            });
        }

        source.ReplaceRange(1, 10, list);
    }

    private void Button_Clicked_Move(object sender, EventArgs e)
    {
        var source = listView.ItemsSource as ObservableRangeCollection<Item>;
        source.Move(1, 10);
    }
}