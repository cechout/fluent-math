using Windows.Foundation;

namespace FluentMath.Views
{
    // a page that can be shown in compact mode; the window owns the mode, see MainWindow, and each page brings
    // the sizes it needs and lays itself out for it
    public interface ICompactPage
    {
        // the first compact window of a session on this page, in pixels; after that it comes back at the size
        // it was dragged to
        Size CompactStartSize { get; }

        // the smallest the page itself can get while compact, in pixels; the window adds its title bar and
        // its resize border on top
        Size CompactMinSize { get; }

        // compact hides the page header, since the title bar carries the way back, and drops the row floors
        // to the compact ones
        void SetCompactLayout(bool compact);
    }
}
