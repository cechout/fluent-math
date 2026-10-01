using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using FluentMath.Persistence.Models;

namespace FluentMath.Persistence.Services
{
    // the disk layer under every saved state:
    // one json file per area, each written through its own debounce; callers hand in plain data and get plain
    // data back, and the folder is handed in, so nothing here depends on the app
    public class PersistenceService
    {
        // === fields ===

        // --- files ---
        public const string SettingsFileName = "settings.json";
        public const string WindowStateFileName = "window-state.json";
        public const string PageStateFileName = "page-state.json";

        // --- quarantine ---
        // a file that does not parse is moved here rather than read again, and kept for a while in case a
        // problem gets reported
        private const string QuarantineFolderName = "quarantine";
        private const string CorruptSuffix = ".corrupt-";
        private static readonly TimeSpan QuarantineRetention = TimeSpan.FromDays(30);

        // rapid changes, a window drag above all, are written once they settle
        private const int DebounceMs = 1000;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _rootFolder;
        private readonly PendingFile _settings;
        private readonly PendingFile _windowStates;
        private readonly PendingFile _pageState;

        // the timers fire on the thread pool; a write and the hand over of its json happen under this lock
        private readonly object _gate = new object();


        // === singleton instance ===

        // set once by App with the folder of this build; the tests make their own
        public static PersistenceService Instance { get; private set; } = null!;

        public static void Initialize(string rootFolder)
        {
            Instance = new PersistenceService(rootFolder);
        }


        // === constructor ===

        public PersistenceService(string rootFolder)
        {
            _rootFolder = rootFolder;
            _settings = new PendingFile(Path.Combine(rootFolder, SettingsFileName));
            _windowStates = new PendingFile(Path.Combine(rootFolder, WindowStateFileName));
            _pageState = new PendingFile(Path.Combine(rootFolder, PageStateFileName));

            TidyQuarantine();
        }


        // === public api ===

        public string RootFolder => _rootFolder;

        // load; a missing or broken file gives the defaults
        public AppSettingsData LoadSettings() => LoadFile<AppSettingsData>(_settings.Path) ?? new AppSettingsData();
        public Dictionary<string, WindowState> LoadWindowStates() => LoadFile<Dictionary<string, WindowState>>(_windowStates.Path) ?? new Dictionary<string, WindowState>();
        public PageStateData LoadPageState() => LoadFile<PageStateData>(_pageState.Path) ?? new PageStateData();

        // debounced save; the data is serialized on the spot, so a later change to it cannot reach this write
        public void SaveSettingsDebounced(AppSettingsData data) => QueueSave(_settings, data);
        public void SaveWindowStatesDebounced(Dictionary<string, WindowState> data) => QueueSave(_windowStates, data);
        public void SavePageStateDebounced(PageStateData data) => QueueSave(_pageState, data);

        // writes whatever is still pending right away; on exit, where a timer would never get to fire
        public void FlushAll()
        {
            Flush(_settings);
            Flush(_windowStates);
            Flush(_pageState);
        }


        // === private helpers ===

        // one file with its debounce timer and the json still waiting to be written
        private sealed class PendingFile
        {
            public PendingFile(string path)
            {
                Path = path;
            }

            public string Path { get; }
            public Timer? Timer { get; set; }
            public string? Json { get; set; }
        }

        private void QueueSave<T>(PendingFile file, T data)
        {
            string json = JsonSerializer.Serialize(data, JsonOptions);

            lock (_gate)
            {
                file.Json = json;
                file.Timer?.Dispose();
                file.Timer = new Timer(_ => Flush(file), null, DebounceMs, Timeout.Infinite);
            }
        }

        // the write itself stays inside the lock, so an older json can never land after a newer one
        private void Flush(PendingFile file)
        {
            lock (_gate)
            {
                file.Timer?.Dispose();
                file.Timer = null;

                if (file.Json == null) return;

                WriteFile(file.Path, file.Json);
                file.Json = null;
            }
        }

        // through a temp file, so a crash mid write leaves the old file rather than half a new one
        private void WriteFile(string path, string json)
        {
            try
            {
                Directory.CreateDirectory(_rootFolder);

                string temp = path + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, path, overwrite: true);
            }
            catch
            {
                // best effort; a failed save never takes the app down
            }
        }

        private T? LoadFile<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;

                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
            }
            catch (JsonException)
            {
                Quarantine(path);
                return null;
            }
            catch
            {
                // unreadable right now, a lock for instance; the file is left where it is
                return null;
            }
        }

        private string QuarantineFolder => Path.Combine(_rootFolder, QuarantineFolderName);

        private void Quarantine(string path)
        {
            try
            {
                Directory.CreateDirectory(QuarantineFolder);

                string name = $"{Path.GetFileName(path)}{CorruptSuffix}{DateTime.Now:yyyyMMdd-HHmmss}";
                string target = Path.Combine(QuarantineFolder, name);
                File.Move(path, target, overwrite: true);

                // a move keeps the old write time, and the retention counts from the move
                File.SetLastWriteTime(target, DateTime.Now);
            }
            catch { /* if even the move fails, the next start tries again */ }
        }

        // removes what has been in the quarantine past its retention, once per start
        private void TidyQuarantine()
        {
            try
            {
                if (!Directory.Exists(QuarantineFolder)) return;

                foreach (string kept in Directory.GetFiles(QuarantineFolder))
                {
                    if (DateTime.Now - File.GetLastWriteTime(kept) > QuarantineRetention) File.Delete(kept);
                }
            }
            catch { /* housekeeping, never worth failing a start over */ }
        }
    }
}
