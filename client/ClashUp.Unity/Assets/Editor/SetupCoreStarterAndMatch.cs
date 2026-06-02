using ClashUp.Client.CoreStarter;
using ClashUp.Client.Match;
using ClashUp.Client.Matchmaking;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClashUp.Editor
{
    public static class SetupCoreStarterAndMatch
    {
        [MenuItem("Tools/Setup CoreStarter, Matchmaking & Match Scenes (one-time)")]
        public static void Run()
        {
            CreateCoreStarterScene();
            CreateMatchmakingScene();
            CreateMatchScene();
            Debug.Log("[Setup] CoreStarter, Matchmaking & Match scenes created and added to build settings.");
        }

        private static void CreateCoreStarterScene()
        {
            const string sceneDir = "Assets/Core/CoreStarter/Content/Scenes";

            EnsureFolder("Assets/Core/CoreStarter/Content");
            EnsureFolder(sceneDir);

            const string scenePath = sceneDir + "/CoreStarter.unity";

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var root = new GameObject("CoreStarterLifetimeScope");
            root.AddComponent<CoreStarterLifetimeScope>();

            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.CloseScene(scene, true);

            AddToBuildSettings(scenePath);
            Debug.Log($"[Setup] Created scene: {scenePath}");
        }

        private static void CreateMatchmakingScene()
        {
            const string sceneDir = "Assets/Core/Matchmaking/Content/Scenes";

            EnsureFolder("Assets/Core/Matchmaking/Content");
            EnsureFolder(sceneDir);

            const string scenePath = sceneDir + "/Matchmaking.unity";

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var root = new GameObject("MatchmakingLifetimeScope");
            root.AddComponent<MatchmakingLifetimeScope>();

            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.CloseScene(scene, true);

            AddToBuildSettings(scenePath);
            Debug.Log($"[Setup] Created scene: {scenePath}");
        }

        private static void CreateMatchScene()
        {
            const string sceneDir = "Assets/Core/Match/Content/Scenes";

            EnsureFolder("Assets/Core/Match/Content");
            EnsureFolder(sceneDir);

            const string scenePath = sceneDir + "/Match.unity";

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var root = new GameObject("MatchLifetimeScope");
            root.AddComponent<MatchLifetimeScope>();

            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.CloseScene(scene, true);

            AddToBuildSettings(scenePath);
            Debug.Log($"[Setup] Created scene: {scenePath}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
            var folderName = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
