#if UNITY_EDITOR // in case

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PendingName.TilemapEditor {
    internal partial class TilemapPainterEditor {
        private Texture2D atlasGridTexture = null;
        private int atlasGridSize = 8;
        private bool hasSelectedAtlasCell = false;
        private Vector2Int selectedAtlasCell;
        private VisualElement atlasPanel;
        private TilemapPainterViewport atlasViewport;

        private Tools currentTool;
        private List<Button> toolButtons;

        private VisualElement CreateAtlasPanel() {
            atlasPanel = new VisualElement {
                name = "Atlas Panel"
            };

            SetupAtlasViewport();

            var atlasImageGrid = CreateAtlasImageGrid();
            var atlasObjectField = CreateSpriteAtlasField(atlasImageGrid);
            var atlasGridSizeField = CreateGridSizeField(atlasImageGrid);
            var toolAreaElement = CreateToolAreaElement();
            var toolBrushButton = CreateToolBrushButton();
            var toolEraseButton = CreateToolEraseButton();
            var drawImageClearButton = CreateDrawImageClearButton();

            toolAreaElement.Add(toolBrushButton);
            toolAreaElement.Add(toolEraseButton);

            atlasViewport.GetContentArea().Add(atlasImageGrid);

            atlasPanel.Add(atlasViewport);
            atlasPanel.Add(toolAreaElement);
            atlasPanel.Add(drawImageClearButton);
            atlasPanel.Add(CreateSpacer());
            atlasPanel.Add(atlasObjectField);
            atlasPanel.Add(atlasGridSizeField);

            atlasPanel.AddToClassList("atlas-panel");

            ResizeAtlasZone(atlasViewport.GetImageSize());

            foreach (var child in atlasPanel.Children())
                child.AddToClassList("atlas-panel-element");

            return atlasPanel;
        }

        private void SetupAtlasViewport() {
            atlasViewport = new TilemapPainterViewport("Atlas Panel", 190);
            atlasViewport.GetControlHandler().OnClick += (pos) => OnAtlasGridSelect(pos, atlasViewport.GetImage());
            atlasViewport.AddToClassList("atlas-image-viewport");
        }

        private Image CreateAtlasImageGrid() {
            var atlasImageGrid = new Image {
                name = "Atlas Image Grid",
                image = atlasGridTexture
            };

            atlasImageGrid.style.position = Position.Absolute;

            UpdateAtlasGridImage(atlasImageGrid, atlasGridSize, true);

            return atlasImageGrid;
        }

        private ObjectField CreateSpriteAtlasField(Image atlasImageGrid) {
            var atlasField = new ObjectField {
                name = "Sprite Atlas Object Field",
                label = "Sprite Atlas",
                allowSceneObjects = false
            };

            atlasField.RegisterValueChangedCallback((evt) => {
                atlasViewport.OnTextureChange(evt);
                UpdateAtlasGridImage(atlasImageGrid, atlasGridSize, true);
            });

            return atlasField;
        }

        private IntegerField CreateGridSizeField(Image atlasImageGrid) {
            var atlasGridSizeField = new IntegerField {
                name = "Sprite Atlas Grid Size Field",
                label = "Grid Size",
                value = 8
            };

            atlasGridSizeField.RegisterValueChangedCallback((evt) => {
                if (evt.newValue < 1) atlasGridSizeField.value = 1;
                else {
                    atlasGridSize = evt.newValue;
                    UpdateAtlasGridImage(atlasImageGrid, atlasGridSize);
                }
            });

            return atlasGridSizeField;
        }

        private VisualElement CreateToolAreaElement() {
            toolButtons = new List<Button>();

            var toolAreaElement = new VisualElement {
                name = "Tool Area"
            };

            toolAreaElement.AddToClassList("tool-area");

            return toolAreaElement;
        }

        private Button CreateToolBrushButton() {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(TOOL_ICON_PATH + "brush.png");

            if (!icon) icon = new Texture2D(16, 16);

            var toolBrushButton = new Button {
                name = "Tool Brush Button",
                iconImage = icon
            };

            toolBrushButton.RegisterCallback<ClickEvent>((evt) => SelectTool(Tools.Brush, toolBrushButton));

            toolButtons.Add(toolBrushButton);

            SelectTool(Tools.Brush, toolBrushButton); // default selection

            return toolBrushButton;
        }

        private Button CreateToolEraseButton() {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(TOOL_ICON_PATH + "eraser.png");

            if (!icon) icon = new Texture2D(16, 16);

            var toolEraseButton = new Button {
                name = "Tool Erase Button",
                iconImage = icon
            };

            toolEraseButton.RegisterCallback<ClickEvent>((evt) => SelectTool(Tools.Erase, toolEraseButton));

            toolButtons.Add(toolEraseButton);

            return toolEraseButton;
        }

        private void SelectTool(Tools tool, Button toolButton) {
            UnselectAllTools();
            toolButton.AddToClassList("selected-tool");
            toolButton.SetEnabled(false);

            currentTool = tool;
        }

        private void UnselectAllTools() {
            foreach (Button tool in toolButtons) {
                tool.RemoveFromClassList("selected-tool");
                tool.SetEnabled(true);
            }
        }

        private void ResizeAtlasZone(int newImageSize) {
            atlasPanel.style.width = new Length(newImageSize + 10, LengthUnit.Pixel);
            atlasViewport.Resize(newImageSize);
            Repaint();
        }

        private void UpdateAtlasGridImage(Image atlasImageGrid, int gridSize, bool createTexture = false) {
            if (createTexture) {
                atlasGridTexture = new Texture2D(atlasViewport.GetTexture().width, atlasViewport.GetTexture().height,
                                                 TextureFormat.RGBA32, false) {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };

                atlasImageGrid.image = atlasGridTexture;
            }

            int width = atlasGridTexture.width;
            int height = atlasGridTexture.height;
            Color32[] pixels = new Color32[width * height];
            Color32 lineColor = new Color32(0, 0, 0, 100);

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++) {
                    bool onHorizontal = (y % gridSize) == 0;
                    bool onVertical = (x % gridSize) == 0;

                    pixels[y * width + x] = (onHorizontal || onVertical) ? lineColor : Color.clear;
                }

            atlasGridTexture.SetPixels32(pixels);
            atlasGridTexture.Apply(false, false);
            Repaint();
        }

        private void OnAtlasGridSelect(Vector2 localPos, Image atlasImage) {
            Vector2 imageActualSize = atlasImage.layout.size;
            Vector2 scale = new Vector2(atlasViewport.GetTexture().width / imageActualSize.x,
                                        atlasViewport.GetTexture().height / imageActualSize.y);
            Vector2Int cell = new Vector2Int(Mathf.FloorToInt(localPos.x * scale.x / atlasGridSize),
                                             Mathf.FloorToInt(localPos.y * scale.y / atlasGridSize));

            if (hasSelectedAtlasCell) {
                ToggleAtlasGridCell(selectedAtlasCell, false);

                if (selectedAtlasCell.Equals(cell)) {
                    hasSelectedAtlasCell = false;
                    selectedAtlasCell = Vector2Int.zero;
                    return;
                }
            }

            ToggleAtlasGridCell(cell, true);
            hasSelectedAtlasCell = true;
            selectedAtlasCell = cell;
        }

        private void ToggleAtlasGridCell(Vector2Int cell, bool select) {
            int width = atlasGridTexture.width;
            int height = atlasGridTexture.height;
            Vector2Int topLeft = GetCellTopLeft(width, height, cell, true);
            Color32[] pixels = atlasGridTexture.GetPixels32();

            // invalid selection, deselect
            if (pixels.Length == 0) {
                if (select) {
                    hasSelectedAtlasCell = false;
                    selectedAtlasCell = Vector2Int.zero;
                }

                return;
            }

            Color32 selectedColor = new Color32(0, 255, 0, 100);
            Color32 lineColor = new Color32(0, 0, 0, 100);

            for (int x = topLeft.x; x <= Mathf.Clamp(topLeft.x + atlasGridSize, 0, width); x++)
                for (int y = topLeft.y; y >= Mathf.Clamp(topLeft.y - atlasGridSize, 0, height); y--) {
                    bool onHorizontal = (y % atlasGridSize) == 0;
                    bool onVertical = (x % atlasGridSize) == 0;

                    if (onHorizontal || onVertical) {
                        int pixel = y * width + x;

                        if (pixel < pixels.Length)
                            pixels[pixel] = select ? selectedColor : lineColor;
                    }
                }

            atlasGridTexture.SetPixels32(pixels);
            atlasGridTexture.Apply(false, false);
            Repaint();
        }

        private Vector2Int GetCellTopLeft(int width, int height, Vector2Int cell, bool useGridTopLeft = false) {
            int x = Mathf.Clamp(cell.x * atlasGridSize, 0, width);
            if (x % atlasGridSize != 0) x -= x % atlasGridSize;
            x += useGridTopLeft ? 0 : 1;

            int y = Mathf.Clamp(height - cell.y * atlasGridSize, 0, height);
            if (y % atlasGridSize != 0) y += atlasGridSize - (y % atlasGridSize);
            y -= useGridTopLeft ? 0 : 1;

            return new Vector2Int(x, y);
        }

        private Color32[] GetAtlasCellContents(Vector2Int cell) {
            int width = atlasViewport.GetTexture().width;
            int height = atlasViewport.GetTexture().height;
            Vector2Int topLeft = GetCellTopLeft(width, height, cell);
            topLeft.x -= 1;
            Color32[] pixels = atlasViewport.GetTexture().GetPixels32();
            Color32[] cellContents = new Color32[atlasGridSize * atlasGridSize];

            if (pixels.Length == 0) return cellContents;

            for (int x = 0; x < atlasGridSize; x++)
                for (int y = 0; y < atlasGridSize; y++) {
                    int sourceX = topLeft.x + x;
                    int sourceY = topLeft.y - y;
                    int sourcePixel = sourceY * width + sourceX;
                    int destPixel = (atlasGridSize - 1 - y) * atlasGridSize + x;

                    if (sourcePixel >= 0 && sourcePixel < pixels.Length &&
                        destPixel >= 0 && destPixel < cellContents.Length)
                        cellContents[destPixel] = pixels[sourcePixel];
                }

            return cellContents;
        }
    }
}

#endif