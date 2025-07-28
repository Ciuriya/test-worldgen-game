using UnityEngine;
using UnityEngine.SceneManagement;

public class GameplaySystem : CoreSystem {

    private bool _shouldGenerateWorld = false;
    private WorldGenerator _worldGenerator;

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

	public override void EarlyUpdate() {
		base.EarlyUpdate();

        _worldGenerator?.EarlyUpdate();
	}

    private void CreateWorld() {
        WorldGeneratorData worldGenData = GameCore.Instance.GetScriptableObject<WorldGeneratorData>("DefaultWorldGenData");
        _worldGenerator = new WorldGenerator(worldGenData);
        _worldGenerator.StartGenerationSequence();
    }
}
