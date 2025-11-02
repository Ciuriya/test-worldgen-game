namespace PendingName.UI {
    public class MainMenuViewController : ViewController {
        public MainMenuView View => BaseView as MainMenuView;

        public MainMenuViewController() : base() {
            BaseView = new MainMenuView(this);
        }
    }
}