using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.System;

namespace FluentMath.Distribution
{
    // the distribution channel:
    // installer, portable or store, the one place to ask; installer and portable are the GitHub downloads and
    // differ only in where the state lives
    // a packaged store build never runs its own updater: the store forbids it, the install folder is read only and
    // signed, and the inno installer would leave a second copy
    public static class AppDistribution
    {
        // === win32 api imports ===

        // DllImport, not LibraryImport; the generator cannot marshal the optional null buffer the probe needs
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

        private const int AppmodelErrorNoPackage = 15700; // no package identity


        // === fields ===

        // portable mode: this marker next to the exe moves the state from %LocalAppData% into the app
        // folder; (only in the portable zip)
        public const string PortableMarkerFileName = "portable.txt";

        private static readonly Lazy<bool> _isPackaged = new Lazy<bool>(DetectPackaged);
        private static readonly Lazy<bool> _isPortableBuild = new Lazy<bool>(DetectPortableBuild);


        // === public api ===

        public static bool IsPackaged => _isPackaged.Value;

        public static bool IsPortableBuild => _isPortableBuild.Value;

        // the in-app updater keys off this; (the store build updates through the store, see StoreUpdateSource)
        public static bool SupportsSelfUpdate => !IsPackaged;

        // the product page, where an update can always be installed by hand; found by the package family name,
        // so no store id has to be kept here
        public static Task OpenStorePageAsync()
        {
            if (!IsPackaged) return Task.CompletedTask;

            string familyName;
            try
            {
                familyName = Package.Current.Id.FamilyName;
            }
            catch
            {
                // no identity after all; nothing to open
                return Task.CompletedTask;
            }

            return LaunchStoreAsync(new Uri($"ms-windows-store://pdp/?PFN={familyName}"));
        }


        // === private helpers ===

        // Launcher first, the shell as the fallback; (the answer of Launcher may never come back while the store
        // replaces the package, so nothing waits on it)
        private static async Task LaunchStoreAsync(Uri uri)
        {
            try
            {
                if (await Launcher.LaunchUriAsync(uri)) return;
            }
            catch { /* the shell below gets the next try */ }

            try
            {
                Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
            }
            catch { /* nothing left to try, the click simply does nothing */ }
        }

        // a zero length buffer probes the identity: ERROR_INSUFFICIENT_BUFFER when packaged, APPMODEL_ERROR_NO_PACKAGE
        // otherwise; (Package.Current would throw on every unpackaged start)
        private static bool DetectPackaged()
        {
            try
            {
                int length = 0;
                return GetCurrentPackageFullName(ref length, null) != AppmodelErrorNoPackage;
            }
            catch
            {
                // an api that cannot be reached at all means no package identity either
                return false;
            }
        }

        // a packaged build never counts, its install folder is read only
        private static bool DetectPortableBuild()
        {
            if (IsPackaged) return false;

            try
            {
                string? appFolder = Path.GetDirectoryName(Environment.ProcessPath);
                if (string.IsNullOrEmpty(appFolder)) return false;

                return File.Exists(Path.Combine(appFolder, PortableMarkerFileName));
            }
            catch
            {
                // an unreadable app folder counts as installed (%LocalAppData%)
                return false;
            }
        }
    }
}
