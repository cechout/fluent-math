using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FluentMath.Models;
using FluentMath.Persistence.Models;
using FluentMath.Persistence.Services;

namespace FluentMath.ViewModels
{
    // the currency converter:
    // the two pickers, the amount typed in and the converted result;
    // the input is a plain string, since the converter only ever takes one number
    public class CurrencyViewModel : INotifyPropertyChanged
    {
        // === fields ===

        private ConvertCurrency? _converter; // null while there are no rates at all


        // === display properties ===

        // replaced only when a refresh brings another set of codes
        public List<CurrencyInfo> AvailableCurrencies { get; private set; } = new List<CurrencyInfo>();

        // the equality guard keeps a resync of the two-way binding from bouncing UpdateRateText
        // (null is what a picker pushes back when its list is replaced; nobody picks it)
        private CurrencyInfo _selectedCurrency1;
        public CurrencyInfo SelectedCurrency1
        {
            get => _selectedCurrency1;
            set
            {
                if (value == null || _selectedCurrency1 == value) return;
                _selectedCurrency1 = value;
                OnPropertyChanged();
                PageStateService.Instance.CurrencyFrom = value.Code;
                UpdateRateText();
            }
        }

        private CurrencyInfo _selectedCurrency2;
        public CurrencyInfo SelectedCurrency2
        {
            get => _selectedCurrency2;
            set
            {
                if (value == null || _selectedCurrency2 == value) return;
                _selectedCurrency2 = value;
                OnPropertyChanged();
                PageStateService.Instance.CurrencyTo = value.Code;
                UpdateRateText();
            }
        }

        private string _inputText = "0";
        public string InputText
        {
            get => _inputText;
            set { _inputText = value; OnPropertyChanged(); }
        }

        private string _resultText = "0";
        public string ResultText
        {
            get => _resultText;
            set { _resultText = value; OnPropertyChanged(); }
        }

        // the small line under the converter, e.g. "1 EUR = 1.0842 USD"
        private string _rateText;
        public string RateText
        {
            get => _rateText;
            set { _rateText = value; OnPropertyChanged(); }
        }

        // the day the rates are for, in the region format; on the same line, left
        private string _ratesDateText = "";
        public string RatesDateText
        {
            get => _ratesDateText;
            set { _ratesDateText = value; OnPropertyChanged(); }
        }


        // === commands ===

        public ICommand InputCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand BackspaceCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand RefreshCommand { get; }


        // === constructor ===

        public CurrencyViewModel()
        {
            InputCommand = new RelayCommand<string>(AddInput);
            ClearCommand = new RelayCommand<string>(_ => Clear());
            BackspaceCommand = new RelayCommand<string>(_ => Backspace());
            CalculateCommand = new RelayCommand<string>(_ => Calculate());
            RefreshCommand = new RelayCommand<string>(_ => RefreshRates());

            ApplyRates(LoadRates());
        }


        // === rates ===

        // the live feed, kept on disk for the next time it cannot be reached; else the kept one; else none
        private static RateTable? LoadRates()
        {
            try
            {
                RateTable fetched = GetCurrencyData.FetchAllRates();
                PersistenceService.Instance.SaveRates(fetched);
                return fetched;
            }
            catch
            {
                return PersistenceService.Instance.LoadRates();
            }
        }

        // the picker list is whatever the rates contain; the pair is the saved one, or EUR and USD when the
        // rates lack a saved code
        // (the list is only replaced for another set of codes, which a start without any rates needs)
        private void ApplyRates(RateTable? rates)
        {
            _converter = rates != null ? new ConvertCurrency(rates.Rates) : null;
            RatesDateText = rates?.Date?.ToString("d", CultureInfo.CurrentCulture) ?? "";

            List<string> codes = rates?.Rates.Keys.ToList() ?? new List<string>();
            if (!codes.SequenceEqual(AvailableCurrencies.Select(c => c.Code)))
            {
                AvailableCurrencies = codes.Select(CurrencyHelper.GetInfo).ToList();
                OnPropertyChanged(nameof(AvailableCurrencies));
            }

            SelectedCurrency1 = FindCurrency(PageStateService.Instance.CurrencyFrom)
                ?? FindCurrency(PageStateData.DefaultCurrencyFrom);
            SelectedCurrency2 = FindCurrency(PageStateService.Instance.CurrencyTo)
                ?? FindCurrency(PageStateData.DefaultCurrencyTo);

            UpdateRateText();
        }

        private CurrencyInfo? FindCurrency(string code)
        {
            return AvailableCurrencies.FirstOrDefault(c => c.Code == code);
        }

        // a refresh goes the way the start does
        private void RefreshRates()
        {
            ApplyRates(LoadRates());
        }


        // === input handling ===

        // the first digit replaces the placeholder zero; a decimal point keeps it
        private void AddInput(string sign)
        {
            if (InputText == "0" && sign != ".") InputText = "";
            InputText += sign;
        }

        private void Clear()
        {
            InputText = "0";
            ResultText = "0";
        }

        private void Backspace()
        {
            if (InputText.Length <= 1) InputText = "0";
            else InputText = InputText.Remove(InputText.Length - 1);
        }

        // parsed invariant; the keypad always types a dot
        private void Calculate()
        {
            if (_converter == null || SelectedCurrency1 == null || SelectedCurrency2 == null) return;

            if (double.TryParse(InputText, NumberStyles.Any, CultureInfo.InvariantCulture, out double amount))
            {
                ResultText = _converter.GetAmountCurrency2(SelectedCurrency1.Code, SelectedCurrency2.Code, amount);
            }
            else
            {
                ResultText = "Error";
            }
        }

        private void UpdateRateText()
        {
            if (_converter == null)
            {
                RateText = "No rates available";
                return;
            }

            if (SelectedCurrency1 == null || SelectedCurrency2 == null) return;

            string rate = _converter.GetCurrencyRate(SelectedCurrency1.Code, SelectedCurrency2.Code);
            RateText = $"1 {SelectedCurrency1.Code} = {rate} {SelectedCurrency2.Code}";
        }


        // === property changed ===

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
