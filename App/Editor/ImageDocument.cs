using System.Windows.Media.Imaging;

namespace DocumentationImageHelper.Editor;

/// <summary>
/// Holds the image currently being edited together with its undo/redo history.
/// The image is stored as a single immutable (frozen) bitmap. Every edit produces
/// a brand new bitmap; the previous one is pushed onto the undo history so that
/// undo and redo are simply a matter of swapping which bitmap is current.
/// </summary>
public class ImageDocument
{
    /// <summary>Maximum number of undo steps kept, to bound memory use on large screenshots.</summary>
    private const int MaxHistory = 20;

    // The oldest undo entry is at index 0 so the list can be trimmed from the front
    // once it grows past MaxHistory.
    private readonly List<BitmapSource> _undo = new();
    private readonly Stack<BitmapSource> _redo = new();

    /// <summary>The image being shown, or null when nothing has been pasted yet.</summary>
    public BitmapSource? Current { get; private set; }

    /// <summary>Raised whenever the current image or the history changes.</summary>
    public event EventHandler? Changed;

    public bool HasImage => Current != null;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// Replaces the whole image and clears the history. Used when a new image is
    /// pasted, since that starts a fresh editing session.
    /// </summary>
    public void Reset(BitmapSource image)
    {
        _undo.Clear();
        _redo.Clear();
        Current = Freeze(image);
        OnChanged();
    }

    /// <summary>Records an edited bitmap as a new history step.</summary>
    public void Commit(BitmapSource image)
    {
        if (Current != null)
        {
            _undo.Add(Current);
            if (_undo.Count > MaxHistory)
                _undo.RemoveAt(0);
        }

        // Any edit invalidates the redo branch.
        _redo.Clear();
        Current = Freeze(image);
        OnChanged();
    }

    public void Undo()
    {
        if (!CanUndo || Current == null)
            return;

        _redo.Push(Current);
        Current = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        OnChanged();
    }

    public void Redo()
    {
        if (!CanRedo || Current == null)
            return;

        _undo.Add(Current);
        Current = _redo.Pop();
        OnChanged();
    }

    // Frozen bitmaps are immutable and safe to share, which is required before
    // putting them on the clipboard or reusing them across history steps.
    private static BitmapSource Freeze(BitmapSource image)
    {
        if (image.CanFreeze && !image.IsFrozen)
            image.Freeze();
        return image;
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
