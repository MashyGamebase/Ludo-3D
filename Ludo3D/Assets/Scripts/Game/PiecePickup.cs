using Board;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class PiecePickup : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam; // defaults to Camera.main if null
    public LayerMask cellMask;
    public LayerMask pieceMask;
    public BoardGrid grid;
    public BoardCell currentCell;

    [Header("Visual Offset")]
    [Tooltip("Offset applied on top of cell position. Example: (0,0.2,0) to keep the piece floating above the grid.")]
    public Vector3 pieceOffset = new Vector3(0f, 0.2f, 0f);

    [Header("Hover FX")]
    public bool hoverWhenSelected = true;
    public float hoverAmplitude = 0.1f;
    public float hoverFrequency = 4f;
    [Tooltip("If null, uses this transform. Prefer assigning a visual child.")]
    public Transform hoverGraphic;

    [Header("Movement")]
    [Tooltip("Seconds per single-cell hop")]
    public float stepDuration = 0.22f;
    [Tooltip("Bounce apex height in meters")]
    public float bounceHeight = 0.25f;
    [Tooltip("If true, allow Manhattan path (H first then V) when target not aligned.")]
    public bool allowManhattanPath = true;

    [Header("Stability")]
    [Tooltip("If true, pins world Y to cell Y + offset whenever not moving.")]
    public bool lockYToGrid = false;

    [Header("Events")]
    public UnityEvent onSelected;
    public UnityEvent onDeselected;
    public UnityEvent onMoveStart;
    public UnityEvent onMoveEnd;

    private bool _selected;
    private bool _moving;

    // Hover baselines
    private Vector3 _baseLocalPos;
    private Vector3 _baseWorldPos;
    private float _hoverTime;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (hoverGraphic == null) hoverGraphic = transform;

        if (grid != null && currentCell != null)
            transform.position = grid.GetCellWorldCenter(currentCell) + pieceOffset;

        _baseLocalPos = hoverGraphic.localPosition;
        _baseWorldPos = hoverGraphic.position;
    }

    void Update()
    {
        if (!_moving && lockYToGrid && grid != null && currentCell != null)
        {
            Vector3 basePos = grid.GetCellWorldCenter(currentCell) + pieceOffset;
            transform.position = new Vector3(transform.position.x, basePos.y, transform.position.z);
        }

        if (_moving) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (RayHit(transform, pieceMask))
            {
                ToggleSelect(true);
            }
            else
            {
                if (_selected && TryRaycastCell(out BoardCell targetCell))
                {
                    TryMoveTo(targetCell);
                }
                else if (_selected)
                {
                    ToggleSelect(false);
                }
            }
        }

        HandleHover();
    }

    void HandleHover()
    {
        bool hovering = _selected && hoverWhenSelected && hoverAmplitude > 0f && hoverFrequency > 0f;

        if (hovering)
        {
            _hoverTime += Time.deltaTime * hoverFrequency * Mathf.PI * 2f;
            float y = Mathf.Sin(_hoverTime) * hoverAmplitude;

            if (hoverGraphic == transform)
            {
                var p = transform.position + pieceOffset;
                p.y = _baseWorldPos.y + y;
                transform.position = p;
            }
            else
            {
                var lp = _baseLocalPos;
                lp.y += y;
                hoverGraphic.localPosition = lp;
            }
        }
        else
        {
            if (hoverGraphic == transform)
            {
                float newY = Mathf.Lerp(transform.position.y, _baseWorldPos.y, 10f * Time.deltaTime);
                var p = transform.position; p.y = newY; transform.position = p;
            }
            else
            {
                hoverGraphic.localPosition = Vector3.Lerp(hoverGraphic.localPosition, _baseLocalPos, 10f * Time.deltaTime);
            }
        }
    }

    public void ToggleSelect(bool on)
    {
        if (_moving) return;
        if (_selected == on) return;
        _selected = on;
        if (_selected) onSelected?.Invoke(); else onDeselected?.Invoke();

        _baseLocalPos = hoverGraphic.localPosition;
        _baseWorldPos = hoverGraphic.position;
        _hoverTime = 0f;
    }

    bool TryRaycastCell(out BoardCell cell)
    {
        cell = null;
        if (cam == null) return false;
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 1000f, cellMask))
        {
            cell = hit.collider.GetComponentInParent<BoardCell>();
            return cell != null;
        }
        return false;
    }

    bool RayHit(Transform t, LayerMask mask)
    {
        if (cam == null) return false;
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 1000f, mask))
            return hit.transform == t || hit.transform.IsChildOf(t);
        return false;
    }

    public void TryMoveTo(BoardCell target)
    {
        if (_moving || target == null || grid == null || currentCell == null) return;

        var path = BuildPath(currentCell, target);
        if (path.Count == 0) return;

        StartCoroutine(MovePath(path));
    }

    List<BoardCell> BuildPath(BoardCell start, BoardCell target)
    {
        var path = new List<BoardCell>();
        if (start == target) return path;

        int r = start.row, c = start.col;
        int dr = target.row - r;
        int dc = target.col - c;

        if (dr == 0 || dc == 0)
        {
            int stepR = dr == 0 ? 0 : (dr > 0 ? 1 : -1);
            int stepC = dc == 0 ? 0 : (dc > 0 ? 1 : -1);
            while (r != target.row || c != target.col)
            {
                r += stepR; c += stepC;
                if (grid.TryGetCell(r, c, out var next))
                    path.Add(next);
                else break;
            }
            return path;
        }

        if (!allowManhattanPath) return path;

        int stepC2 = dc > 0 ? 1 : -1;
        while (c != target.col)
        {
            c += stepC2;
            if (grid.TryGetCell(r, c, out var next))
                path.Add(next);
            else return path;
        }
        int stepR2 = dr > 0 ? 1 : -1;
        while (r != target.row)
        {
            r += stepR2;
            if (grid.TryGetCell(r, c, out var next))
                path.Add(next);
            else return path;
        }
        return path;
    }

    IEnumerator MovePath(List<BoardCell> path)
    {
        _moving = true;
        onMoveStart?.Invoke();
        ToggleSelect(false);

        foreach (var cell in path)
        {
            var startPos = transform.position;
            var endPos = grid.GetCellWorldCenter(cell) + pieceOffset;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, stepDuration);
                float u = Mathf.Clamp01(t);

                var p = Vector3.Lerp(startPos, endPos, u);

                float yArc = 4f * u * (1f - u) * bounceHeight;
                p.y = endPos.y + yArc; // baseline includes offset
                transform.position = p;

                yield return null;
            }

            transform.position = endPos;
            currentCell = cell;
        }

        onMoveEnd?.Invoke();
        _moving = false;

        _baseWorldPos = hoverGraphic.position;
        _baseLocalPos = hoverGraphic.localPosition;
        _hoverTime = 0f;
    }

    public void WarpTo(BoardCell cell)
    {
        if (grid == null || cell == null) return;
        currentCell = cell;
        transform.position = grid.GetCellWorldCenter(cell) + pieceOffset;
        _baseWorldPos = hoverGraphic.position;
        _baseLocalPos = hoverGraphic.localPosition;
        _hoverTime = 0f;
    }
}