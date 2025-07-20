#if UNITY_EDITOR // in case

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public partial class TilemapPainterEditor {

    private int drawImageSize = 512;
    private Texture2D drawTexture = null;
    private Dictionary<Vector2Int, int> drawnCells;
    private Vector2Int lastCellDrawnTo;
    private const string saveFolder = "Assets/Art/IndexMaps";
    private VisualElement drawPanel;
    private event Action<int> OnDrawTextureResize;

    private VisualElement CreateDrawPanel() {
        drawPanel = new VisualElement {
            name = "Draw Panel"
        };

        drawPanel.AddToClassList("draw-panel");

        drawnCells = new Dictionary<Vector2Int, int>();

        var drawImageViewport = CreateDrawImageViewport();
        var drawImage = CreateDrawImage();
        var drawImageContentArea = CreateDrawImageContentArea(drawImageViewport, drawImage);
        var drawTextureSizeField = CreateDrawTextureSizeField();
        var drawTextureNameField = CreateDrawTextureNameField();
        var drawTextureLoadField = CreateDrawTextureLoadField(drawTextureNameField, drawImage);
        var drawTextureSaveButton = CreateDrawTextureSaveButton(drawTextureNameField);

        drawImageContentArea.Add(drawImage);
        drawImageViewport.Add(drawImageContentArea);

        drawPanel.Add(drawImageViewport);
        atlasPanel.Add(drawTextureSizeField);
        atlasPanel.Add(drawTextureNameField);
        atlasPanel.Add(drawTextureLoadField);
        atlasPanel.Add(drawTextureSaveButton);

        OnDrawTextureResize += (newDrawImageSize) => ResizeDrawTexture(newDrawImageSize, drawPanel, drawImageViewport, 
                                                                       drawImageContentArea, drawTextureSizeField);
        OnDrawTextureResize?.Invoke(drawImageSize);

        return drawPanel;
    }

    private VisualElement CreateDrawImageViewport() {
        var drawImageViewport = new VisualElement {
            name = "Draw Image Viewport"
        };

        drawImageViewport.AddToClassList("draw-image-viewport");

        return drawImageViewport;
    }

    private VisualElement CreateDrawImageContentArea(VisualElement viewport, Image drawImage) {
        var drawImageContentArea = new VisualElement {
            name = "Draw Image Content Area"
        };

        var imageHandler = new ImageControlHandler(drawImageContentArea);
        imageHandler.OnClick += (pos) => OnDrawClick(false, pos, drawImage);
        imageHandler.OnDragClick += (pos) => OnDrawClick(true, pos, drawImage);

        UpdateContentAreaSize(viewport, drawImageContentArea, drawTexture);

        return drawImageContentArea;
    }

    private Image CreateDrawImage() {
        if (!drawTexture)
            drawTexture = CreateDrawTexture();

        var drawTextureImage = new Image {
            name = "Draw Texture Image",
            image = drawTexture
        };

        return drawTextureImage;
    }

    private Button CreateDrawImageClearButton() {
        var drawImageClearButton = new Button {
            name = "Draw Image Clear Button",
            text = "Clear"
        };

        drawImageClearButton.RegisterCallback<ClickEvent>((evt) => {
            ClearTexture(drawTexture);
            drawnCells.Clear();
        });

        drawImageClearButton.AddToClassList("draw-image-clear-button");

        return drawImageClearButton;
    }

    private IntegerField CreateDrawTextureSizeField() {
        var drawTextureSizeField = new IntegerField {
            name = "Draw Texture Size Field",
            label = "Image Size",
            value = drawImageSize
        };

        drawTextureSizeField.RegisterValueChangedCallback((evt) => {
            if (evt.newValue > 0)
                OnDrawTextureResize?.Invoke(evt.newValue);
        });

        return drawTextureSizeField;
    }

    private TextField CreateDrawTextureNameField() {
        var drawTextureNameField = new TextField {
            name = "Draw Texture Name Field",
            label = "Image Name"
        };

        return drawTextureNameField;
    }

    private ObjectField CreateDrawTextureLoadField(TextField nameField, Image drawImage) {
        var drawTextureLoadField = new ObjectField {
            name = "Draw Texture Load Field",
            label = "Load Image"
        };
        
        drawTextureLoadField.RegisterValueChangedCallback((evt) => {
            Texture2D indexMap = evt.newValue is Texture2D text ? text : null;

            if (indexMap == null) {
                Debug.LogError("Invalid index map.");
                return;
            }

            int width = indexMap.width;
            int height = indexMap.height;

            if (width != height) {
                Debug.LogError("Width and Height are not equal, you may only load index maps that are square.");
                return;
            }

            Texture2D output = DeconstructIndexMapTexture(indexMap, out var drawnCells);

            if (output == null) return;

            this.drawnCells = drawnCells;

            if (output.width != drawImageSize || output.height != drawImageSize)
                drawImageSize = output.width;

            drawTexture = output;
            drawImage.image = drawTexture;

            nameField.SetValueWithoutNotify(evt.newValue.name);

            OnDrawTextureResize?.Invoke(drawImageSize);

            Debug.Log($"Loaded Index Map: {indexMap.name}");
        });

        return drawTextureLoadField;
    }

    private Button CreateDrawTextureSaveButton(TextField nameField) {
        var drawTextureSaveButton = new Button {
            name = "Draw Texture Save Button",
            text = "Save Image"
        };

        drawTextureSaveButton.RegisterCallback<ClickEvent>((evt) => {
            SaveDrawTextureAsFile(nameField.text);
        });

        return drawTextureSaveButton;
    }

    private void ResizeDrawTexture(int newDrawImageSize, VisualElement panel, VisualElement viewport, VisualElement contentArea, 
                                   IntegerField drawTextureSizeField, bool generateTexture = false) {
        if (generateTexture && drawImageSize != newDrawImageSize)
            drawTexture = CreateDrawTexture();

        drawImageSize = newDrawImageSize;
        drawTextureSizeField.SetValueWithoutNotify(newDrawImageSize);

        panel.style.width = new Length(drawImageSize + 10, LengthUnit.Pixel);
        viewport.style.width = new Length(drawImageSize, LengthUnit.Pixel);
        viewport.style.height = new Length(drawImageSize, LengthUnit.Pixel);

        UpdateContentAreaSize(viewport, contentArea, drawTexture);
        Repaint();
    }

    private void OnDrawClick(bool isDragClick, Vector2 localPos, Image drawImage) {
        Vector2 imageActualSize = drawImage.layout.size;
        Vector2 scale = new Vector2(drawTexture.width / imageActualSize.x,
                                    drawTexture.height / imageActualSize.y);
        Vector2Int cell = new Vector2Int(Mathf.FloorToInt(localPos.x * scale.x / atlasGridSize),
                                         Mathf.FloorToInt(localPos.y * scale.y / atlasGridSize));

        if (isDragClick && lastCellDrawnTo.Equals(cell)) return;

        switch (currentTool) {
            case Tools.Brush:
                OnBrushClick(cell);
                break;
            case Tools.Erase:
                OnEraseClick(cell);
                break;
            default: break;
        }
    }

    private void OnBrushClick(Vector2Int cell) {
        if (hasSelectedAtlasCell) {
            Color32[] pixels = GetAtlasCellContents(selectedAtlasCell);

            if (pixels.Length == 0) return;

            DrawPixelsOntoDrawTexture(cell, pixels);

            lastCellDrawnTo = cell;

            if (drawnCells.ContainsKey(cell))
                drawnCells.Remove(cell);

            drawnCells.Add(cell, selectedAtlasCell.y * 
                                    Mathf.CeilToInt(atlasTexture.width / (float) atlasGridSize) + 
                                    selectedAtlasCell.x);
        }
    }

    private void OnEraseClick(Vector2Int cell) {
        Color32[] emptyPixels = new Color32[atlasGridSize * atlasGridSize];
        Color32 blankColor = new Color32(176, 176, 176, 255);

        for (int i = 0; i < emptyPixels.Length; ++i)
            emptyPixels[i] = blankColor;

        DrawPixelsOntoDrawTexture(cell, emptyPixels);

        lastCellDrawnTo = cell;

        if (drawnCells.ContainsKey(cell))
            drawnCells.Remove(cell);
    }

    private void DrawPixelsOntoDrawTexture(Vector2Int cell, Color32[] atlasPixels) {
        int width = drawTexture.width;
        int height = drawTexture.height;
        Vector2Int topLeft = GetCellTopLeft(width, height, cell);

        int drawX = topLeft.x;
        int drawY = Mathf.Clamp(topLeft.y - (atlasGridSize - 1), 0, height);
        int sizeX = atlasGridSize;
        int sizeY = atlasGridSize;

        if (width - drawX < sizeX) sizeX = width - drawX;
        if (height - drawY < sizeY) sizeY = height - drawY;

        drawTexture.SetPixels32(drawX, // left
                                drawY, // bottom
                                sizeX, 
                                sizeY, 
                                atlasPixels);
        drawTexture.Apply(false, false);
        Repaint();
    }

    private void SaveDrawTextureAsFile(string fileName) {
        if (string.IsNullOrEmpty(fileName))
            fileName = "IndexMap";

        Texture2D indexMapTexture = BuildIndexMapTexture();
        SaveIndexMapTextureAsFile(fileName, indexMapTexture);

        Debug.Log($"Saved Index Map to {saveFolder}/{fileName}.png");
    }

    private Texture2D BuildIndexMapTexture() {
        int cellsX = drawTexture.width / atlasGridSize;
        int cellsY = drawTexture.height / atlasGridSize;

        Texture2D indexMapTexture = new Texture2D(cellsX, cellsY, TextureFormat.RGBA32, 
                                                  false, true) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };

        Color32[] pixels = new Color32[cellsX * cellsY];

        for (int y = 0; y < cellsY; ++y)
            for (int x = 0; x < cellsX; ++x) {
				bool isDrawn = drawnCells.TryGetValue(new Vector2Int(x, y), out int atlasId);

				byte r = (byte)(atlasId & 0xFF); // low byte
                byte g = (byte)((atlasId >> 8) & 0xFF); // high byte

                // flip Y so UV 0,0 = bottom-left like the shader
                int pixel = (cellsY - 1 - y) * cellsX + x;
                pixels[pixel] = new Color32(r, g, 0, (byte) (isDrawn ? 255 : 0));
            }

        indexMapTexture.SetPixels32(pixels);
        indexMapTexture.Apply();

        return indexMapTexture;
    }

    private Texture2D DeconstructIndexMapTexture(Texture2D indexMap, out Dictionary<Vector2Int, int> drawnCells) {
        drawnCells = new Dictionary<Vector2Int, int>();

        if (!atlasTexture) {
            Debug.LogError("No atlas loaded, cannot deconstruct index map.");
            return null;
        }

        int width = indexMap.width;
        int height = indexMap.height;
        int outputWidth = width * atlasGridSize;
        int outputHeight = height * atlasGridSize;

        Texture2D output = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            alphaIsTransparency = true
        };

        Color32[] outputPixels = new Color32[outputWidth * outputHeight];
        Color emptyColor = new Color32(176, 176, 176, 255);

        for (int i = 0; i < outputPixels.Length; ++i)
            outputPixels[i] = emptyColor;

        int atlasCellsX = Mathf.CeilToInt(atlasTexture.width / (float) atlasGridSize);
        Color32[] indexPixels = indexMap.GetPixels32();

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) {
                int pixel = (height - 1 - y) * width + x;
                Color32 pack = indexPixels[pixel];

                int atlasId = (pack.g << 8) | pack.r;
                Vector2Int atlasCell = new Vector2Int(atlasId % atlasCellsX, atlasId / atlasCellsX);
                Color32[] cellPixels = GetAtlasCellContents(atlasCell);

                if (cellPixels.Length == 0) continue;

                int destCellY = height - 1 - y;

                bool isDrawn = false;
                for (int iy = 0; iy < atlasGridSize; iy++)
                    for (int ix = 0; ix < atlasGridSize; ix++) {
                        int destX = x * atlasGridSize + ix;
                        int destY = destCellY * atlasGridSize + iy;
                        int destPixel = destY * outputWidth + destX;
                        int sourcePixel = iy * atlasGridSize + ix;

                        Color32 source = cellPixels[sourcePixel];
                        
                        if (source.a == 255) {
                            outputPixels[destPixel] = cellPixels[sourcePixel];
                            isDrawn = true;
                        }
                    }

                if (isDrawn) {
                    Vector2Int outputCell = new Vector2Int(x, y);
                    drawnCells.Add(outputCell, atlasId);
                }
            }

        output.SetPixels32(outputPixels);
        output.Apply(false, false);

        return output;
    }

    private void SaveIndexMapTextureAsFile(string fileName, Texture2D indexMapTexture) {
        string path = $"{saveFolder}/{fileName}.png";

        File.WriteAllBytes(path, indexMapTexture.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter) AssetImporter.GetAtPath(path);
        if (importer) {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; 
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }

    private Texture2D CreateDrawTexture() {
        Texture2D drawTexture = new Texture2D(drawImageSize, drawImageSize, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            alphaIsTransparency = true
        };

        ClearTexture(drawTexture);

        return drawTexture;
    }

    private void ClearTexture(Texture2D texture) {
        // could potentially be cached
        Color32[] colors = new Color32[texture.width * texture.height];
        Color emptyColor = new Color32(176, 176, 176, 255);

        for (int i = 0; i < colors.Length; ++i)
            colors[i] = emptyColor;

        texture.SetPixels32(colors);
        texture.Apply(false, false);
    }
}

#endif