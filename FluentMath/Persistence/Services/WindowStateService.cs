using System.Collections.Generic;
using FluentMath.Persistence.Models;

namespace FluentMath.Persistence.Services
{
    // the saved window bounds in memory, keyed by window ("Main"); every change goes to disk debounced
    public class WindowStateService
    {
        // === fields ===

        private readonly Dictionary<string, WindowState> _states = new Dictionary<string, WindowState>();


        // === singleton instance ===

        public static WindowStateService Instance { get; } = new WindowStateService();


        // === constructor ===

        private WindowStateService() { }


        // === public api ===

        // null when this window has never been saved
        public WindowState? GetState(string windowKey)
        {
            return _states.TryGetValue(windowKey, out WindowState? state) ? state : null;
        }

        public void SetState(string windowKey, WindowState state)
        {
            _states[windowKey] = state;
            PersistenceService.Instance.SaveWindowStatesDebounced(_states);
        }

        // persistence
        public void LoadFromDisk(Dictionary<string, WindowState> loaded)
        {
            _states.Clear();
            foreach (KeyValuePair<string, WindowState> pair in loaded)
            {
                _states[pair.Key] = pair.Value;
            }
        }
    }
}
