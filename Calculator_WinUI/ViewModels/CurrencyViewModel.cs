using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Calculator_WinUI.Models;

namespace Calculator_WinUI.ViewModels
{
    public class CurrencyViewModel : INotifyPropertyChanged
    {
        private ConvertCurrency _converter;

        // ui properties
        public List<CurrencyInfo> AvailableCurrencies { get; set; }

        private CurrencyInfo _selectedCurrency1;
        public CurrencyInfo SelectedCurrency1
        {
            get => _selectedCurrency1;
            set
            {
                if (_selectedCurrency1 == value) return; // prevent stackoverflow 
                _selectedCurrency1 = value;
                OnPropertyChanged();
                UpdateRateText();
            }
        }

        private CurrencyInfo _selectedCurrency2;
        public CurrencyInfo SelectedCurrency2
        {
            get => _selectedCurrency2;
            set
            {
                if (_selectedCurrency2 == value) return; // prevent stackoverflow 
                _selectedCurrency2 = value;
                OnPropertyChanged();
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

        private string _rateText;
        public string RateText
        {
            get => _rateText;
            set { _rateText = value; OnPropertyChanged(); }
        }


        // button commands
        public ICommand InputCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand BackspaceCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand RefreshCommand { get; }


        // constructor
        public CurrencyViewModel()
        {
            _converter = new ConvertCurrency();

            // dynamically build the list of currency objects
            AvailableCurrencies = _converter.ExchangeRates.Keys
                .Select(code => CurrencyHelper.GetInfo(code))
                .ToList();

            // standard currencies
            SelectedCurrency1 = AvailableCurrencies.FirstOrDefault(c => c.Code == "EUR");
            SelectedCurrency2 = AvailableCurrencies.FirstOrDefault(c => c.Code == "USD");

            // connect buttons to the methods
            InputCommand = new RelayCommand<string>(AddInput);
            ClearCommand = new RelayCommand<string>(_ => Clear());
            BackspaceCommand = new RelayCommand<string>(_ => Backspace());
            CalculateCommand = new RelayCommand<string>(_ => Calculate());
            RefreshCommand = new RelayCommand<string>(_ => RefreshRates());
        }


        // logic methods for the buttons
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

        private void Calculate()
        {
            if (SelectedCurrency1 == null || SelectedCurrency2 == null) return;

            // converts the input string to a double
            if (double.TryParse(InputText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double amount))
            {
                // we only pass the .Code string to the math logic
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

            // we only pass the .Code string to the math logic
            string rate = _converter.GetCurrencyRate(SelectedCurrency1.Code, SelectedCurrency2.Code);
            RateText = $"1 {SelectedCurrency1.Code} = {rate} {SelectedCurrency2.Code}";
        }

        private void RefreshRates()
        {
            // recreates the converter object to pull the latest exchange rates
            _converter = new ConvertCurrency();
            UpdateRateText();
        }


        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
