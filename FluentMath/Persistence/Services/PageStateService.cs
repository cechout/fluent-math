using FluentMath.Persistence.Models;

namespace FluentMath.Persistence.Services
{
    // the page state in memory: compact sizes, the scientific splitter and the converter pairs
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

        // null when this converter has never been picked on
        public UnitPair? GetConverterPair(string converter)
        {
            return _data.ConverterPairs.TryGetValue(converter, out UnitPair? pair) ? pair : null;
        }

        public void SetConverterPair(string converter, string from, string to)
        {
            UnitPair? saved = GetConverterPair(converter);
            if (saved != null && saved.From == from && saved.To == to) return;

            _data.ConverterPairs[converter] = new UnitPair { From = from, To = to };
            Save();
        }

        // persistence
        // a null in a file edited by hand falls back to the default
        public void LoadFromDisk(PageStateData loaded)
        {
            loaded.CompactSizes ??= new PageStateData().CompactSizes;
            loaded.ConverterPairs ??= new PageStateData().ConverterPairs;

            _data = loaded;
        }

        private void Save()
        {
            PersistenceService.Instance.SavePageStateDebounced(_data);
        }
    }
}
