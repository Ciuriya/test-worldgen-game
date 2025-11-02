using UnityEngine;

namespace PendingName.Core {
    public class SettingsSystem : CoreSystem {
        public override void EarlyStart() {
            base.EarlyStart();

            InitSettings();
        }

        private void InitSettings() {
            // ideally ask about this, by default I'd say we want -1 for phones
            // for now we just target as high as possible
            float framerate;

#if UNITY_ANDROID || UNITY_IOS
            framerate = FetchHighestRefreshRate();
#elif UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            // either set the fps (by asking) or use vSyncCount
            // by default it should be unlimited
            framerate = -1;
#endif

            // assume -1 is 30 fps for now, we always want to set fixed timestep to a bit under the framerate
            // todo: update this when managing fps in settings (maybe even using average fps metrics)
            Time.fixedDeltaTime = framerate == -1 ? 25f : 1f / (framerate - 5f);
        }

        private int FetchHighestRefreshRate() {
            int highestRefreshRate = int.MinValue;

            foreach (Resolution res in Screen.resolutions)
                if (res.refreshRateRatio.value > highestRefreshRate)
                    highestRefreshRate = Mathf.RoundToInt((float)res.refreshRateRatio.value);

            return highestRefreshRate < 30 ? -1 : highestRefreshRate;
        }
    }
}
