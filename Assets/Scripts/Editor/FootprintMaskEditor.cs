using UnityEditor;
using UnityEngine;
using WheatFarm.Core.Data;

namespace WheatFarm.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="PlaceableData"/> that replaces the raw
    /// <c>FootprintRows</c> string array with a visual grid of toggle buttons.
    /// </summary>
    [CustomEditor(typeof(PlaceableData))]
    public class FootprintMaskEditor : UnityEditor.Editor
    {
        private const float CellSize = 22f;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "FootprintRows");

            EditorGUILayout.Space();
            DrawFootprintSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFootprintSection()
        {
            var rowsProp = serializedObject.FindProperty("FootprintRows");
            var gridSizeProp = serializedObject.FindProperty("GridSize");

            EditorGUILayout.LabelField("Footprint", EditorStyles.boldLabel);

            var rows = ReadRows(rowsProp);
            var gridSize = gridSizeProp.vector2IntValue;

            var isValid = IsValid(rows);

            if (!isValid)
            {
                EditorGUILayout.HelpBox(
                    "Маска невалидна — будет использован прямоугольник GridSize",
                    MessageType.Warning);
            }

            var width = isValid ? rows[0].Length : Mathf.Max(1, gridSize.x);
            var height = isValid ? rows.Length : Mathf.Max(1, gridSize.y);

            // Ensure we always have a well-formed grid to draw and edit.
            var editRows = isValid ? rows : MakeSolidRows(width, height);

            DrawGrid(rowsProp, editRows, width, height);

            EditorGUILayout.Space();
            DrawButtons(rowsProp, editRows, width, height, gridSize);
        }

        private void DrawGrid(SerializedProperty rowsProp, string[] rows, int width, int height)
        {
            for (var y = 0; y < height; y++)
            {
                EditorGUILayout.BeginHorizontal();
                for (var x = 0; x < width; x++)
                {
                    var occupied = rows[y][x] == 'X' || rows[y][x] == 'x';
                    var newOccupied = GUILayout.Toggle(
                        occupied,
                        GUIContent.none,
                        GUI.skin.button,
                        GUILayout.Width(CellSize),
                        GUILayout.Height(CellSize));

                    if (newOccupied != occupied)
                    {
                        var chars = rows[y].ToCharArray();
                        chars[x] = newOccupied ? 'X' : '.';
                        rows[y] = new string(chars);
                        WriteRows(rowsProp, rows);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawButtons(SerializedProperty rowsProp, string[] rows, int width, int height, Vector2Int gridSize)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("+Row"))
            {
                var newRows = new string[height + 1];
                System.Array.Copy(rows, newRows, height);
                newRows[height] = new string('.', width);
                WriteRows(rowsProp, newRows);
            }

            if (GUILayout.Button("−Row") && height > 1)
            {
                var newRows = new string[height - 1];
                System.Array.Copy(rows, newRows, height - 1);
                WriteRows(rowsProp, newRows);
            }

            if (GUILayout.Button("+Col"))
            {
                var newRows = new string[height];
                for (var y = 0; y < height; y++)
                    newRows[y] = rows[y] + ".";
                WriteRows(rowsProp, newRows);
            }

            if (GUILayout.Button("−Col") && width > 1)
            {
                var newRows = new string[height];
                for (var y = 0; y < height; y++)
                    newRows[y] = rows[y].Substring(0, width - 1);
                WriteRows(rowsProp, newRows);
            }

            if (GUILayout.Button("Fill from GridSize"))
            {
                var w = Mathf.Max(1, gridSize.x);
                var h = Mathf.Max(1, gridSize.y);
                WriteRows(rowsProp, MakeSolidRows(w, h));
            }

            EditorGUILayout.EndHorizontal();
        }

        private static string[] ReadRows(SerializedProperty rowsProp)
        {
            var rows = new string[rowsProp.arraySize];
            for (var i = 0; i < rows.Length; i++)
                rows[i] = rowsProp.GetArrayElementAtIndex(i).stringValue ?? string.Empty;
            return rows;
        }

        private void WriteRows(SerializedProperty rowsProp, string[] rows)
        {
            Undo.RecordObject(serializedObject.targetObject, "Edit Footprint Mask");

            rowsProp.arraySize = rows.Length;
            for (var i = 0; i < rows.Length; i++)
                rowsProp.GetArrayElementAtIndex(i).stringValue = rows[i];

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }

        private static string[] MakeSolidRows(int width, int height)
        {
            var rows = new string[height];
            for (var y = 0; y < height; y++)
                rows[y] = new string('X', width);
            return rows;
        }

        private static bool IsValid(string[] rows)
        {
            if (rows == null || rows.Length == 0)
                return false;

            var width = rows[0]?.Length ?? 0;
            if (width == 0)
                return false;

            var occupied = false;
            foreach (var row in rows)
            {
                if (row == null || row.Length != width)
                    return false;

                foreach (var c in row)
                {
                    if (c == 'X' || c == 'x')
                        occupied = true;
                    else if (c != '.')
                        return false;
                }
            }

            return occupied;
        }
    }
}
