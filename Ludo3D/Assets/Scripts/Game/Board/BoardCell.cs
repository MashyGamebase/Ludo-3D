using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Board
{
    [RequireComponent(typeof(Collider))]
    public class BoardCell : MonoBehaviour
    {
        public int row;
        public int col;
        [Tooltip("Optional visual for hover/selection feedback on cells.")]
        public Renderer highlightRenderer;
        [ColorUsage(false, true)] public Color highlightColor = new Color(0.8f, 0.8f, 0.2f, 1f);

        private Color _originalColor;
        private Material _matInstance;
        private BoardGrid _grid;

        void Awake()
        {
            _grid = GetComponentInParent<BoardGrid>();
            if (_grid) _grid.Register(this);

            if (highlightRenderer != null)
            {
                _matInstance = highlightRenderer.material; // instanced
                _originalColor = _matInstance.HasProperty("_BaseColor")
                    ? _matInstance.GetColor("_BaseColor")
                    : highlightRenderer.sharedMaterial.color;
            }
        }

        void OnDestroy()
        {
            if (_grid) _grid.Unregister(this);
        }

        public void SetHighlighted(bool on)
        {
            if (highlightRenderer == null || _matInstance == null) return;

            if (_matInstance.HasProperty("_BaseColor"))
                _matInstance.SetColor("_BaseColor", on ? highlightColor : _originalColor);
            else
                _matInstance.color = on ? highlightColor : _originalColor;
        }
    }
}
