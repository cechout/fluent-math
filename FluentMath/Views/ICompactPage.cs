using Windows.Foundation;

namespace FluentMath.Views
{
    // a page that can be shown in compact mode; MainWindow owns the mode, the page brings its sizes
    public interface ICompactPage
    {
        // in px; the first compact window of a session on this page
        Size CompactStartSize { get; }

        // in px; the smallest the page gets while compact, without title bar and resize border
        Size CompactMinSize { get; }

        // hides the page header and switches to the compact row floors
        void SetCompactLayout(bool compact);
    }
}
