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
#if UNITY_ANDROID || UNITY_IOS
        Application.targetFrameRate = FetchHighestRefreshRate();
#elif UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            // either set the fps (by asking) or use vSyncCount
            // by default it should be unlimited
            Application.targetFrameRate = -1;
#endif
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
