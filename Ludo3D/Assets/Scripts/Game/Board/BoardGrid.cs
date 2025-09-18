using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Board
{
    public class BoardGrid : MonoBehaviour
    {
        [Tooltip("World-space size of a single cell (x,z). Y is ignored.")]
        public Vector2 cellSize = new Vector2(1, 1);

        [Tooltip("Optional vertical offset for pieces to sit on top of cells.")]
        public float pieceY = 0.0f;

        // Row-major dictionary: key = (row,col)
        private readonly Dictionary<(int r, int c), BoardCell> _cells = new();

        public void Register(BoardCell cell)
        {
            var key = (cell.row, cell.col);
            if (!_cells.ContainsKey(key))
                _cells.Add(key, cell);
            else
                _cells[key] = cell;
        }

        public void Unregister(BoardCell cell)
        {
            var key = (cell.row, cell.col);
            if (_cells.ContainsKey(key))
                _cells.Remove(key);
        }

        public bool TryGetCell(int row, int col, out BoardCell cell) =>
            _cells.TryGetValue((row, col), out cell);

        public Vector3 GetCellWorldCenter(BoardCell cell)
        {
            if (cell == null) return transform.position;
            var pos = cell.transform.position;
            pos.y = pieceY;
            return pos;
        }

        public IEnumerable<BoardCell> AllCells() => _cells.Values;
    }
}
