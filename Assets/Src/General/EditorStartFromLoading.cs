using UnityEngine;
using UnityEngine.SceneManagement;

public class EditorStartFromLoading {

#if UNITY_EDITOR
    public static int OtherScene = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitLoadingScene() {
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;

        if (sceneIndex == 0) return; // started from loading screen already

        Debug.Log("Going to loading screen...");

        SceneManager.LoadScene(0);
    }
#endif
}
