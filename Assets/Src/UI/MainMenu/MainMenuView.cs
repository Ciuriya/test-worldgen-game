using static Enums;
using UnityEngine;

public class MainMenuView : View {
    public override ViewEnum ViewEnum => ViewEnum.MainMenu;
    public MainMenuViewController ViewController => BaseViewController as MainMenuViewController;
    protected override GameObject _viewPrefab => GameCore.Instance.MainMenuPrefab;

    public MainMenuView(ViewController viewController) : base(viewController) { }

    public override void LoadElements() {
        base.LoadElements();
    }

    public override void Start() {
        base.Start();
    }
}
