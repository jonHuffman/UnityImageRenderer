namespace ImageRenderer.Editor
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    public class ImageRendererWindow : EditorWindow
    {
        private const string SAVE_PATH_PREF_KEY = "ImageRendererWindow.SavePath";
        private const string DEFAULT_FILENAME = "New Image";
        private const int WINDOW_HORIZONTAL_BUFFER = 12;
        private const int WINDOW_VERTICAL_BUFFER = 115;
        private const int MAX_PREVIEW_DIMENSION = 500;
        private const float TEXTURE_HORIZONTAL_OFFSET_SPLIT = 0.5f;
        private const float TEXTURE_VERTICAL_OFFSET_SPLIT = 0.7f;

        private Camera renderCamera;
        private int textureWidth = 2048;
        private int textureHeight = 2048;
        private ImageFormat imageFormat = ImageFormat.png;

        private RenderTexture renderTexture;
        private bool showAlpha;

        private string savePath = string.Empty;

        private float ImageAspectRatio
        {
            get => (float)textureWidth / textureHeight;
        }

        #region Unity Methods

        private void OnDestroy()
        {
            DestroyImmediate(renderCamera.gameObject);
        }

        private void OnGUI()
        {
            DrawImageSettings();
            DrawImagePreview();
            DrawSaveButtons();
        }

        private void OnFocus()
        {
            if (renderCamera != null)
            {
                RebuildRenderTarget();
                Selection.activeGameObject = renderCamera.gameObject;
            }
        }

        #endregion

        [MenuItem("Tools/Image Renderer")]
        private static void OpenWindow()
        {
            ImageRendererWindow window = GetWindow<ImageRendererWindow>(true, "Image Renderer", true);
            Vector2 windowSize = new Vector2(MAX_PREVIEW_DIMENSION + WINDOW_HORIZONTAL_BUFFER, MAX_PREVIEW_DIMENSION + WINDOW_VERTICAL_BUFFER);
            window.minSize = windowSize;
            window.maxSize = windowSize;
            window.Initialize();
            window.Show();
        }

        private void Initialize()
        {
            if (renderCamera == null)
            {
                GameObject cameraObject = new GameObject("[TOOL] ImageRendererCamera", typeof(Camera));
                cameraObject.hideFlags = HideFlags.DontSave;
                renderCamera = cameraObject.GetComponent<Camera>();
            }

            Selection.activeGameObject = renderCamera.gameObject;
            savePath = EditorPrefs.GetString(SAVE_PATH_PREF_KEY, String.Empty);

            RebuildRenderTarget();
        }

        private void RebuildRenderTarget()
        {
            if (renderTexture != null)
            {
                renderCamera.targetTexture = null;
                DestroyImmediate(renderTexture);
            }

            renderTexture = new RenderTexture(textureWidth, textureHeight, 32);
            renderCamera.targetTexture = renderTexture;
            renderCamera.aspect = ImageAspectRatio;

            // Clears the active render target (our texture in this case)
            GL.Clear(true, true, Color.clear);
        }

        private void DrawImageSettings()
        {
            const int MAX_TEXTURE_SIZE = 2048;

            GUILayout.Label("Image Settings", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                    {
                        textureWidth = EditorGUILayout.IntField("Width", textureWidth);
                        textureHeight = EditorGUILayout.IntField("Height", textureHeight);

                        if (check.changed)
                        {
                            textureWidth = Mathf.Clamp(textureWidth, 1, MAX_TEXTURE_SIZE);
                            textureHeight = Mathf.Clamp(textureHeight, 1, MAX_TEXTURE_SIZE);

                            RebuildRenderTarget();
                        }
                    }
                }

                imageFormat = (ImageFormat)EditorGUILayout.EnumPopup("Image Format", imageFormat);
            }
        }

        private void DrawSaveButtons()
        {
            const float SAVE_FIELDS_OFFSET = MAX_PREVIEW_DIMENSION - 15;
            const float BUTTON_WIDTH = 100f;

            bool selectPathClicked = false;
            bool saveClicked = false;

            GUILayout.Space(SAVE_FIELDS_OFFSET);
            using (new EditorGUILayout.HorizontalScope())
            {
                savePath = EditorGUILayout.TextField(savePath, GUILayout.ExpandWidth(true));
                selectPathClicked = GUILayout.Button("Select Path", GUILayout.Width(BUTTON_WIDTH));
                saveClicked = GUILayout.Button("Save", GUILayout.Width(BUTTON_WIDTH));
            }

            if (selectPathClicked)
            {
                savePath = EditorUtility.SaveFilePanel("Save Location", Application.dataPath, DEFAULT_FILENAME, imageFormat.ToString());
            }

            if (saveClicked)
            {
                Texture2D convertedTexture = ConvertToTexture2D(renderTexture);
                SaveTexture(convertedTexture);
                DestroyImmediate(convertedTexture);

                EditorPrefs.SetString(SAVE_PATH_PREF_KEY, savePath);
            }
        }

        private void DrawImagePreview()
        {
            GUILayout.Label("Image Preview", EditorStyles.boldLabel);

            int maxPreviewDimension = Mathf.Min(Mathf.Max(textureWidth, textureHeight), MAX_PREVIEW_DIMENSION);
            float previewWidth = ImageAspectRatio >= 1f ? maxPreviewDimension : maxPreviewDimension * ImageAspectRatio;
            float previewHeight = ImageAspectRatio >= 1f ? maxPreviewDimension / ImageAspectRatio : maxPreviewDimension;

            Rect previewTextureRect = new Rect(
                WINDOW_HORIZONTAL_BUFFER * TEXTURE_HORIZONTAL_OFFSET_SPLIT + (MAX_PREVIEW_DIMENSION - previewWidth) / 2,
                WINDOW_VERTICAL_BUFFER * TEXTURE_VERTICAL_OFFSET_SPLIT,
                previewWidth,
                previewHeight);

            EditorGUI.DrawPreviewTexture(previewTextureRect, renderTexture);

            EditorGUILayout.Space();
            DrawAlphaPreviewToggle();

            if (showAlpha)
            {
                DrawAlphaPreview();
            }
        }

        private void DrawAlphaPreviewToggle()
        {
            const float SHOW_LABEL_WIDTH = 70f;
            const float SHOW_TOGGLE_WIDTH = 20f;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
                {
                    normal =
                    {
                        textColor = renderCamera.clearFlags == CameraClearFlags.Depth ? Color.white : Color.black
                    }
                };

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Show Alpha", labelStyle, GUILayout.MaxWidth(SHOW_LABEL_WIDTH));
                showAlpha = EditorGUILayout.Toggle(showAlpha, GUILayout.MaxWidth(SHOW_TOGGLE_WIDTH));
            }
        }

        private void DrawAlphaPreview()
        {
            const int ALPHA_PREVIEW_DIMENSION = 150;
            const uint BORDER_WIDTH = 1;

            float previewWidth = ImageAspectRatio >= 1f ? ALPHA_PREVIEW_DIMENSION : ALPHA_PREVIEW_DIMENSION * ImageAspectRatio;
            float previewHeight = ImageAspectRatio >= 1f ? ALPHA_PREVIEW_DIMENSION / ImageAspectRatio : ALPHA_PREVIEW_DIMENSION;

            Rect previewTextureRect = new Rect(
                WINDOW_HORIZONTAL_BUFFER * TEXTURE_HORIZONTAL_OFFSET_SPLIT + MAX_PREVIEW_DIMENSION * 0.7f,
                WINDOW_VERTICAL_BUFFER * TEXTURE_VERTICAL_OFFSET_SPLIT + MAX_PREVIEW_DIMENSION * 0.7f,
                previewWidth,
                previewHeight);
            previewTextureRect.x += ALPHA_PREVIEW_DIMENSION - previewWidth;
            previewTextureRect.y += ALPHA_PREVIEW_DIMENSION - previewHeight;

            Rect previewTextureBorderRect = CreateBorderRect(previewTextureRect, BORDER_WIDTH);

            EditorGUI.DrawRect(previewTextureBorderRect, Color.magenta);
            EditorGUI.DrawTextureAlpha(previewTextureRect, renderTexture, ScaleMode.ScaleToFit);
        }

        private Rect CreateBorderRect(Rect originalRect, uint borderWidth)
        {
            Debug.Assert(borderWidth > 0, "Border width must be at least 1 pixel");

            Rect borderRect = new Rect(originalRect);
            borderRect.x -= borderWidth;
            borderRect.y -= borderWidth;
            borderRect.width += borderWidth * 2;
            borderRect.height += borderWidth * 2;

            return borderRect;
        }

        private Texture2D ConvertToTexture2D(RenderTexture renderTexture)
        {
            RenderTexture previousRenderTexture = RenderTexture.active;
            RenderTexture.active = renderTexture;

            Texture2D savableTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false);
            savableTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            savableTexture.Apply();

            RenderTexture.active = previousRenderTexture;

            return savableTexture;
        }

        private void SaveTexture(Texture2D texture)
        {
            byte[] encodedImageData;

            switch (imageFormat)
            {
                case ImageFormat.jpg:
                    encodedImageData = texture.EncodeToJPG();
                    break;
                case ImageFormat.png:
                    encodedImageData = texture.EncodeToPNG();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            savePath = string.IsNullOrWhiteSpace(savePath) ? $"{Application.dataPath}/{DEFAULT_FILENAME}.{imageFormat}" : savePath;

            File.WriteAllBytes(savePath, encodedImageData);
        }

        private enum ImageFormat
        {
            jpg,
            png
        }
    }
}