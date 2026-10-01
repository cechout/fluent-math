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

        private ConvertCurrency _converter;


        // === display properties ===

        public List<CurrencyInfo> AvailableCurrencies { get; set; }

        // the equality guard keeps a resync of the two-way binding from bouncing UpdateRateText
        private CurrencyInfo _selectedCurrency1;
        public CurrencyInfo SelectedCurrency1
        {
            get => _selectedCurrency1;
            set
            {
                if (_selectedCurrency1 == value) return;
                _selectedCurrency1 = value;
                OnPropertyChanged();
                if (value != null) PageStateService.Instance.CurrencyFrom = value.Code;
                UpdateRateText();
            }
        }

        private CurrencyInfo _selectedCurrency2;
        public CurrencyInfo SelectedCurrency2
        {
            get => _selectedCurrency2;
            set
            {
                if (_selectedCurrency2 == value) return;
                _selectedCurrency2 = value;
                OnPropertyChanged();
                if (value != null) PageStateService.Instance.CurrencyTo = value.Code;
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


        // === commands ===

        public ICommand InputCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand BackspaceCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand RefreshCommand { get; }


        // === constructor ===

        // the picker list is whatever the ECB feed contains; the pair is the saved one, or EUR and USD when the
        // feed lacks a saved code
        public CurrencyViewModel()
        {
            _converter = new ConvertCurrency();

            AvailableCurrencies = _converter.ExchangeRates.Keys
                .Select(code => CurrencyHelper.GetInfo(code))
                .ToList();

            SelectedCurrency1 = FindCurrency(PageStateService.Instance.CurrencyFrom)
                ?? FindCurrency(PageStateData.DefaultCurrencyFrom);
            SelectedCurrency2 = FindCurrency(PageStateService.Instance.CurrencyTo)
                ?? FindCurrency(PageStateData.DefaultCurrencyTo);

            InputCommand = new RelayCommand<string>(AddInput);
            ClearCommand = new RelayCommand<string>(_ => Clear());
            BackspaceCommand = new RelayCommand<string>(_ => Backspace());
            CalculateCommand = new RelayCommand<string>(_ => Calculate());
            RefreshCommand = new RelayCommand<string>(_ => RefreshRates());
        }

        private CurrencyInfo? FindCurrency(string code)
        {
            return AvailableCurrencies.FirstOrDefault(c => c.Code == code);
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
            if (SelectedCurrency1 == null || SelectedCurrency2 == null) return;

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
            if (_converter == null || SelectedCurrency1 == null || SelectedCurrency2 == null) return;

            string rate = _converter.GetCurrencyRate(SelectedCurrency1.Code, SelectedCurrency2.Code);
            RateText = $"1 {SelectedCurrency1.Code} = {rate} {SelectedCurrency2.Code}";
        }

        // rates are fetched in the ConvertCurrency constructor, so a refresh means a new converter
        private void RefreshRates()
        {
            _converter = new ConvertCurrency();
            UpdateRateText();
        }


        // === property changed ===

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
