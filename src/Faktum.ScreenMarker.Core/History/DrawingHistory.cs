using Faktum.ScreenMarker.Core.Drawing;

namespace Faktum.ScreenMarker.Core.History;

public interface IHistoryCommand
{
    long EstimatedSize { get; }

    void Apply(DrawingSession session);

    void Undo(DrawingSession session);
}

public sealed class AddObjectCommand : IHistoryCommand
{
    public AddObjectCommand(DrawingObject target, int insertionIndex)
    {
        Target = target;
        InsertionIndex = insertionIndex;
        EstimatedSize = DrawingHistory.EstimateObject(target);
    }

    public DrawingObject Target { get; }

    public int InsertionIndex { get; }

    public long EstimatedSize { get; }

    public void Apply(DrawingSession session) => session.AddObjectInternal(Target, InsertionIndex);

    public void Undo(DrawingSession session) => session.RemoveObjectInternal(Target.Id);
}

public sealed class RemoveObjectCommand : IHistoryCommand
{
    public RemoveObjectCommand(DrawingObject target, int originalIndex)
    {
        Target = target;
        OriginalIndex = originalIndex;
        EstimatedSize = DrawingHistory.EstimateObject(target);
    }

    public DrawingObject Target { get; }

    public int OriginalIndex { get; }

    public long EstimatedSize { get; }

    public void Apply(DrawingSession session) => session.RemoveObjectInternal(Target.Id);

    public void Undo(DrawingSession session) => session.AddObjectInternal(Target, OriginalIndex);
}

public sealed class ClearAllCommand : IHistoryCommand
{
    public ClearAllCommand(IReadOnlyList<DrawingObject> previousObjects)
    {
        PreviousObjects = previousObjects;
        EstimatedSize = previousObjects.Sum(DrawingHistory.EstimateObject);
    }

    public IReadOnlyList<DrawingObject> PreviousObjects { get; }

    public long EstimatedSize { get; }

    public void Apply(DrawingSession session) => session.ClearObjectsInternal();

    public void Undo(DrawingSession session)
    {
        session.ClearObjectsInternal();
        for (var i = 0; i < PreviousObjects.Count; i++)
        {
            session.AddObjectInternal(PreviousObjects[i], i);
        }
    }
}

/// <summary>
/// Undo/redo history with byte-budget accounting across both stacks.
/// <see cref="MaxCommandCount"/> applies to the undo stack only.
/// <see cref="MaxEstimatedBytes"/> applies to the combined undo and redo retained estimate.
/// </summary>
public sealed class DrawingHistory
{
    public const int MaxCommandCount = 500;
    public const long MaxEstimatedBytes = 64L * 1024L * 1024L;

    private readonly LinkedList<IHistoryCommand> _undo = new();
    private readonly Stack<IHistoryCommand> _redo = new();
    private long _estimatedBytes;

    public int UndoCount => _undo.Count;

    public int RedoCount => _redo.Count;

    public long EstimatedBytes => _estimatedBytes;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Execute(IHistoryCommand command, DrawingSession session)
    {
        command.Apply(session);
        _undo.AddLast(command);
        SubtractDiscardedRedoEstimates();
        _redo.Clear();
        _estimatedBytes += command.EstimatedSize;
        TrimIfNeeded();
    }

    public void Undo(DrawingSession session)
    {
        if (_undo.Last is null)
        {
            return;
        }

        var command = _undo.Last.Value;
        _undo.RemoveLast();
        command.Undo(session);
        _redo.Push(command);
    }

    public void Redo(DrawingSession session)
    {
        if (_redo.Count == 0)
        {
            return;
        }

        var command = _redo.Pop();
        command.Apply(session);
        _undo.AddLast(command);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _estimatedBytes = 0;
    }

    public long ComputeRetainedEstimate() =>
        _undo.Sum(command => command.EstimatedSize) + _redo.Sum(command => command.EstimatedSize);

    private void SubtractDiscardedRedoEstimates()
    {
        foreach (var command in _redo)
        {
            _estimatedBytes -= command.EstimatedSize;
        }

        if (_estimatedBytes < 0)
        {
            _estimatedBytes = 0;
        }
    }

    private void TrimIfNeeded()
    {
        while (_undo.Count > MaxCommandCount || _estimatedBytes > MaxEstimatedBytes)
        {
            if (_undo.First is null)
            {
                break;
            }

            var oldest = _undo.First.Value;
            _undo.RemoveFirst();
            _estimatedBytes -= oldest.EstimatedSize;
            if (_estimatedBytes < 0)
            {
                _estimatedBytes = 0;
            }
        }
    }

    internal static long EstimateObject(DrawingObject obj) =>
        obj switch
        {
            FreehandStroke stroke => 128L + (stroke.Points.Count * 16L),
            TextAnnotation text => 256L + (text.Text.Length * 2L),
            _ => 256L,
        };
}
