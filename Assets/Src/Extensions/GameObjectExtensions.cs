using UnityEngine;

public static class GameObjectExtensions {
    public static T FindChild<T>(this GameObject go, string name) where T : Component {
        if (go.transform.name == name) return go.transform.GetComponent<T>();

        for (int i = 0; i < go.transform.childCount; i++) {
            T result = go.transform.GetChild(i).gameObject.FindChild<T>(name);

            if (result != null) return result.GetComponent<T>();
        }

        return null;
    }

}
