using PendingName.Log;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EditorStartFromLoading {

#if UNITY_EDITOR
    public static int OtherScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitLoadingScene() {
        OtherScene = 1;
        
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;

        if (sceneIndex == 0) return; // started from loading screen already

        CustomLogger.Instance.Log(LogLevel.Info, "Going to loading screen...");

        SceneManager.LoadScene(0);
    }
#endif
}
