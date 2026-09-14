namespace ModelEditor
{
    /// <summary>
    /// Interface for editor action controllers that handle undo and redo operations.
    /// </summary>
    /// <remarks>
    /// Implement this interface to provide custom undo/redo functionality in the model editor.
    /// </remarks>
    public interface IEditorActionController
    {
        void Undo();
        void Redo();
    }
}
