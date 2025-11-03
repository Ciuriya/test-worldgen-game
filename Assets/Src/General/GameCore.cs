using System.Collections.Generic;
using PendingName.Log;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PendingName.Core {
    public class GameCore : MonoBehaviour {
        public static GameCore Instance {
            get => _instance;
            set => _instance = value;
        }
        private static GameCore _instance;

        public List<GameObject> Prefabs;
        public List<ScriptableObject> ScriptableObjects;

        private List<CoreSystem> _systems;
        private string _sceneName;

        [RuntimeInitializeOnLoadMethod]
        private static void ResetInstance() => _instance = null;

        void Start() {
            if (_instance != null) {
                Destroy(this);
                return;
            }

            _sceneName = SceneManager.GetActiveScene().name;

            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSystems();

            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        void Update() {
            foreach (CoreSystem system in _systems) system.EarlyUpdate();
            foreach (CoreSystem system in _systems) system.Update();
        }

        void LateUpdate() {
            foreach (CoreSystem system in _systems) system.LateUpdate();
        }

		void OnDestroy() {
            SceneManager.activeSceneChanged -= OnSceneChanged;
		}

		private void InitializeSystems() {
            _systems = new List<CoreSystem> {
                new PrefabSystem(),
                new UISystem(),
                new GameplaySystem(),
                new SettingsSystem()
            };

            foreach (CoreSystem system in _systems) system.EarlyStart();
            foreach (CoreSystem system in _systems) system.Start();
            foreach (CoreSystem system in _systems) system.LateStart();
        }

        private void OnSceneChanged(Scene currentScene, Scene nextScene) {
            CustomLogger.Instance.Log(LogLevel.Info, $"Changing scenes from {_sceneName} to {nextScene.name}!");

            _sceneName = nextScene.name;

            foreach (CoreSystem system in _systems) system.OnSceneChanged(currentScene, nextScene);
        }

        public T GetSystem<T>() where T : CoreSystem {
            foreach (CoreSystem system in _systems)
                if (system is T result)
                    return result;

            return null;
        }

        // some quick shortcuts
        public GameObject GetPrefab(string name) => GetSystem<PrefabSystem>().GetPrefab(name);

        public T GetScriptableObject<T>(string name) where T : ScriptableObject =>
            GetSystem<PrefabSystem>().GetScriptableObject<T>(name);
    }
}
