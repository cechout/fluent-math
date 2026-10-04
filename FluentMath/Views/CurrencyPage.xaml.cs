using FluentMath.Models;
using FluentMath.Models.Converters;
using FluentMath.Persistence.Services;
using FluentMath.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace FluentMath.Views
{
    public sealed partial class CurrencyPage : Page, ICompactPage
    {
        public CurrencyPage()
        {
            this.InitializeComponent();
            Panel.ViewModel = new ConverterViewModel(new CurrencyUnitSource(LoadRates));
        }

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

        public Size CompactStartSize => Panel.CompactStartSize;
        public Size CompactMinSize => Panel.CompactMinSize;
        public void SetCompactLayout(bool compact) => Panel.SetCompactLayout(compact);
    }
}
