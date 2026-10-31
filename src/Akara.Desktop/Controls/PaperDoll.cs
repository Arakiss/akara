using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop.Controls;

public sealed class PaperDoll : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty=DependencyProperty.Register(nameof(ItemsSource),typeof(IEnumerable<EquipmentSlotViewModel>),typeof(PaperDoll),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender,Changed));
    public static readonly DependencyProperty SelectedItemProperty=DependencyProperty.Register(nameof(SelectedItem),typeof(ItemViewModel),typeof(PaperDoll),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault|FrameworkPropertyMetadataOptions.AffectsRender));
    public IEnumerable<EquipmentSlotViewModel>? ItemsSource{get=>(IEnumerable<EquipmentSlotViewModel>?)GetValue(ItemsSourceProperty);set=>SetValue(ItemsSourceProperty,value);}
    public ItemViewModel? SelectedItem{get=>(ItemViewModel?)GetValue(SelectedItemProperty);set=>SetValue(SelectedItemProperty,value);}
    internal void SelectItem(ItemViewModel? item)=>SetCurrentValue(SelectedItemProperty,item);
    public event EventHandler? ItemInvoked;
    static readonly Dictionary<string,Rect> Layout=new(){["helm"]=new(185,10,70,85),["amulet"]=new(262,66,62,56),["weapon"]=new(60,90,92,200),["shield"]=new(290,140,92,180),["armor"]=new(170,120,100,180),["gloves"]=new(64,335,80,90),["belt"]=new(172,325,96,60),["boots"]=new(295,335,80,90),["ring1"]=new(161,436,48,56),["ring2"]=new(231,436,48,56)};
    public PaperDoll(){Focusable=true;Cursor=Cursors.Hand;UseLayoutRounding=true;SnapsToDevicePixels=true;ToolTip="Selecciona una pieza. Flechas para recorrer el equipo; Intro para editar.";AutomationProperties.SetName(this,"Equipo. Usa las flechas para seleccionar una pieza y pulsa Intro para editarla.");}
    protected override AutomationPeer OnCreateAutomationPeer()=>new ItemBoardAutomationPeer(this,"Equipo");
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e){base.OnPropertyChanged(e);if(e.Property==SelectedItemProperty)AutomationProperties.SetName(this,SelectedItem is { } item?"Equipo: "+item.Name:"Equipo. Sin pieza seleccionada.");}
    static void Changed(DependencyObject target,DependencyPropertyChangedEventArgs e){var doll=(PaperDoll)target;if(e.OldValue is INotifyCollectionChanged old)old.CollectionChanged-=doll.CollectionChanged;if(e.NewValue is INotifyCollectionChanged current)current.CollectionChanged+=doll.CollectionChanged;doll.InvalidateVisual();}
    void CollectionChanged(object? sender,NotifyCollectionChangedEventArgs e)=>InvalidateVisual();
    protected override Size MeasureOverride(Size availableSize)=>new(Math.Min(440,availableSize.Width),Math.Min(500,availableSize.Height));
    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);double scale=Math.Min(ActualWidth/440,ActualHeight/500);if(scale<=0)return;
        d.PushTransform(new TranslateTransform((ActualWidth-440*scale)/2,0));d.PushTransform(new ScaleTransform(scale,scale));
        d.DrawRectangle(new SolidColorBrush(Color.FromRgb(25,29,32)),null,new Rect(0,0,440,500));
        var charcoal=new SolidColorBrush(Color.FromRgb(20,24,27));

        foreach(var pair in Layout)
        {
            var slot=ItemsSource?.FirstOrDefault(s=>s.Slot==pair.Key);var item=slot?.Item;var r=pair.Value;bool selected=item is not null&&SelectedItem?.Id==item.Id;

            var stroke=new Pen(new SolidColorBrush(selected?Color.FromRgb(195,166,109):Color.FromRgb(68,78,85)),selected?1.7:.8);
            d.DrawRectangle(charcoal,stroke,r);

            if(item is not null)
            {
                if(selected)d.DrawRectangle(new SolidColorBrush(Color.FromArgb(26,195,166,109)),null,new Rect(r.X+1,r.Y+1,r.Width-2,r.Height-2));
                var image=ItemArt.Get(item);var area=new Rect(r.X+7,r.Y+5,r.Width-14,r.Height-24/scale);DrawContained(d,image,area);
            }
            else
            {
                string glyph=pair.Key switch{"weapon"=>"╱","shield"=>"◇","armor"=>"♧","helm"=>"⌒","ring1" or "ring2"=>"○",_=>"·"};
                var ghost=new FormattedText(glyph,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),22,new SolidColorBrush(Color.FromRgb(75,85,92)),1);
                d.DrawText(ghost,new(r.X+(r.Width-ghost.Width)/2,r.Y+(r.Height-ghost.Height)/2));
            }
            string labelText=pair.Key switch{"ring1" or "ring2"=>"Anillo","shield"=>"Secundaria",_=>slot?.Label??Names.Slot(pair.Key)};
            var label=new FormattedText(labelText,CultureInfo.GetCultureInfo("es-ES"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),12/scale,new SolidColorBrush(Color.FromRgb(168,175,178)),1){MaxTextWidth=r.Width-6,Trimming=TextTrimming.CharacterEllipsis,TextAlignment=TextAlignment.Center};
            if(item is null)d.DrawText(label,new(r.X+3,r.Bottom-18/scale));
        }
        var twoHand=ItemsSource?.FirstOrDefault(s=>s.Slot=="weapon")?.Item;
        if(twoHand?.TwoHanded==true&&ItemsSource?.FirstOrDefault(s=>s.Slot=="shield")?.Item is null)
        {
            var r=Layout["shield"];d.PushOpacity(.2);DrawContained(d,ItemArt.Get(twoHand),new Rect(r.X+11,r.Y+8,r.Width-22,r.Height-23));d.Pop();
        }
        d.Pop();d.Pop();
    }
    static void DrawContained(DrawingContext d,ImageSource image,Rect area){double ratio=image.Width/image.Height;double width=area.Width,height=area.Height;if(width/height>ratio)width=height*ratio;else height=width/ratio;d.DrawImage(image,new Rect(area.X+(area.Width-width)/2,area.Y+(area.Height-height)/2,width,height));}
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);double scale=Math.Min(ActualWidth/440,ActualHeight/500);if(scale<=0)return;
        var point=e.GetPosition(this);point=new((point.X-(ActualWidth-440*scale)/2)/scale,point.Y/scale);
        var hit=Layout.FirstOrDefault(p=>p.Value.Contains(point));
        var slot=ItemsSource?.FirstOrDefault(s=>s.Slot==hit.Key);
        ToolTip=slot is null?"Selecciona una pieza. Flechas para recorrer el equipo; Intro para editar.":slot.Item is { } item?item.Name+Environment.NewLine+slot.Label:slot.Label+" · vacío";
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){base.OnMouseLeftButtonDown(e);Focus();double scale=Math.Min(ActualWidth/440,ActualHeight/500);var point=e.GetPosition(this);point=new((point.X-(ActualWidth-440*scale)/2)/scale,point.Y/scale);var hit=Layout.FirstOrDefault(p=>p.Value.Contains(point));SelectItem(ItemsSource?.FirstOrDefault(s=>s.Slot==hit.Key)?.Item);if(e.ClickCount==2&&SelectedItem is not null)ItemInvoked?.Invoke(this,EventArgs.Empty);e.Handled=true;}
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)
        {
            var items=Layout.Keys.Select(key=>ItemsSource?.FirstOrDefault(s=>s.Slot==key)?.Item).OfType<ItemViewModel>().ToList();if(items.Count==0)return;
            int index=items.FindIndex(i=>i.Id==SelectedItem?.Id);index=e.Key switch{Key.Home=>0,Key.End=>items.Count-1,Key.Left or Key.Up=>Math.Max(0,index-1),_=>Math.Min(items.Count-1,index+1)};SelectItem(items[index]);e.Handled=true;
        }
        else if(e.Key is Key.Enter or Key.Space&&SelectedItem is not null){ItemInvoked?.Invoke(this,EventArgs.Empty);e.Handled=true;}
    }
}
