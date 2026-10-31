using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Akara.Desktop.ViewModels;

namespace Akara.Desktop.Controls;

public sealed class SectionVisibilityConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>string.Equals(value?.ToString(),parameter?.ToString(),StringComparison.Ordinal)?Visibility.Visible:Visibility.Collapsed;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
public sealed class InverseVisibilityConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is true?Visibility.Collapsed:Visibility.Visible;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
public sealed class ItemImageConverter : IValueConverter
{
    public object? Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is ItemViewModel item?ItemArt.Get(item):null;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
public sealed class BrushConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(value?.ToString()??"#B99D6C"));
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
public sealed class ClassSigilConverter : IValueConverter
{
    public object? Convert(object value,Type targetType,object parameter,CultureInfo culture)=>Application.Current.TryFindResource("Class."+(value?.ToString()??"Sorceress"));
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
public sealed class SelectionMatchConverter : IMultiValueConverter
{
    public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture)=>values.Length==2&&string.Equals(values[0]?.ToString(),values[1]?.ToString(),StringComparison.Ordinal);
    public object[] ConvertBack(object value,Type[] targetTypes,object parameter,CultureInfo culture)=>[Binding.DoNothing,Binding.DoNothing];
}
public static class BrandArt
{
    public static ImageSource? Logo
    {
        get
        {
            try
            {
                var bitmap=new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Akara;component/Assets/Art/akara-mark.png"));
                var cropped=new System.Windows.Media.Imaging.CroppedBitmap(bitmap,new Int32Rect(265,72,780,1007));cropped.Freeze();return cropped;
            }
            catch(Exception exception)when(exception is System.IO.IOException or ArgumentException){return null;}
        }
    }
}
