namespace MPowerKit.VirtualizeListView;

public class ScrollEventArgs : ScrolledEventArgs
{
    public ScrollEventArgs(double x, double y, double dx, double dy) : base(x, y)
    {
        Dx = dx;
        Dy = dy;
    }

    public double Dx { get; set; }
    public double Dy { get; set; }

    public static ScrollEventArgs operator -(ScrolledEventArgs newArgs, ScrollEventArgs oldArgs)
    {
        return new(newArgs.ScrollX, newArgs.ScrollY, newArgs.ScrollX - oldArgs.ScrollX, newArgs.ScrollY - oldArgs.ScrollY);
    }
}