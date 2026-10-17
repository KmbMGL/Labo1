#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Workshop;

/// <summary>
/// Создаёт и сохраняет сцену Assets/Scenes/Workshop.unity (один объект WorkshopBootstrap).
/// Вызывается автоматически при первом открытии проекта и вручную: меню Цех -> Создать сцену цеха.
/// </summary>
public static class CreateWorkshopScene
{
    const string ScenePath = "Assets/Scenes/Workshop.unity";

    [InitializeOnLoadMethod]
    static void AutoCreate()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ScenePath) && !EditorApplication.isPlayingOrWillChangePlaymode)
                Create(false);
        };
    }

    [MenuItem("Цех/Создать сцену цеха")]
    static void CreateMenu() { Create(true); }

    static void Create(bool open)
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("WorkshopBootstrap").AddComponent<WorkshopBootstrap>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.Refresh();
        if (open) Debug.Log("Сцена создана: " + ScenePath + ". Нажмите Play.");
    }
}
#endif
