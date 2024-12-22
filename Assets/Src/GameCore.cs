using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameCore : MonoBehaviour {

    public static GameCore Instance { 
        get => _instance;
        set => _instance = value;
    }
    private static GameCore _instance;

    [Header("UI Prefabs")]
    public GameObject LoadingScreenPrefab;
    public GameObject MainMenuPrefab;

    private List<CoreSystem> _systems;

    void Start() {
        if (_instance != null) {
            Destroy(this);
            return;
        }

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

    private void InitializeSystems() {
        _systems = new List<CoreSystem> {
            new UISystem()
        };

        foreach (CoreSystem system in _systems) system.EarlyStart();
        foreach (CoreSystem system in _systems) system.Start();
        foreach (CoreSystem system in _systems) system.LateStart();
    }

    private void OnSceneChanged(Scene currentScene, Scene nextScene) {
        Debug.Log($"Changing scenes from {currentScene.name} to {nextScene.name}!");

        foreach (CoreSystem system in _systems) system.OnSceneChanged(currentScene, nextScene);
    }
}
