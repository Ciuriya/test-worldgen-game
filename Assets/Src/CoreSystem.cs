using UnityEngine.SceneManagement;

public abstract class CoreSystem {
    public virtual void EarlyStart() { }
    public virtual void Start() { }
    public virtual void LateStart() { }
    public virtual void EarlyUpdate() { }
    public virtual void Update() { }
    public virtual void LateUpdate() { }
    public virtual void Destroy() { }
    public virtual void OnSceneChanged(Scene currentScene, Scene nextScene) { }
}
