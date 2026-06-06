using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DocumentationImageHelper.Editor;

/// <summary>Reads images from and writes images to the Windows clipboard.</summary>
public static class ClipboardService
{
    /// <summary>
    /// Returns the clipboard image, or null when the clipboard does not hold an image.
    /// </summary>
    public static BitmapSource? TryGetImage()
    {
        if (!Clipboard.ContainsImage())
            return null;

        try
        {
            return Clipboard.GetImage();
        }
        catch (COMException)
        {
            // The clipboard can briefly be locked by another process.
            return null;
        }
    }

    /// <summary>
    /// Copies a bitmap to the clipboard as both a device bitmap and a PNG, so the
    /// widest range of target applications can paste it. The data is flushed so it
    /// survives after this app closes. Retries a few times because the clipboard
    /// can momentarily be locked by another process.
    /// </summary>
    public static bool SetImage(BitmapSource image)
    {
        var data = new DataObject();
        data.SetImage(image);

        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        encoder.Save(stream);
        data.SetData("PNG", stream);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, true);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(50);
            }
        }

        return false;
    }
}
