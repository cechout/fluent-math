using FluentMath.Persistence.Models;

namespace FluentMath.Persistence.Services
{
    // the page state in memory: compact sizes, the scientific splitter and the currency pair
    // every change goes to disk debounced
    public class PageStateService
    {
        // === fields ===

        private PageStateData _data = new PageStateData();


        // === singleton instance ===

        public static PageStateService Instance { get; } = new PageStateService();


        // === constructor ===

        private PageStateService() { }


        // === public api ===

        // null when this page has never been compact
        public CompactSize? GetCompactSize(string pageName)
        {
            return _data.CompactSizes.TryGetValue(pageName, out CompactSize? size) ? size : null;
        }

        public void SetCompactSize(string pageName, double width, double height)
        {
            _data.CompactSizes[pageName] = new CompactSize { Width = width, Height = height };
            Save();
        }

        public double? ScientificDisplayHeight
        {
            get => _data.ScientificDisplayHeight;
            set
            {
                if (_data.ScientificDisplayHeight == value) return;

                _data.ScientificDisplayHeight = value;
                Save();
            }
        }

        public string CurrencyFrom
        {
            get => _data.CurrencyFrom;
            set
            {
                if (_data.CurrencyFrom == value) return;

                _data.CurrencyFrom = value;
                Save();
            }
        }

        public string CurrencyTo
        {
            get => _data.CurrencyTo;
            set
            {
                if (_data.CurrencyTo == value) return;

                _data.CurrencyTo = value;
                Save();
            }
        }

        // persistence
        // a null in a file edited by hand falls back to the default
        public void LoadFromDisk(PageStateData loaded)
        {
            loaded.CompactSizes ??= new PageStateData().CompactSizes;
            loaded.CurrencyFrom ??= PageStateData.DefaultCurrencyFrom;
            loaded.CurrencyTo ??= PageStateData.DefaultCurrencyTo;

            _data = loaded;
        }

        private void Save()
        {
            PersistenceService.Instance.SavePageStateDebounced(_data);
        }
    }
}
