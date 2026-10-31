using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop.Controls;

/// <summary>One local hover popup per item surface; never changes selection.</summary>
public sealed class ItemHover
{
    public static readonly DependencyProperty ItemProperty=DependencyProperty.RegisterAttached(
        "Item",typeof(ItemViewModel),typeof(ItemHover),new PropertyMetadata(null,OnItemChanged));
    static readonly DependencyProperty ControllerProperty=DependencyProperty.RegisterAttached(
        "Controller",typeof(ItemHover),typeof(ItemHover),new PropertyMetadata(null));
    public static ItemViewModel? GetItem(DependencyObject owner)=>(ItemViewModel?)owner.GetValue(ItemProperty);
    public static void SetItem(DependencyObject owner,ItemViewModel? item)=>owner.SetValue(ItemProperty,item);
    internal static ItemHover? GetController(DependencyObject owner)=>(ItemHover?)owner.GetValue(ControllerProperty);
    static void OnItemChanged(DependencyObject target,DependencyPropertyChangedEventArgs e)
    {
        if(target is not FrameworkElement element)return;
        var controller=(ItemHover?)element.GetValue(ControllerProperty);
        if(controller is null&&e.NewValue is ItemViewModel)
        {
            controller=new ItemHover(element);element.SetValue(ControllerProperty,controller);
            element.AddHandler(UIElement.MouseMoveEvent,new MouseEventHandler(OnAttachedMouseMove),true);
        }
        controller?.Invalidate();
    }
    static void OnAttachedMouseMove(object sender,MouseEventArgs e)
    {
        if(sender is FrameworkElement element&&element.GetValue(ControllerProperty) is ItemHover controller)
            controller.Track(GetItem(element));
    }
    static WeakReference<ItemHover>? active;
    readonly FrameworkElement owner;
    readonly DispatcherTimer opening,leaving;
    readonly ToolTip tip;
    Window? window;
    bool detached;
    internal ItemViewModel? CurrentItem {get;private set;}
    internal ItemTooltipCard? Card {get;private set;}
    internal bool IsOpen=>tip.IsOpen;
    internal bool IsPending=>opening.IsEnabled;

    internal ItemHover(FrameworkElement owner)
    {
        this.owner=owner;
        tip=new ToolTip {PlacementTarget=owner,Placement=PlacementMode.MousePoint,
            HorizontalOffset=14,VerticalOffset=14,StaysOpen=true,Focusable=false,
            Padding=new Thickness(0),BorderThickness=new Thickness(0),Background=Brushes.Transparent,
            MaxWidth=double.PositiveInfinity,MaxHeight=double.PositiveInfinity};
        opening=new DispatcherTimer(DispatcherPriority.Input,owner.Dispatcher){Interval=TimeSpan.FromMilliseconds(300)};
        leaving=new DispatcherTimer(DispatcherPriority.Input,owner.Dispatcher){Interval=TimeSpan.FromMilliseconds(150)};
        opening.Tick+=OnOpening;leaving.Tick+=OnLeaving;
        owner.MouseEnter+=OnOwnerEnter;owner.MouseLeave+=OnOwnerLeave;
        owner.PreviewMouseDown+=OnMouseDown;owner.PreviewKeyDown+=OnKeyDown;
        owner.IsVisibleChanged+=OnAvailableChanged;owner.IsEnabledChanged+=OnAvailableChanged;
        owner.Unloaded+=OnUnloaded;owner.Loaded+=OnLoaded;owner.SizeChanged+=OnSizeChanged;
        tip.MouseEnter+=OnTipEnter;tip.MouseLeave+=OnTipLeave;tip.PreviewKeyDown+=OnKeyDown;
        AttachWindow();
    }

    internal void Track(ItemViewModel? item)
    {
        if(detached||item is null||!Available()){Close();return;}
        leaving.Stop();
        if(ReferenceEquals(CurrentItem,item))
        {if(Card is null&&!opening.IsEnabled)opening.Start();return;}
        bool immediate=IsOpen||Card is not null||active is not null&&active.TryGetTarget(out var shown)&&shown.IsOpen&&ReferenceEquals(shown.window,window);
        opening.Stop();CurrentItem=item;
        if(immediate)CompletePending();else opening.Start();
    }
    // Used by the real timer and by detached native smoke tests (no popup HWND).
    internal void CompletePending()
    {
        opening.Stop();
        if(detached||CurrentItem is null||!Available()){Close();return;}
        double scale=LayoutScale();
        tip.IsOpen=false;
        Card=new ItemTooltipCard(CurrentItem){LayoutTransform=new ScaleTransform(scale,scale),
            MaxHeight=Math.Min(620,Math.Max(160,(SystemParameters.WorkArea.Height-32)/scale))};
        tip.Content=Card;
        // Rendering/tests may prepare the exact card without presenting a window.
        if(PresentationSource.FromVisual(owner) is not null)
        {
            if(active is not null&&active.TryGetTarget(out var previous)&&!ReferenceEquals(previous,this))previous.Close();
            tip.IsOpen=true;active=new WeakReference<ItemHover>(this);
        }
    }
    internal void Close()
    {
        Card=null;CurrentItem=null;tip.IsOpen=false;tip.Content=null;opening.Stop();leaving.Stop();
        if(active is not null&&active.TryGetTarget(out var current)&&ReferenceEquals(current,this))active=null;
    }
    internal void Invalidate()=>Close();
    internal void Detach()
    {
        if(detached)return;
        Close();detached=true;DetachWindow();
        opening.Tick-=OnOpening;leaving.Tick-=OnLeaving;
        owner.MouseEnter-=OnOwnerEnter;owner.MouseLeave-=OnOwnerLeave;
        owner.PreviewMouseDown-=OnMouseDown;owner.PreviewKeyDown-=OnKeyDown;
        owner.IsVisibleChanged-=OnAvailableChanged;owner.IsEnabledChanged-=OnAvailableChanged;
        owner.Unloaded-=OnUnloaded;owner.Loaded-=OnLoaded;owner.SizeChanged-=OnSizeChanged;
        tip.MouseEnter-=OnTipEnter;tip.MouseLeave-=OnTipLeave;tip.PreviewKeyDown-=OnKeyDown;
    }
    bool Available()
    {
        if(!owner.IsEnabled)return false;
        if(window is not null&&!window.IsActive&&PresentationSource.FromVisual(owner) is not null)return false;
        for(DependencyObject? node=owner;node is not null;node=Parent(node))
            if(node is UIElement element&&element.Visibility!=Visibility.Visible)return false;
        return true;
    }
    double LayoutScale()
    {
        double scale=1;
        for(DependencyObject? node=owner;node is not null;node=Parent(node))
            if(node is FrameworkElement element)
            {var matrix=element.LayoutTransform.Value;scale*=Math.Sqrt(matrix.M11*matrix.M11+matrix.M12*matrix.M12);}
        return double.IsFinite(scale)&&scale>0?scale:1;
    }
    static DependencyObject? Parent(DependencyObject node)=>node is Visual?VisualTreeHelper.GetParent(node)??(node as FrameworkElement)?.Parent:null;
    void AttachWindow()
    {
        var next=Window.GetWindow(owner);if(ReferenceEquals(next,window))return;
        DetachWindow();window=next;
        if(window is not null){window.Deactivated+=OnDeactivated;window.PreviewKeyDown+=OnKeyDown;window.PreviewMouseDown+=OnMouseDown;}
    }
    void DetachWindow()
    {if(window is not null){window.Deactivated-=OnDeactivated;window.PreviewKeyDown-=OnKeyDown;window.PreviewMouseDown-=OnMouseDown;window=null;}}
    void OnOpening(object? sender,EventArgs e)=>CompletePending();
    void OnLeaving(object? sender,EventArgs e){leaving.Stop();if(!owner.IsMouseOver&&!tip.IsMouseOver)Close();}
    void OnOwnerEnter(object sender,MouseEventArgs e)=>leaving.Stop();
    void OnOwnerLeave(object sender,MouseEventArgs e){opening.Stop();if(CurrentItem is not null)leaving.Start();}
    void OnTipEnter(object sender,MouseEventArgs e)=>leaving.Stop();
    void OnTipLeave(object sender,MouseEventArgs e){if(CurrentItem is not null&&!owner.IsMouseOver)leaving.Start();}
    void OnMouseDown(object sender,MouseButtonEventArgs e)=>Close();
    void OnKeyDown(object sender,KeyEventArgs e)=>Close();
    void OnAvailableChanged(object sender,DependencyPropertyChangedEventArgs e){if(e.NewValue is false)Close();}
    void OnUnloaded(object sender,RoutedEventArgs e){Close();DetachWindow();}
    void OnLoaded(object sender,RoutedEventArgs e)=>AttachWindow();
    void OnSizeChanged(object sender,SizeChangedEventArgs e)=>Close();
    void OnDeactivated(object? sender,EventArgs e)=>Close();
}
