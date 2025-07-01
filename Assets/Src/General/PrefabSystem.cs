using System.Collections.Generic;
using UnityEngine;

public class PrefabSystem : CoreSystem {

    private Dictionary<string, GameObject> _loadedPrefabs;
    private Dictionary<string, ScriptableObject> _loadedScriptableObjects;

    public override void EarlyStart() {
        base.EarlyStart();

        LoadPrefabs();
        LoadScriptableObjects();
    }

    private void LoadPrefabs() {
        _loadedPrefabs = new Dictionary<string, GameObject>();

        foreach (GameObject prefab in GameCore.Instance.Prefabs)
            _loadedPrefabs.Add(prefab.name.ToLowerInvariant(), prefab);
    }

    private void LoadScriptableObjects() {
        _loadedScriptableObjects = new Dictionary<string, ScriptableObject>();

        foreach (ScriptableObject scriptableObject in GameCore.Instance.ScriptableObjects)
            _loadedScriptableObjects.Add(scriptableObject.name.ToLowerInvariant(), scriptableObject);
    }

    public GameObject GetPrefab(string name) {
        if (_loadedPrefabs.ContainsKey(name.ToLowerInvariant()))
            return _loadedPrefabs[name.ToLowerInvariant()];

        return null;
    }

    public T GetScriptableObject<T>(string name) where T : ScriptableObject {
        if (_loadedScriptableObjects.ContainsKey(name.ToLowerInvariant())) {
            ScriptableObject obj = _loadedScriptableObjects[name.ToLowerInvariant()];

            if (obj is T t) return t;
        }

        return null;
    }
}
