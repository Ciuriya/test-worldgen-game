using UnityEngine.SceneManagement;
using PendingName.WorldGen;
using PendingName.Log;

namespace PendingName.Core {
    public class GameplaySystem : CoreSystem {
        private bool _shouldGenerateWorld = false;
        private WorldGenerator _worldGenerator;
        private World _world;

        public void LoadGame() {
            CustomLogger.Instance.Log(LogLevel.Info, "Starting game loading...");

            _shouldGenerateWorld = true;

            SceneManager.LoadScene(2);
        }

        public override void OnSceneChanged(Scene currentScene, Scene nextScene) {
            base.OnSceneChanged(currentScene, nextScene);

            if (_shouldGenerateWorld) {
                _shouldGenerateWorld = false;
                CreateWorld();
            }
        }

        public override void EarlyUpdate() {
            base.EarlyUpdate();

            _worldGenerator?.EarlyUpdate();
        }

        private void CreateWorld() {
            WorldGeneratorData worldGenData = GameCore.Instance.GetScriptableObject<WorldGeneratorData>("DefaultWorldGenData");
            _worldGenerator = new WorldGenerator(worldGenData);
            _worldGenerator.StartGenerationSequence(OnWorldCreated);
        }

        private void OnWorldCreated(World world) {
			_world = world;
            _worldGenerator = null;

            // call entity system to spawn player?
		}
    }
}