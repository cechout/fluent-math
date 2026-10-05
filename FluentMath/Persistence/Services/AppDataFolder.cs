using System;
using System.IO;
using FluentMath.Distribution;
using Windows.Storage;

namespace FluentMath.Persistence.Services
{
    // where the json files live:
    // %LocalAppData%\FluentMath for the installer build, the package LocalState for the store build, and a
    // Persistence folder next to the exe for the portable build
    //
    // the store build asks the app model rather than building the path: msix redirects a write to
    // %LocalAppData% into the package, so the literal path works for the app but sends "Open" to an empty
    // folder in an Explorer outside it; LocalState is the one both agree on, and goes with an uninstall
    public static class AppDataFolder
    {
        // === fields ===

        private const string LocalFolderName = "FluentMath";

        // next to the exe, so the whole state travels with the folder; (portable build only)
        private const string PortableFolderName = "Persistence";


        // === public api ===

        public static string Resolve()
        {
            if (AppDistribution.IsPackaged)
            {
                try
                {
                    return ApplicationData.Current.LocalFolder.Path;
                }
                catch { /* no app data for this identity; the per user folder below still keeps the state */ }
            }

            string localAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LocalFolderName);

            if (!AppDistribution.IsPortableBuild) return localAppData;

            try
            {
                string? appFolder = Path.GetDirectoryName(Environment.ProcessPath);
                if (string.IsNullOrEmpty(appFolder)) return localAppData;

                // created on the spot as the write check; a zip unpacked somewhere read only would drop every save
                string portableFolder = Path.Combine(appFolder, PortableFolderName);
                Directory.CreateDirectory(portableFolder);
                return portableFolder;
            }
            catch
            {
                // an unusable app folder; the per user folder then
                return localAppData;
            }
        }
    }
}
