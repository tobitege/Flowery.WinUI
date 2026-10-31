using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Flowery.WinUI.Photos.Services
{
    /// <summary>
    /// Windows shell helpers: recycle bin, explorer, wallpaper, clipboard, launching.
    /// </summary>
    public static partial class FileOperations
    {
        public static bool MoveToRecycleBin(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                return !File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        public static string GetUniquePath(string path)
        {
            if (!File.Exists(path))
                return path;

            var dir = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var ext = System.IO.Path.GetExtension(path);
            for (var i = 1; i < 10000; i++)
            {
                var candidate = System.IO.Path.Combine(dir, $"{name} ({i}){ext}");
                if (!File.Exists(candidate))
                    return candidate;
            }
            return System.IO.Path.Combine(dir, $"{name} ({Guid.NewGuid():N}){ext}");
        }

        public static void ShowInExplorer(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            catch
            {
                // Ignore.
            }
        }

        public static void OpenFolder(string folder)
        {
            try
            {
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch
            {
                // Ignore.
            }
        }

        public static void OpenUri(string uri)
        {
            try
            {
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            }
            catch
            {
                // Ignore.
            }
        }

        public static async Task<bool> OpenWithAsync(string path)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                return await Windows.System.Launcher.LaunchFileAsync(file, new Windows.System.LauncherOptions { DisplayApplicationPicker = true });
            }
            catch
            {
                return false;
            }
        }

        private const int SpiSetDeskWallpaper = 0x0014;
        private const int SpifUpdateIniFile = 0x01;
        private const int SpifSendChange = 0x02;

        [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SystemParametersInfo(int uiAction, int uiParam, string pvParam, int fWinIni);

        public static bool SetAsWallpaper(string path)
        {
            try
            {
                return SystemParametersInfo(SpiSetDeskWallpaper, 0, path, SpifUpdateIniFile | SpifSendChange);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Copies the file (as file drop + bitmap) to the clipboard.
        /// </summary>
        public static async Task<bool> CopyFilesToClipboardAsync(IReadOnlyList<string> paths)
        {
            try
            {
                List<IStorageItem> files = [];
                foreach (var path in paths)
                    files.Add(await StorageFile.GetFileFromPathAsync(path));

                var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                package.SetStorageItems(files);
                if (files.Count == 1 && files[0] is StorageFile single)
                    package.SetBitmap(RandomAccessStreamReference.CreateFromFile(single));
                Clipboard.SetContent(package);
                Clipboard.Flush();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Copies PNG-encoded bitmap bytes to the clipboard.
        /// </summary>
        public static async Task<bool> CopyBitmapToClipboardAsync(byte[] pngBytes)
        {
            if (pngBytes.Length == 0)
                return false;
            try
            {
                var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(pngBytes);
                    await writer.StoreAsync();
                    await writer.FlushAsync();
                    writer.DetachStream();
                }
                stream.Seek(0);
                var package = new DataPackage();
                package.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
                Clipboard.SetContent(package);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string FormatFileSize(long bytes)
        {
            var culture = Localization.PhotosLocalization.CurrentCulture;
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            double size = bytes;
            var unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return unit == 0
                ? string.Format(culture, "{0} {1}", bytes, units[unit])
                : string.Format(culture, "{0:0.#} {1}", size, units[unit]);
        }

        public static IntPtr GetWindowHandle(Window window) => WinRT.Interop.WindowNative.GetWindowHandle(window);
    }
}
