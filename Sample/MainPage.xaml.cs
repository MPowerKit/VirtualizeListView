using CommunityToolkit.Mvvm.ComponentModel;

using MPowerKit.VirtualizeListView;

namespace Sample;

public class ItemsGroup : ObservableRangeCollection<Item>
{
    public int Key { get; set; }
    public ItemsGroup(IEnumerable<Item> items, int key) : base(items)
    {
        Key = key;
    }
}

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
    [ObservableProperty]
    private double _height;
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
                Title = $"{i}",
                Description = $"This is the long description for {i}",
                Image = "https://picsum.photos/600",
                Height = 50
            });
        }

        listView.ItemsSource = items.GroupBy(i => (int)(i.Id / 10.0)).Select(g => new ItemsGroup(g, g.Key));
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
                Description = $"This is the long description for {i + 500}",
                Image = "https://picsum.photos/600",
                Height = 50
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
                Image = "https://picsum.photos/600",
                Height = 50
            });
        }

        source.ReplaceRange(1, 10, list);
    }

    private void Button_Clicked_Move(object sender, EventArgs e)
    {
        var source = listView.ItemsSource as ObservableRangeCollection<Item>;
        source.Move(1, 10);
    }

    private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        var item = (sender as View).BindingContext as Item;
        item.Description += item.Description;
    }

    private readonly Random _rnd = new();
    private readonly Random _heightRnd = new();

    private void Entry_TextChanged(object sender, TextChangedEventArgs e)
    {
        var items = new ObservableRangeCollection<Item>();
        if (!string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            var count = _rnd.Next(10, 50);
            for (var i = 0; i < count; i++)
                items.Add(new Item
                {
                    Title = $"Item #{i}",
                    Description = e.NewTextValue,
                    Height = _heightRnd.Next(50, 100),
                    Image = "https://picsum.photos/600"
                });
        }

        listView.ItemsSource = items;
    }
}