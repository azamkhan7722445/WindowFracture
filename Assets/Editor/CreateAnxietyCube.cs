using UnityEditor;
using UnityEngine;

public static class CreateAnxietyCube
{
    [MenuItem("Tools/Create Anxiety Cube")]
    static void Create()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "AnxietyCube";
        cube.transform.localScale = new Vector3(2f, 2f, 2f);
        cube.AddComponent<Anxiety>();
        Selection.activeGameObject = cube;
        Undo.RegisterCreatedObjectUndo(cube, "Create Anxiety Cube");
    }
}
