using UnityEngine;
using static Enums;

public abstract class View {
    public abstract ViewEnum ViewEnum { get; }
    public GameObject ViewRoot { get; private set; }
    public ViewController BaseViewController { get; private set; }

    protected abstract GameObject _viewPrefab { get; }

    // add public event actions

    public View(ViewController viewController) {
        ViewRoot = Object.Instantiate(_viewPrefab);
        BaseViewController = viewController;

        LoadElements();
        Start();
    }

    public virtual void LoadElements() { }
    public virtual void Start() { }
    public virtual void Update() { }
    public virtual void Destroy() { }
}
