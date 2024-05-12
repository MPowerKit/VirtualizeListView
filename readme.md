# MPowerKit.VirtualizeListView

#### MAUI Virtualize ListView with smooth scrolling and without platform-specific code

[![Nuget](https://img.shields.io/nuget/v/MPowerKit.VirtualizeListView)](https://www.nuget.org/packages/MPowerKit.VirtualizeListView)

It's not a secret to anybody, that ```ListView``` and ```CollectionView``` in MAUI have bad scrolling performance. This project started as an experimantal project to find a way to make a virtualize listview using ```ScrollView``` and ```AbsoluteLayout``` only. Suprisingly, the scrolling performance is way better, than was expected. So you can check it right now.

**It works on all platforms MAUI supports and it has the same behavior on all platforms, because it does not have platform-specific code.**

So, under the hood it is a ```ScrollView``` with ```AbsoluteLayout``` as items layout. The main idea of virtualization for this listview is to change Translation of items while keeping them attached to the ```AbsoluteLayout```. ```ItemsLayout``` is responsible for virtualization process and for creating / removing views and it will not create more views as necessary.

**Your PRs are welcome!**

| Android | Windows |
|-|-|
| <video src="https://github.com/MPowerKit/VirtualizeListView/assets/23138430/edd8aa08-3a6a-404f-9e95-28343b13498f" controls="controls" width="287" height="630"/> | <video src="https://github.com/MPowerKit/VirtualizeListView/assets/23138430/e9b6715a-7f56-4f23-b757-9c18c6597fad" controls="controls" width="426" height="240"/> |

### Implemented features:

- [x] Linear items layout
- [ ] Grid items layout
- [x] Items spacing
- [x] Virtualization
- [x] Grouping
- [x] Collection padding
- [x] Scroll orientation (collection orientation)
- [x] Disable scrolling
- [x] Header / Footer
- [x] Load more
- [x] Item tap handling
- [x] Item resize handling
- [x] Add, Remove, Replace, Move, Reset operations on collection
- [ ] Drag and drop (reordering)
- [ ] Sticky headers
- [ ] Animated add/remove/move operations

### Usage

Add ```UseMPowerKitListView()``` to your ```MauiProgram.cs``` file as next

```csharp
builder
    .UseMauiApp<App>()
    .UseMPowerKitListView();
```

and in your xaml just use as a regular ```CollectionView```.

**Note: The root of the ItemTemplate has to be ```typeof(VirtualizeListViewCell)```, but group/header/footer templates are not allowed to have root of this type.**

To change items spacing you need to reset the ```ItemsLayout``` property as next:

```xaml
<mpowerkit:VirtualizeListView>
	<mpowerkit:VirtualizeListView.ItemsLayout>
		<mpowerkit:LinearLayout ItemSpacing="15" />
	</mpowerkit:VirtualizeListView.ItemsLayout>
</mpowerkit:VirtualizeListView>
```

By default it has zero spacing.

To change collection orientation just change the ```Orientation``` property of the ```VirtualizeListView``` as  you would do this in ```ScrollView```.

To disable scroll set ```CanScroll``` property to ```false```, and do not change ```Orientation``` property to ```Neither```.

## Other useful features

#### ```FixedRefreshView```

This package brings to you fixed MAUI's RefreshView as ```FixedRefreshView```. Here, you can disable refreshing without disabling entire collection. For this you may use ```IsPullToRefreshEnabled```.

#### ```ObservableRangeCollection```

Also, this package contains ```ObservableRangeCollection``` which is a ```ObservableCollection```, but it has a bunch of useful methods to manipulate the collection with batch updates. Recommended to use with ```VirtualizeListView```. It provides few methods: ```AddRange```, ```InsertRange```, ```RemoveRange```, ```ReplaceRange```.

## Known issues

- In debug mode it can have bad scrolling performance, especially on Windows, but in release mode it works surprisingly very well.
