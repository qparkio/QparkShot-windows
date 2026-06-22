using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using QPARKShot.Helpers;

namespace QPARKShot.Services;

public static class ClipboardService
{
    /// <summary>Place a bitmap on the system clipboard as PNG.</summary>
    public static async Task SetBitmapAsync(Bitmap bitmap)
    {
        // Save to a temp file and reference it as a RandomAccessStream — the most reliable
        // path on unpackaged WinUI 3 apps.
        var tempPath = Path.Combine(Path.GetTempPath(), $"qpark-clip-{Guid.NewGuid():N}.png");
        BitmapHelpers.SavePng(bitmap, tempPath);

        var file = await StorageFile.GetFileFromPathAsync(tempPath);
        var stream = RandomAccessStreamReference.CreateFromFile(file);

        var data = new DataPackage();
        data.SetBitmap(stream);
        Clipboard.SetContent(data);
        Clipboard.Flush();
    }
}
