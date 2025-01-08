using UnityEngine;
using static Enums;

public abstract class View {

    public abstract ViewEnum ViewEnum { get; }
    public GameObject ViewRoot { get; private set; }
    public ViewController BaseViewController { get; private set; }

    protected abstract GameObject ViewPrefab { get; }

    // todo: add public event actions (OnViewStarted, etc.)

    public View(ViewController viewController) {
        ViewRoot = Object.Instantiate(ViewPrefab);
        BaseViewController = viewController;

        LoadElements();
        Start();
    }

    public virtual void LoadElements() { }
    public virtual void Start() { }
    public virtual void Update() { }
    public virtual void Destroy() { }
}
