using UnityEngine;

public abstract class ViewController {

    public View BaseView { get; protected set; }

    public ViewController() { }

    public virtual void EarlyUpdate() { }
    public virtual void Update() { }
    public virtual void LateUpdate() { }
    public virtual void Destroy() { BaseView.Destroy(); }
}
