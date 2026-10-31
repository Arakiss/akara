using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop.Controls;

public sealed class InventoryBoard : FrameworkElement
{
    internal ItemHover Hover {get;}
    protected override AutomationPeer OnCreateAutomationPeer()=>new ItemBoardAutomationPeer(this,"Inventario");
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e){base.OnPropertyChanged(e);if(e.Property==SelectedItemProperty)AutomationProperties.SetName(this,SelectedItem is { } item?"Inventario: "+item.Name:"Inventario. Usa las flechas para seleccionar una pieza y pulsa Intro para editarla.");}
    public static readonly DependencyProperty ItemsSourceProperty=DependencyProperty.Register(nameof(ItemsSource),typeof(IEnumerable<ItemViewModel>),typeof(InventoryBoard),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender,ItemsChanged));
    public static readonly DependencyProperty ColumnsProperty=DependencyProperty.Register(nameof(Columns),typeof(int),typeof(InventoryBoard),new FrameworkPropertyMetadata(10,FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty RowsProperty=DependencyProperty.Register(nameof(Rows),typeof(int),typeof(InventoryBoard),new FrameworkPropertyMetadata(8,FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CellSizeProperty=DependencyProperty.Register(nameof(CellSize),typeof(double),typeof(InventoryBoard),new FrameworkPropertyMetadata(34d,FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SelectedItemProperty=DependencyProperty.Register(nameof(SelectedItem),typeof(ItemViewModel),typeof(InventoryBoard),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault|FrameworkPropertyMetadataOptions.AffectsRender));
    public IEnumerable<ItemViewModel>? ItemsSource {get=>(IEnumerable<ItemViewModel>?)GetValue(ItemsSourceProperty);set=>SetValue(ItemsSourceProperty,value);}
    public int Columns {get=>(int)GetValue(ColumnsProperty);set=>SetValue(ColumnsProperty,value);}
    public int Rows {get=>(int)GetValue(RowsProperty);set=>SetValue(RowsProperty,value);}
    public double CellSize {get=>(double)GetValue(CellSizeProperty);set=>SetValue(CellSizeProperty,value);}
    public ItemViewModel? SelectedItem {get=>(ItemViewModel?)GetValue(SelectedItemProperty);set=>SetValue(SelectedItemProperty,value);}
    internal void SelectItem(ItemViewModel? item)=>SetCurrentValue(SelectedItemProperty,item);
    public event EventHandler? ItemInvoked;
    public InventoryBoard(){Focusable=true;SnapsToDevicePixels=true;UseLayoutRounding=true;Cursor=Cursors.Hand;Hover=new ItemHover(this);}
    static void ItemsChanged(DependencyObject sender,DependencyPropertyChangedEventArgs e){var board=(InventoryBoard)sender;if(e.OldValue is INotifyCollectionChanged old)old.CollectionChanged-=board.CollectionChanged;if(e.NewValue is INotifyCollectionChanged current)current.CollectionChanged+=board.CollectionChanged;board.Hover?.Invalidate();board.InvalidateVisual();}
    void CollectionChanged(object? sender,NotifyCollectionChangedEventArgs e){Hover.Invalidate();InvalidateVisual();}
    protected override Size MeasureOverride(Size availableSize)=>new(Math.Max(1,Columns)*CellSize,Math.Max(1,Rows)*CellSize);
    Rect Bounds(ItemViewModel item)=>new(item.GridX*CellSize+2,item.GridY*CellSize+2,item.Width*CellSize-4,item.Height*CellSize-4);
    internal ItemViewModel? HitItem(Point point)
    {
        if(CellSize<=0||point.X<0||point.Y<0||point.X>=Columns*CellSize||point.Y>=Rows*CellSize)return null;
        int x=Math.Min(Math.Max(0,Columns-1),(int)Math.Floor(point.X/CellSize));
        int y=Math.Min(Math.Max(0,Rows-1),(int)Math.Floor(point.Y/CellSize));
        return (ItemsSource??[]).LastOrDefault(item=>x>=item.GridX&&x<item.GridX+item.Width&&y>=item.GridY&&y<item.GridY+item.Height);
    }
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);Hover.Track(HitItem(e.GetPosition(this)));}
    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);int columns=Math.Max(1,Columns),rows=Math.Max(1,Rows);double width=columns*CellSize,height=rows*CellSize;
        d.DrawRectangle(new LinearGradientBrush(Color.FromRgb(18,17,16),Color.FromRgb(8,9,11),90),null,new Rect(0,0,width,height));
        d.DrawRectangle(MaterialBrushes.Grain,null,new Rect(0,0,width,height));
        var gridPen=new Pen(new SolidColorBrush(Color.FromRgb(53,46,36)),.65);
        for(int x=0;x<=columns;x++)d.DrawLine(gridPen,new(x*CellSize,0),new(x*CellSize,height));
        for(int y=0;y<=rows;y++)d.DrawLine(gridPen,new(0,y*CellSize),new(width,y*CellSize));
        foreach(var item in ItemsSource??[])
        {
            var rect=Bounds(item);bool selected=SelectedItem?.Id==item.Id;
            var tone=(Color)ColorConverter.ConvertFromString(item.Accent);
            d.DrawRectangle(new SolidColorBrush(Color.FromArgb(selected?(byte)30:(byte)7,tone.R,tone.G,tone.B)),new Pen(new SolidColorBrush(selected?tone:Color.FromArgb(68,tone.R,tone.G,tone.B)),selected?1.5:.65),rect);
            var image=ItemArt.Get(item);double ratio=image.Width/image.Height;double drawWidth=rect.Width-7,drawHeight=rect.Height-7;if(drawWidth/drawHeight>ratio)drawWidth=drawHeight*ratio;else drawHeight=drawWidth/ratio;
            d.DrawImage(image,new Rect(rect.X+(rect.Width-drawWidth)/2,rect.Y+(rect.Height-drawHeight)/2,drawWidth,drawHeight));
        }
        if(IsKeyboardFocusWithin)d.DrawRectangle(null,new Pen(new SolidColorBrush(Color.FromRgb(195,166,109)),1),new Rect(.5,.5,width-1,height-1));
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){base.OnMouseLeftButtonDown(e);Hover.Close();Focus();SelectItem(HitItem(e.GetPosition(this)));if(e.ClickCount==2&&SelectedItem is not null)ItemInvoked?.Invoke(this,EventArgs.Empty);e.Handled=true;}
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.Key is Key.Left or Key.Right or Key.Up or Key.Down){var items=(ItemsSource??[]).OrderBy(i=>i.Y).ThenBy(i=>i.X).ToList();if(items.Count==0)return;int index=items.FindIndex(i=>i.Id==SelectedItem?.Id);index=e.Key is Key.Left or Key.Up?Math.Max(0,index-1):Math.Min(items.Count-1,index+1);SelectItem(items[index]);e.Handled=true;}else if(e.Key==Key.Enter&&SelectedItem is not null){ItemInvoked?.Invoke(this,EventArgs.Empty);e.Handled=true;}}
}
internal sealed class ItemBoardAutomationPeer(FrameworkElement owner,string className):FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore()=>className;
    protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.List;
}
