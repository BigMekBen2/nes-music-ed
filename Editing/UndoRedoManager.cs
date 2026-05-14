using System;
using System.Collections.Generic;

namespace NESMusicEditor.Editing;

public class UndoRedoManager
{
    private readonly Stack<(Action undo, Action redo)> _undoStack = new();
    private readonly Stack<(Action undo, Action redo)> _redoStack = new();

    public void Execute(Action doAction, Action undoAction)
    {
        doAction();
        _undoStack.Push((undoAction, doAction));
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (_undoStack.TryPop(out var pair))
        {
            pair.undo();
            _redoStack.Push(pair);
        }
    }

    public void Redo()
    {
        if (_redoStack.TryPop(out var pair))
        {
            pair.redo();
            _undoStack.Push(pair);
        }
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
}
