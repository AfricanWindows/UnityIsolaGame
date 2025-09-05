using System.Collections.Generic;
using UnityEngine;

public class GameBoard
{
    public enum SlotState
    {
        Empty, P1, P2, Broken
    }

    public readonly int width;
    public readonly int height;

    private SlotState[,] cells;
    private Vector2Int p1Pos;   // blue
    private Vector2Int p2Pos;   // red

    public GameBoard(int w, int h)
    {
        width = Mathf.Max(3, w);
        height = Mathf.Max(3, h);
        cells = new SlotState[width, height];
    }

    // Clear and place players at start
    public void ResetStart()
    {
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                cells[x, y] = SlotState.Empty;

        p1Pos = new Vector2Int(width / 2, 0);
        cells[p1Pos.x, p1Pos.y] = SlotState.P1;

        p2Pos = new Vector2Int(width / 2, height - 1);
        cells[p2Pos.x, p2Pos.y] = SlotState.P2;
    }

    // --- accessors ---
    public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    public SlotState Get(int x, int y)
    {
        if (!InBounds(x, y)) return SlotState.Broken; // safe guard
        return cells[x, y];
    }

    public void Set(int x, int y, SlotState s)
    {
        if (InBounds(x, y)) cells[x, y] = s;
    }

    public bool IsEmpty(int x, int y) => InBounds(x, y) && cells[x, y] == SlotState.Empty;

    public Vector2Int GetP1Pos() => p1Pos;
    public Vector2Int GetP2Pos() => p2Pos;
    public void SetP1Pos(Vector2Int p) => p1Pos = p;
    public void SetP2Pos(Vector2Int p) => p2Pos = p;

    // --- rules helpers ---
    public bool AreAdjacent(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return (dx <= 1 && dy <= 1);
    }

    public bool HasAnyMoveFrom(Vector2Int pos)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = pos.x + dx, ny = pos.y + dy;
                if (IsEmpty(nx, ny)) return true;
            }
        return false;
    }

    public List<Vector2Int> GetLegalMovesFrom(Vector2Int pos)
    {
        var list = new List<Vector2Int>();
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = pos.x + dx, ny = pos.y + dy;
                if (IsEmpty(nx, ny)) list.Add(new Vector2Int(nx, ny));
            }
        return list;
    }

    public List<Vector2Int> GetAllEmpty()
    {
        var list = new List<Vector2Int>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (cells[x, y] == SlotState.Empty)
                    list.Add(new Vector2Int(x, y));
        return list;
    }
}
