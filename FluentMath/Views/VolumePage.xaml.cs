using FluentMath.Models.Converters;
using FluentMath.Persistence.Services;
using FluentMath.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace FluentMath.Views
{
    public sealed partial class VolumePage : Page, ICompactPage
    {
        public VolumePage()
        {
            this.InitializeComponent();
            Panel.ViewModel = new ConverterViewModel(VolumeUnits.Create(), App.Settings, SettingsService.Instance.Converter);
        }

        public Size CompactStartSize => Panel.CompactStartSize;
        public Size CompactMinSize => Panel.CompactMinSize;
        public void SetCompactLayout(bool compact) => Panel.SetCompactLayout(compact);
    }
}
