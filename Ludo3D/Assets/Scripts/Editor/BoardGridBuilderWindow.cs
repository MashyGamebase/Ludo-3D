using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Board
{
    public class BoardGridBuilderWindow : EditorWindow
    {
        private Transform parent;
        private GameObject cellPrefab;
        private int rows = 8;
        private int cols = 8;
        private Vector2 cellSize = new Vector2(1, 1);
        private Vector2 spacing = new Vector2(0.05f, 0.05f);
        private bool centerOnParent = true;
        private bool addBoardGridToParent = true;
        private float pieceY = 0f;

        [MenuItem("Tools/Board/Grid Builder")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<BoardGridBuilderWindow>("Board Grid Builder");
            wnd.minSize = new Vector2(340, 320);
        }

        void OnGUI()
        {
            GUILayout.Label("Grid Layout", EditorStyles.boldLabel);
            parent = (Transform)EditorGUILayout.ObjectField(new GUIContent("Parent"), parent, typeof(Transform), true);
            cellPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Cell Prefab"), cellPrefab, typeof(GameObject), false);

            rows = Mathf.Max(1, EditorGUILayout.IntField("Rows", rows));
            cols = Mathf.Max(1, EditorGUILayout.IntField("Cols", cols));

            cellSize = EditorGUILayout.Vector2Field("Cell Size (x,z)", cellSize);
            spacing = EditorGUILayout.Vector2Field("Spacing (x,z)", spacing);
            centerOnParent = EditorGUILayout.Toggle(new GUIContent("Center on Parent"), centerOnParent);
            addBoardGridToParent = EditorGUILayout.Toggle(new GUIContent("Add BoardGrid to Parent"), addBoardGridToParent);
            pieceY = EditorGUILayout.FloatField(new GUIContent("Piece Y Offset"), pieceY);

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledGroupScope(parent == null || cellPrefab == null))
            {
                if (GUILayout.Button("Build / Rebuild Grid"))
                    BuildGrid();
                if (GUILayout.Button("Clear Children"))
                    ClearChildren();
            }

            EditorGUILayout.HelpBox("• Cells are named Cell_r_c\n• Each cell gets BoardCell + BoxCollider if missing\n• Parent gets BoardGrid (optional)\n• Set layers on the prefab for clicking", MessageType.Info);
        }

        void ClearChildren()
        {
            if (parent == null) return;
            Undo.RegisterFullObjectHierarchyUndo(parent.gameObject, "Clear Grid");
            for (int i = parent.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
            EditorUtility.SetDirty(parent.gameObject);
        }

        void BuildGrid()
        {
            if (parent == null || cellPrefab == null) return;

            Undo.RegisterFullObjectHierarchyUndo(parent.gameObject, "Build Grid");
            ClearChildren();

            var parentGo = parent.gameObject;
            var grid = parentGo.GetComponent<BoardGrid>();
            if (addBoardGridToParent)
            {
                if (grid == null) grid = Undo.AddComponent<BoardGrid>(parentGo);
                grid.cellSize = cellSize;
                grid.pieceY = pieceY;
            }

            // Compute origin so grid is centered if requested
            Vector2 pitch = cellSize + spacing;
            float totalW = cols * pitch.x - spacing.x;
            float totalH = rows * pitch.y - spacing.y;

            Vector3 origin = parent.position;
            if (centerOnParent)
            {
                origin -= new Vector3((totalW - cellSize.x) * 0.5f, 0f, (totalH - cellSize.y) * 0.5f);
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var x = origin.x + c * pitch.x;
                    var z = origin.z + r * pitch.y;
                    var pos = new Vector3(x, parent.position.y, z);

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(cellPrefab, parent);
                    go.name = $"Cell_{r}_{c}";
                    go.transform.position = pos;
                    go.transform.rotation = parent.rotation;
                    go.transform.localScale = new Vector3(cellSize.x, 1f, cellSize.y);

                    // Ensure collider
                    var col = go.GetComponent<Collider>();
                    if (col == null)
                        col = Undo.AddComponent<BoxCollider>(go);
                    // Thin Y so raycasts are reliable
                    if (col is BoxCollider bc)
                        bc.size = new Vector3(1f, 0.1f, 1f);

                    // Ensure BoardCell
                    var cell = go.GetComponent<BoardCell>();
                    if (cell == null)
                        cell = Undo.AddComponent<BoardCell>(go);
                    cell.row = r;
                    cell.col = c;

                    // Ensure it registers to grid at edit-time
                    if (grid == null) grid = parentGo.GetComponent<BoardGrid>();
                }
            }

            EditorUtility.SetDirty(parentGo);
            if (grid != null) EditorUtility.SetDirty(grid);
        }
    }
}