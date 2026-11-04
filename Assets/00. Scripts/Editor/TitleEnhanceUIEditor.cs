using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TitleEnhanceUI))]
public class TitleEnhanceUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

        if (GUILayout.Button("레벨 / 다이아몬드 초기화"))
        {
            TitleEnhanceUI ui = (TitleEnhanceUI)target;
            Undo.RecordObject(ui, "Reset Title Enhance Levels And Diamonds");
            ui.ResetAllLevels();
            EditorUtility.SetDirty(ui);
            if (!Application.isPlaying)
                EditorSceneManagerMarkDirty(ui);
        }
    }

    private static void EditorSceneManagerMarkDirty(Object targetObject)
    {
        if (targetObject is Component component)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }
}
