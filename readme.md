# MPowerKit.VirtualizeListView

#### MAUI Virtualize ListView with smooth scrolling and without platform-specific code

[![Nuget](https://img.shields.io/nuget/v/MPowerKit.VirtualizeListView)](https://www.nuget.org/packages/MPowerKit.VirtualizeListView)

It's not a secret to anybody, that ```ListView``` and ```CollectionView``` in MAUI have bad scrolling performance. This project started as an experimantal project POC to find a way to make a virtualize listview using ```ScrollView``` and ```AbsoluteLayout``` only. Suprisingly, the scrolling performance is way better, than was expected. So you can check it right now.

**It works on all platforms MAUI supports and it has the same behavior on all platforms, because it does not have platform-specific code.**

So, under the hood it is a ```ScrollView``` with custom ```typeof(Layout)``` as items layout. The main idea of virtualization for this listview is to change Translation (virtual position, but on Android it is still physical chage because of [#5](https://github.com/MPowerKit/VirtualizeListView/issues/5)) of items while keeping them attached to the layout. ```VirtualizeItemsLayoutManger``` is responsible for virtualization process and for creating / removing views and it will not create more views as necessary.

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
- [x] Scroll speed
- [x] Cache pool
- [x] Item tap handling
- [x] Item resize handling
- [x] Add, Remove, Replace, Move, Reset operations on collection
- [ ] Drag and drop (reordering)
- [ ] Sticky headers
- [ ] Animated add/remove/move operations

# Tips for better scrolling user experience

- Bindings - your enemy. Try to avoid bindings in your item templates or do ```OneTime``` where you can. But if you need them, use only compiled bindings or better [levitali/CompiledBindings](https://github.com/levitali/CompiledBindings) library.
- Try to avoid using triggers, they are slow.
- Use as less templates as possible. The more templates you have, the more time it takes to create a view.
- Use ```InitialCachePoolSize``` to set up the number of initial views to keep in cache. Default size is 4. The bigger value - the more cached views it creates during initialization. And if you have a lot of templates it can take some time to initialize pool for each template. So in this case just manipulate with the ```InitialCachePoolSize```. If you have only 1 template, you can set it to 2, it will be enough.
- Create separate view for ```DataTemplate``` and replace bindings where you can by manual setting the values from the code behind of that view.
- Use as less layouts as possible. Use only ```ContentView```, ```Grid```, ```VerticalStackLayout``` or ```HorizontalStackLayout```. Try to avoid using ```Border``` or ```Frame```, better create ```RoundBorderEffect```.
- Try to avoid using ```Shadow``` property in your templates. It can slow down the rendering process.
- Set the direct dimensions for your controls where you can in the template, especially for ```Image```.
- If you have very complex layout and the ```VirtualizeListView``` still not smooth and laggy, you can try to decrease ```ScrollSpeed```, it may help in some cases. (MAUI does a lot of redundant work remeasuring and relayouting views few times).

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
		<mpowerkit:LinearLayout ItemSpacing="15" 
		                        InitialCachePoolSize="4" /> <!-- InitialCachePoolSize is optional, default value is 4 -->
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

Also, this package contains ```ObservableRangeCollection``` which is an ```ObservableCollection```, but it has a bunch of useful methods to manipulate the collection with batch updates. Recommended to use with ```VirtualizeListView```. It provides few methods: ```AddRange```, ```InsertRange```, ```RemoveRange```, ```ReplaceRange```.

## Known issues

- In debug mode it can have bad scrolling performance, especially on Windows, but in release mode it works surprisingly very well.