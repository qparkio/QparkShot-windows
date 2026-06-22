using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using QPARKShot.Helpers;

namespace QPARKShot.Services;

public static class ClipboardService
{
    public static Task SetBitmapAsync(Bitmap bitmap)
    {
        var src = BitmapHelpers.ToBitmapSource(bitmap);
        Application.Current.Dispatcher.Invoke(() =>
        {
            Clipboard.SetImage(src as BitmapSource);
        });
        return Task.CompletedTask;
    }
}
