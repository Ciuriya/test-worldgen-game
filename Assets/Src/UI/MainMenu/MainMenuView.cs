using static Enums;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuView : View {

    public override ViewEnum ViewEnum => ViewEnum.MainMenu;
    public MainMenuViewController ViewController => BaseViewController as MainMenuViewController;
    protected override GameObject ViewPrefab => GameCore.Instance.GetSystem<PrefabSystem>().GetPrefab("MainMenuUI");

    private Button _playButton;

    public MainMenuView(ViewController viewController) : base(viewController) { }

    public override void LoadElements() {
        base.LoadElements();

        _playButton = ViewRoot.FindChild<Button>("Play Button");
        _playButton.onClick.AddListener(OnPlayButtonClicked);
    }

    public override void Start() {
        base.Start();
    }

    private void OnPlayButtonClicked() {
        GameCore.Instance.GetSystem<GameplaySystem>().LoadGame();
    }
}
