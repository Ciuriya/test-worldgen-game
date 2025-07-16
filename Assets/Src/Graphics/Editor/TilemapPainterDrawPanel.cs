#if UNITY_EDITOR // in case

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public partial class TilemapPainterEditor {

    // todo: add eraser
    private int drawImageSize = 512;
    private Texture2D drawTexture = null;
    private Dictionary<Vector2Int, int> drawnCells;
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

        OnDrawTextureResize += (newDrawImageSize) => ResizeDrawTexture(newDrawImageSize, drawPanel, drawImageViewport, drawImageContentArea);
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
        imageHandler.OnClick += (pos) => OnDrawClick(pos, drawImage);

        UpdateContentAreaSize(viewport, drawImageContentArea, drawTexture);

        return drawImageContentArea;
    }

    private Image CreateDrawImage() {
        if (!drawTexture) {
            drawTexture = new Texture2D(drawImageSize, drawImageSize, TextureFormat.RGBA32, false) {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        } 

        var drawTextureImage = new Image {
            name = "Draw Texture Image",
            image = drawTexture
        };

        return drawTextureImage;
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
            drawTexture = evt.newValue is Texture2D text ? text : new Texture2D(drawImageSize, drawImageSize);
            drawImage.image = drawTexture;

            nameField.SetValueWithoutNotify(evt.newValue.name);

            OnDrawTextureResize?.Invoke(drawTexture.width > drawTexture.height ? drawTexture.width : drawTexture.height);
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
                                   bool generateTexture = false) {
        if (generateTexture && drawImageSize != newDrawImageSize) {
            drawTexture = new Texture2D(drawImageSize, drawImageSize, TextureFormat.RGBA32, false) {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        drawImageSize = newDrawImageSize;

        panel.style.width = new Length(drawImageSize + 10, LengthUnit.Pixel);
        viewport.style.width = new Length(drawImageSize, LengthUnit.Pixel);
        viewport.style.height = new Length(drawImageSize, LengthUnit.Pixel);

        UpdateContentAreaSize(viewport, contentArea, atlasTexture);
    }

    private void OnDrawClick(Vector2 localPos, Image drawImage) {
        if (hasSelectedAtlasCell) {
            Color32[] pixels = GetAtlasCellContents(selectedAtlasCell);

            if (pixels.Length > 0) {
                Vector2 imageActualSize = drawImage.layout.size;
                Vector2 scale = new Vector2(drawTexture.width / imageActualSize.x,
                                            drawTexture.height / imageActualSize.y);
                Vector2Int cell = new Vector2Int(Mathf.FloorToInt(localPos.x * scale.x / atlasGridSize),
                                                 Mathf.FloorToInt(localPos.y * scale.y / atlasGridSize));

                DrawAtlasCellOntoDrawTexture(cell, pixels);

                if (drawnCells.ContainsKey(cell))
                    drawnCells.Remove(cell);

                drawnCells.Add(cell, selectedAtlasCell.y * 
                                     Mathf.CeilToInt(atlasImageSize / (float) atlasGridSize) + 
                                     selectedAtlasCell.x);
            }
        }
    }

    private void DrawAtlasCellOntoDrawTexture(Vector2Int cell, Color32[] atlasPixels) {
        int width = drawTexture.width;
        int height = drawTexture.height;
        Vector2Int topLeft = GetCellTopLeft(width, height, cell);
        Color32[] pixels = drawTexture.GetPixels32();

        for (int x = 0; x < atlasGridSize; x++)
            for (int y = 0; y < atlasGridSize; y++) {
                int destX = topLeft.x + x;
                int destY = topLeft.y - y;
                int destPixel = destY * width + destX;
                int sourcePixel = (atlasGridSize - 1 - y) * atlasGridSize + x;

                if (destPixel >= 0 && destPixel < pixels.Length && 
                    sourcePixel >= 0 && sourcePixel < atlasPixels.Length)
                    pixels[destPixel] = atlasPixels[sourcePixel];
            }

        drawTexture.SetPixels32(pixels);
        drawTexture.Apply();
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
				drawnCells.TryGetValue(new Vector2Int(x, y), out int atlasId);

				byte r = (byte)(atlasId & 0xFF); // low byte
                byte g = (byte)((atlasId >> 8) & 0xFF); // high byte

                // flip Y so UV 0,0 = bottom-left like the shader
                int pixel = (cellsY - 1 - y) * cellsX + x;
                pixels[pixel] = new Color32(r, g, 0, 255);
            }

        indexMapTexture.SetPixels32(pixels);
        indexMapTexture.Apply();

        return indexMapTexture;
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
            importer.SaveAndReimport();
        }
    }
}

#endif