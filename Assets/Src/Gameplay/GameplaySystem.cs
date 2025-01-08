using UnityEngine;
using UnityEngine.SceneManagement;

public class GameplaySystem : CoreSystem {

    private bool _shouldGenerateWorld = false;

    public void LoadGame() {
        Debug.Log("Starting game loading...");

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

    private void CreateWorld() {
        WorldGeneratorData worldGenData = GameCore.Instance.GetScriptableObject<WorldGeneratorData>("DefaultWorldGenData");
        WorldGenerator worldGen = new WorldGenerator(worldGenData);

        worldGen.StartGenerationSequence();
    }
}
