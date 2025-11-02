using System.Collections.Generic;
using PendingName.UI;
using UnityEngine.SceneManagement;
using static PendingName.Core.Enums;

namespace PendingName.Core {
    public class UISystem : CoreSystem {
        public ViewEnum CurrentView {
            get => _viewStack.Count == 0 ? ViewEnum.None : _viewStack.Peek();
        }

        public ViewEnum PreviousView {
            get => GetViewFromStackIndex(1);
        }

        private Dictionary<ViewEnum, ViewController> _viewDictionary;
        private Stack<ViewEnum> _viewStack;

        public override void Start() {
            base.Start();

            _viewDictionary = new Dictionary<ViewEnum, ViewController>();
            _viewStack = new Stack<ViewEnum>();

            SwitchView(ViewEnum.LoadingScreen);
        }

        public override void EarlyUpdate() {
            base.EarlyUpdate();

            foreach (ViewEnum viewEnum in _viewStack)
                GetViewController(viewEnum).EarlyUpdate();
        }

        public override void Update() {
            base.Update();

            foreach (ViewEnum viewEnum in _viewStack)
                GetViewController(viewEnum).Update();
        }

        public override void LateUpdate() {
            base.LateUpdate();

            foreach (ViewEnum viewEnum in _viewStack)
                GetViewController(viewEnum).LateUpdate();
        }

        public override void Destroy() {
            base.Destroy();

            ClearViews();
        }

        public override void OnSceneChanged(Scene currentScene, Scene nextScene) {
            base.OnSceneChanged(currentScene, nextScene);

            ClearViews();

            // switch to the default view in these scenes
            switch (nextScene.buildIndex) {
                case 0:
                    SwitchView(ViewEnum.LoadingScreen);
                    break;
                case 1:
                    SwitchView(ViewEnum.MainMenu);
                    break;
            }
        }

        private void ClearViews() {
            foreach (ViewEnum viewEnum in _viewStack)
                GetViewController(viewEnum).Destroy();

            _viewDictionary.Clear();
            _viewStack.Clear();
        }

        private void ClearViewStack() {
            _viewStack.Clear();
        }

        private ViewController LoadView(ViewEnum viewEnum) {
            ViewController controller = GetViewController(viewEnum);

            if (controller == null) {
                switch (viewEnum) {
                    case ViewEnum.LoadingScreen:
                        controller = new LoadingScreenViewController();
                        break;
                    case ViewEnum.MainMenu:
                        controller = new MainMenuViewController();
                        break;
                    default: break;
                }

                if (controller != null)
                    _viewDictionary.Add(viewEnum, controller);
            }

            return controller;
        }

        private void ShowView(ViewEnum viewEnum) {
            ViewController controller = GetViewController(viewEnum);
            if (controller == null) return;

            controller.BaseView.ViewRoot.SetActive(true);
        }

        private void HideView(ViewEnum viewEnum) {
            ViewController controller = GetViewController(viewEnum);
            if (controller == null) return;

            controller.BaseView.ViewRoot.SetActive(false);
        }

        public void SwitchView(ViewEnum nextView, bool hidePreviousViews = true, bool allowBack = true) {
            if (GetViewController(nextView) == null) LoadView(nextView);

            if (hidePreviousViews && _viewStack.Count > 0)
                foreach (ViewEnum openView in _viewStack)
                    HideView(openView);

            if (!allowBack) ClearViewStack();

            ShowView(nextView);
            _viewStack.Push(nextView);
        }

        public void GoBack() {
            if (PreviousView == ViewEnum.None) return;

            // todo: this doesn't account for having multiple views showing at once
            //       example: view 1 and 2 are opened at the same time, view 3 hides them both
            //                backtracking with current logic would not show view 1, only view 2 
            HideView(_viewStack.Pop());
            ShowView(_viewStack.Peek());
        }

        public ViewEnum GetViewFromStackIndex(int index) {
            if (_viewStack.Count > index) {
                int i = 0;

                foreach (ViewEnum viewEnum in _viewStack) {
                    if (i == index) return viewEnum;
                    ++i;
                }
            }

            return ViewEnum.None;
        }

        public ViewController GetViewController(ViewEnum viewEnum) =>
            _viewDictionary.TryGetValue(viewEnum, out ViewController controller) ? controller : null;
    }
}