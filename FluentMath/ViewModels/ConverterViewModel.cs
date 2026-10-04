using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FluentMath.Models;
using FluentMath.Models.Converters;
using FluentMath.Persistence.Models;
using FluentMath.Persistence.Services;

namespace FluentMath.ViewModels
{
    // a converter:
    // two pickers over two amount lines, either of which takes the input while the other shows it converted;
    // what is being converted comes from the IUnitSource, so every converter shares this one
    //
    // every key press converts at once, and so does a picker change: the active line keeps its number and
    // the other one is recomputed
    // the lines are kept as plain text with a dot and only dressed with the decimal mark and the grouping
    // on the way out, so a settings change redraws them without touching the input
    public class ConverterViewModel : INotifyPropertyChanged
    {
        // === fields ===

        private readonly IUnitSource _source;
        private readonly CalculatorSettings _numbers; // the decimal mark and the grouping
        private readonly ConverterSettings _precision;

        private string _input = "0"; // the active line as typed; (a dot, never a comma)
        private string _result = "0"; // the other line, rounded; (a dot as well)
        private bool _replaceOnNextKey; // set when a line becomes active; its value goes on the first key


        // === display properties ===

        // replaced only when a refresh brings another set of units
        public IReadOnlyList<UnitInfo> Units { get; private set; } = new List<UnitInfo>();

        // the equality guard keeps a resync of the two-way binding from bouncing a recompute
        // (null is what a picker pushes back when its list is replaced; nobody picks it)
        private UnitInfo? _topUnit;
        public UnitInfo? TopUnit
        {
            get => _topUnit;
            set
            {
                if (value == null || _topUnit == value) return;
                _topUnit = value;
                OnPropertyChanged();
                UnitsChanged();
            }
        }

        private UnitInfo? _bottomUnit;
        public UnitInfo? BottomUnit
        {
            get => _bottomUnit;
            set
            {
                if (value == null || _bottomUnit == value) return;
                _bottomUnit = value;
                OnPropertyChanged();
                UnitsChanged();
            }
        }

        private string _topText = "0";
        public string TopText
        {
            get => _topText;
            private set { _topText = value; OnPropertyChanged(); }
        }

        private string _bottomText = "0";
        public string BottomText
        {
            get => _bottomText;
            private set { _bottomText = value; OnPropertyChanged(); }
        }

        // the line that takes the input; the other one is the result
        private bool _isTopActive = true;
        public bool IsTopActive
        {
            get => _isTopActive;
            private set
            {
                _isTopActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBottomActive));
            }
        }

        public bool IsBottomActive => !_isTopActive;

        // the small line under the converter, e.g. "1 EUR = 1.0842 USD"
        private string _rateText = "";
        public string RateText
        {
            get => _rateText;
            private set { _rateText = value; OnPropertyChanged(); }
        }

        // on the same line, left; e.g. the day the rates are for
        private string _dateText = "";
        public string DateText
        {
            get => _dateText;
            private set { _dateText = value; OnPropertyChanged(); }
        }

        public bool CanRefresh => _source.CanRefresh;

        // the label of the decimal key
        public string DecimalMarkLabel => _numbers.DecimalMarkText;


        // === commands ===

        public ICommand InputCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand BackspaceCommand { get; }
        public ICommand RefreshCommand { get; }


        // === constructor ===

        // the settings are handed in like on CalculatorViewModel, so a test can bring its own
        public ConverterViewModel(IUnitSource source, CalculatorSettings numbers, ConverterSettings precision)
        {
            _source = source;
            _numbers = numbers;
            _precision = precision;

            InputCommand = new RelayCommand<string>(AddInput);
            ClearCommand = new RelayCommand<string>(_ => Clear());
            BackspaceCommand = new RelayCommand<string>(_ => Backspace());
            RefreshCommand = new RelayCommand<string>(_ => Refresh());

            ApplyUnits();

            // the page lives as long as the app, so the subscriptions are never taken back
            _numbers.Changed += SettingsChanged;
            _precision.Changed += SettingsChanged;
        }


        // === units ===

        // the pair is the saved one, or the default when the units lack a saved id
        // (the list is only replaced for another set of ids, which a start without any rates needs)
        private void ApplyUnits()
        {
            DateText = _source.DateText;

            if (!_source.Units.Select(u => u.Id).SequenceEqual(Units.Select(u => u.Id)))
            {
                Units = _source.Units.ToList();
                OnPropertyChanged(nameof(Units));
            }

            UnitPair? saved = PageStateService.Instance.GetConverterPair(_source.Key);
            TopUnit = FindUnit(saved?.From) ?? FindUnit(_source.DefaultFrom);
            BottomUnit = FindUnit(saved?.To) ?? FindUnit(_source.DefaultTo);

            UnitsChanged();
        }

        private UnitInfo? FindUnit(string? id)
        {
            return Units.FirstOrDefault(u => u.Id == id);
        }

        private void Refresh()
        {
            _source.Refresh();
            ApplyUnits();

            // the page lives as long as the app, so the subscriptions are never taken back
            _numbers.Changed += SettingsChanged;
            _precision.Changed += SettingsChanged;
        }

        private void UnitsChanged()
        {
            if (TopUnit != null && BottomUnit != null)
                PageStateService.Instance.SetConverterPair(_source.Key, TopUnit.Id, BottomUnit.Id);

            UpdateRateText();
            Convert();
        }


        // === input handling ===

        // the first digit replaces the placeholder zero, or the value a line brought along when it became
        // active; a decimal point keeps the zero
        private void AddInput(string sign)
        {
            if (_replaceOnNextKey)
            {
                _input = "0";
                _replaceOnNextKey = false;
            }

            if (sign == "." && _input.Contains('.')) return;

            if (_input == "0" && sign != ".") _input = "";
            _input += sign;
            Convert();
        }

        private void Clear()
        {
            _input = "0";
            _replaceOnNextKey = false;
            Convert();
        }

        private void Backspace()
        {
            _replaceOnNextKey = false;

            if (_input.Length <= 1) _input = "0";
            else _input = _input.Remove(_input.Length - 1);
            Convert();
        }

        // the line keeps the value it shows, and the next key replaces it
        public void ActivateLine(bool top)
        {
            if (top == IsTopActive) return;

            _input = _result;
            _replaceOnNextKey = true;
            IsTopActive = top;
            Convert();
        }


        // === conversion ===

        // parsed invariant; the keypad always types a dot
        private void Convert()
        {
            UnitInfo? from = IsTopActive ? TopUnit : BottomUnit;
            UnitInfo? to = IsTopActive ? BottomUnit : TopUnit;

            _result = "0";
            if (from != null && to != null
                && double.TryParse(_input, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount)
                && _source.Convert(from.Id, to.Id, amount) is double converted)
            {
                _result = UnitFormat.Amount(converted, _source.RoundsToDecimals, _precision);
            }

            string input = UnitFormat.Display(_input, _numbers);
            string result = UnitFormat.Display(_result, _numbers);
            TopText = IsTopActive ? input : result;
            BottomText = IsTopActive ? result : input;
        }

        private void UpdateRateText()
        {
            if (TopUnit == null || BottomUnit == null)
            {
                RateText = _source.EmptyText;
                return;
            }

            double? rate = _source.Convert(TopUnit.Id, BottomUnit.Id, 1);
            RateText = rate is double r
                ? $"1 {TopUnit.Symbol} = {UnitFormat.Display(UnitFormat.Rate(r, _source.RoundsToDecimals, _precision), _numbers)} {BottomUnit.Symbol}"
                : "";
        }

        private void SettingsChanged()
        {
            OnPropertyChanged(nameof(DecimalMarkLabel));
            UpdateRateText();
            Convert();
        }


        // === property changed ===

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
