namespace FluentMath.Persistence.Models
{
    // the saved bounds of one window, in px; the key in window-state.json names the window
    // (while maximized the rect stays the one it returns to)
    public class WindowState
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsMaximized { get; set; }
    }
}
