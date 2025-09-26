using System;
using UnityEngine;

// Small helper class that checks board rules and indices
public class BoardStateCheck
{
    // Convert flat index -> (x,y) on board
    public Vector2Int FromIndex(GameBoard board, int index)
    {
        return new Vector2Int(index % board.width, index / board.width);
    }

    // Check if flat index is inside board bounds
    public bool IndexInBounds(GameBoard board, int index)
    {
        var p = FromIndex(board, index);
        return p.x >= 0 && p.x < board.width && p.y >= 0 && p.y < board.height;
    }

    // Check if cell at index is empty
    public bool IsIndexEmpty(GameBoard board, int index)
    {
        var p = FromIndex(board, index);
        return board.IsEmpty(p.x, p.y);
    }

    // Move is legal if index in bounds, empty and adjacent to current player
    public bool IsLegalMoveForCurrent(GameBoard board, bool currentIsP1, int index)
    {
        if (!IndexInBounds(board, index)) return false;
        if (!IsIndexEmpty(board, index)) return false;

        var dst = FromIndex(board, index);
        var cur = currentIsP1 ? board.GetP1Pos() : board.GetP2Pos();
        return board.AreAdjacent(cur, dst);
    }

    // Break is legal if cell is empty
    public bool IsLegalBreakAtIndex(GameBoard board, int index)
    {
        return IsIndexEmpty(board, index);
    }

    // Does current player have any legal move?
    public bool CurrentHasMove(GameBoard board, bool currentIsP1)
    {
        var pos = currentIsP1 ? board.GetP1Pos() : board.GetP2Pos();
        return board.HasAnyMoveFrom(pos);
    }

    // Does opponent have any legal move?
    public bool OpponentHasMove(GameBoard board, bool currentIsP1)
    {
        var pos = currentIsP1 ? board.GetP2Pos() : board.GetP1Pos();
        return board.HasAnyMoveFrom(pos);
    }
}
