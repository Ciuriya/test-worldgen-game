using TMPro;
using UnityEngine;
using static Enums;

public class LoadingScreenView : View {

    public override ViewEnum ViewEnum => ViewEnum.LoadingScreen;
    public LoadingScreenViewController ViewController => BaseViewController as LoadingScreenViewController;
    protected override GameObject ViewPrefab => GameCore.Instance.GetSystem<PrefabSystem>().GetPrefab("LoadingScreenUI");

    private TMP_Text _loadingText;

    public LoadingScreenView(ViewController viewController) : base(viewController) { }

    public override void LoadElements() {
        base.LoadElements();

        _loadingText = ViewRoot.FindChild<TMP_Text>("LoadingText");
    }

    public override void Start() {
        base.Start();
    }

    public void UpdateLoadingText(string text) {
        _loadingText.SetText(text);
    }
}
