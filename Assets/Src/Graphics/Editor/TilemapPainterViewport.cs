#if UNITY_EDITOR

using UnityEngine;
using UnityEngine.UIElements;

internal class TilemapPainterViewport : VisualElement {

    private readonly string _name;
    private int _imageSize;
    private Texture2D _texture;
    private VisualElement _contentArea;
    private ImageControlHandler _imageControlHandler;
    private Image _image;

    internal TilemapPainterViewport(string name, int defaultImageSize) : base() {
        _name = name;
        _imageSize = defaultImageSize;

        Setup();
    }

    internal VisualElement GetContentArea() => _contentArea;
    internal ImageControlHandler GetControlHandler() => _imageControlHandler;
    internal Image GetImage() => _image;
    internal int GetImageSize() => _imageSize;
    internal Texture2D GetTexture() => _texture;

    private void Setup() {
        name = _name + " Viewport";

        CreateContentArea();
        CreateImage();
        SetupImageControlHandler();

        _contentArea.Add(_image);
        Add(_contentArea);
    }

    private void CreateContentArea() {
        _contentArea = new VisualElement {
            name = _name + " Content Area"
        };
    }

    private void CreateImage() {
        ResetTexture();
        _image = new Image {
            name = _name + " Image",
            image = _texture
        };
    }

    private void SetupImageControlHandler() {
        _imageControlHandler = new ImageControlHandler(_contentArea);

        UpdateContentAreaSize();
    }

    private void UpdateContentAreaSize() {
        // the viewport is supposed to be square, but due to tiny rounding errors
        // we cannot have pixel-perfect matching width/height
        // nonetheless, we still assume a square viewport
        float defaultWidth = contentRect.width;
        float defaultHeight = contentRect.height;

        if (_texture.width > _texture.height)
            defaultHeight *= _texture.height / (float) _texture.width;
        else if (_texture.width < _texture.height)
            defaultWidth *= _texture.width / (float) _texture.height;

        _contentArea.style.width = defaultWidth;
        _contentArea.style.height = defaultHeight;
        _contentArea.style.scale = Vector3.one;
        _contentArea.style.translate = Vector3.zero;
    }

    internal void Resize(int newImageSize) {
        _imageSize = newImageSize;
        style.width = new Length(_imageSize, LengthUnit.Pixel);
        style.height = new Length(_imageSize, LengthUnit.Pixel);

        UpdateContentAreaSize();
    }

    internal void OnTextureChange(ChangeEvent<Object> evt) => 
        OnTextureChange(evt.newValue is Texture2D text ? text : new Texture2D(_imageSize, _imageSize));

    internal void OnTextureChange(Texture2D texture, int newSize = 0) {
        _texture = texture;
        _image.image = _texture;

        if (newSize > 0) Resize(newSize);
        else UpdateContentAreaSize();
    }

    internal void ResetTexture() {
        _texture = new Texture2D(_imageSize, _imageSize, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            alphaIsTransparency = true
        };

        ClearTexture();
    }

    internal void ClearTexture() {
        // could potentially be cached
        Color32[] colors = new Color32[_texture.width * _texture.height];
        Color emptyColor = new Color32(176, 176, 176, 255);

        for (int i = 0; i < colors.Length; ++i)
            colors[i] = emptyColor;

        _texture.SetPixels32(colors);
        _texture.Apply(false, false);
    }
}
#endif