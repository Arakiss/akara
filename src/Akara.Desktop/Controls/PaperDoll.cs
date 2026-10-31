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
        var ambient=new RadialGradientBrush(Color.FromRgb(35,29,22),Color.FromRgb(11,12,14)){Center=new Point(.5,.22),GradientOrigin=new Point(.5,.22),RadiusX=.75,RadiusY=.85};
        d.DrawRectangle(ambient,null,new Rect(0,0,440,500));
        d.DrawRectangle(MaterialBrushes.Grain,null,new Rect(0,0,440,500));
        var charcoal=new LinearGradientBrush(Color.FromRgb(23,22,20),Color.FromRgb(9,10,12),90);
        var corner=new Pen(new SolidColorBrush(Color.FromRgb(90,71,48)),.9);
        foreach(var origin in new[]{new Point(6,6),new Point(434,6),new Point(6,494),new Point(434,494)})
        {double sx=origin.X<220?1:-1,sy=origin.Y<250?1:-1;d.DrawLine(corner,origin,new Point(origin.X+18*sx,origin.Y));d.DrawLine(corner,origin,new Point(origin.X,origin.Y+18*sy));}

        foreach(var pair in Layout)
        {
            var slot=ItemsSource?.FirstOrDefault(s=>s.Slot==pair.Key);var item=slot?.Item;var r=pair.Value;bool selected=item is not null&&SelectedItem?.Id==item.Id;

            var tone=item is null?Color.FromRgb(91,76,55):(Color)ColorConverter.ConvertFromString(item.Accent);
            var stroke=new Pen(new SolidColorBrush(selected?tone:Color.FromRgb(70,59,45)),selected?1.7:.8);
            d.DrawRoundedRectangle(charcoal,stroke,r,2,2);
            d.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(45,182,151,108)),.65),new Point(r.X+3,r.Y+2),new Point(r.Right-3,r.Y+2));

            if(item is not null)
            {
                if(selected)d.DrawRectangle(new SolidColorBrush(Color.FromArgb(24,tone.R,tone.G,tone.B)),null,new Rect(r.X+1,r.Y+1,r.Width-2,r.Height-2));
                var image=ItemArt.GetPresentation(item);var area=new Rect(r.X+8,r.Y+7,r.Width-16,r.Height-14);DrawContained(d,image,area);
            }
            else
            {
                string glyph=pair.Key switch{"weapon"=>"╱","shield"=>"◇","helm"=>"⌒","ring1" or "ring2"=>"○",_=>"·"};
                var ghost=new FormattedText(glyph,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),22,new SolidColorBrush(Color.FromRgb(68,61,51)),1);
                d.DrawText(ghost,new(r.X+(r.Width-ghost.Width)/2,r.Y+(r.Height-ghost.Height)/2));
            }
            string labelText=pair.Key switch{"ring1" or "ring2"=>"Anillo","shield"=>"Secundaria",_=>slot?.Label??Names.Slot(pair.Key)};
            var label=new FormattedText(labelText,CultureInfo.GetCultureInfo("es-ES"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),12/scale,new SolidColorBrush(Color.FromRgb(168,175,178)),1){MaxTextWidth=r.Width-6,Trimming=TextTrimming.CharacterEllipsis,TextAlignment=TextAlignment.Center};
            if(item is null)d.DrawText(label,new(r.X+3,r.Bottom-18/scale));
        }
        var twoHand=ItemsSource?.FirstOrDefault(s=>s.Slot=="weapon")?.Item;
        if(twoHand?.TwoHanded==true&&ItemsSource?.FirstOrDefault(s=>s.Slot=="shield")?.Item is null)
        {
            var r=Layout["shield"];d.PushOpacity(.2);DrawContained(d,ItemArt.GetPresentation(twoHand),new Rect(r.X+11,r.Y+8,r.Width-22,r.Height-23));d.Pop();
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
