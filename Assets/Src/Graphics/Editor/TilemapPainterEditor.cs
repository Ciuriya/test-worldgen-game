#if UNITY_EDITOR // just in case

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public partial class TilemapPainterEditor : EditorWindow {

    public static string STYLESHEET_PATH = "Assets/Art/USS/Editor/TilemapPainterEditorStyleSheet.uss";

    [MenuItem("Custom/Tilemap Painter")]
    public new static void Show() {
        EditorWindow window = GetWindow<TilemapPainterEditor>();
        window.titleContent = new GUIContent("Tilemap Painter");
    }

    public void CreateGUI() {
        var mainPanel = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Horizontal) {
            name = "Main Panel"
        };

        DisablePaneDragging(mainPanel);

        var stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(STYLESHEET_PATH);
        
        if (stylesheet)
            rootVisualElement.styleSheets.Add(stylesheet);
        else Debug.LogError("Stylesheet could not be found.");

        mainPanel.AddToClassList("main-panel");
        rootVisualElement.Add(mainPanel);

        var atlasPanel = CreateAtlasPanel();
        mainPanel.Add(atlasPanel);

        var drawPanel = CreateDrawPanel();
        mainPanel.Add(drawPanel);
    }

    private void DisablePaneDragging(VisualElement mainPanel) {
        var dragLineAnchor = mainPanel.Q<VisualElement>("unity-dragline-anchor");
        if (dragLineAnchor != null) dragLineAnchor.pickingMode = PickingMode.Ignore;

        var dragLine = mainPanel.Q<VisualElement>("unity-dragline");
        if (dragLine != null) dragLine.pickingMode = PickingMode.Ignore;
    }
}

#endif