using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using QPARKShot.Helpers;
using QPARKShot.Localization;
namespace QPARKShot.Services;

public static class ShotActions
{
    private static DataTransferManager? _manager;
    private static StorageFile? _shareFile;
    [ComImport, Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow(IntPtr window, ref Guid iid);
        void ShowShareUIForWindow(IntPtr window);
    }
    public static async Task ShareAsync(string path)
    {
        var hwnd = App.MainWindowInstance!.Hwnd;
        _shareFile = await StorageFile.GetFileFromPathAsync(path);
        var interop = DataTransferManager.As<IDataTransferManagerInterop>();
        if (_manager == null)
        {
            var iid = new Guid("A5CAEE9B-8708-49D1-8D36-67D25A8DA00C");
            var pointer = interop.GetForWindow(hwnd, ref iid);
            try { _manager = WinRT.MarshalInterface<DataTransferManager>.FromAbi(pointer); }
            finally { Marshal.Release(pointer); }
            _manager.DataRequested += (_, args) =>
            {
                args.Request.Data.Properties.Title = "QPARK Shot";
                args.Request.Data.RequestedOperation = DataPackageOperation.Copy;
                if (_shareFile != null) args.Request.Data.SetStorageItems(new[] { _shareFile });
            };
        }
        interop.ShowShareUIForWindow(hwnd);
    }
    public static void Pin(System.Drawing.Bitmap bitmap)
    {
        var image = BitmapHelpers.ToBitmapSource(bitmap);
        var panel = new DockPanel();
        var copy = new Button { Content = L.T("common.copy"), Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Left };
        copy.Click += (_, _) => Clipboard.SetImage(image);
        DockPanel.SetDock(copy, Dock.Top); panel.Children.Add(copy);
        panel.Children.Add(new Image { Source = image, Stretch = System.Windows.Media.Stretch.Uniform, Margin = new Thickness(8) });
        var window = new Window { Title = L.T("review.pinned"), Topmost = true, Width = 520, Height = 420, MinWidth = 220, MinHeight = 180, Content = panel };
        window.SetResourceReference(Window.BackgroundProperty, "WindowBackground");
        window.SetResourceReference(Window.ForegroundProperty, "WindowForeground");
        window.Show();
    }
}
