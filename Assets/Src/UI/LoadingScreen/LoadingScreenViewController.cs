using UnityEngine;
using UnityEngine.SceneManagement;

namespace PendingName.UI {
    public class LoadingScreenViewController : ViewController {
        public LoadingScreenView View => BaseView as LoadingScreenView;

        private float _lastDotIncreaseTime;
        private int _currentDotCount;

        public LoadingScreenViewController() : base() {
            BaseView = new LoadingScreenView(this);

            _lastDotIncreaseTime = Time.time;

            LoadMainMenuScene();
        }

        private void LoadMainMenuScene() {
            int sceneToLoad = 1;

#if UNITY_EDITOR
            if (EditorStartFromLoading.OtherScene > 0)
                sceneToLoad = EditorStartFromLoading.OtherScene;
#endif

            SceneManager.LoadScene(sceneToLoad);
        }

        public override void Update() {
            base.Update();

            UpdateLoadingText();
        }

        private void UpdateLoadingText() {
            if (Time.time - _lastDotIncreaseTime >= 1.0f) {
                _lastDotIncreaseTime = Time.time;
                _currentDotCount++;

                if (_currentDotCount == 4) _currentDotCount = 0;

                string loadingText = "Loading";
                for (int i = 0; i < _currentDotCount; i++)
                    loadingText += ".";

                View.UpdateLoadingText(loadingText);
            }
        }
    }
}