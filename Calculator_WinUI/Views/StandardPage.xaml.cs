using Microsoft.UI.Xaml.Controls;
using Calculator_WinUI.ViewModels;

namespace Calculator_WinUI.Views
{
    public sealed partial class StandardPage : Page
    {
        public StandardViewModel ViewModel { get; }

        public StandardPage()
        {
            ViewModel = new StandardViewModel();
            this.InitializeComponent();
        }
    }
}
