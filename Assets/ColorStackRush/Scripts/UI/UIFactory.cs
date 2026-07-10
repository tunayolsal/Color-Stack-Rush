using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ColorStackRush
{
    /// <summary>
    /// Builds every UI element from code: procedural rounded-rect and circle
    /// sprites, texts, buttons and sliders. Keeps all panel scripts compact
    /// and guarantees zero asset dependencies.
    /// </summary>
    public static class UIFactory
    {
        static Font font;
        static Sprite roundedSprite;
        static Sprite circleSprite;

        public static Font DefaultFont
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        /// <summary>9-sliced rounded rectangle used by cards and buttons.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (roundedSprite == null) roundedSprite = MakeRoundedSprite(64, 20);
                return roundedSprite;
            }
        }

        /// <summary>Filled circle used for icons and slider handles.</summary>
        public static Sprite Circle
        {
            get
            {
                if (circleSprite == null) circleSprite = MakeCircleSprite(64);
                return circleSprite;
            }
        }

        // ------------------------------------------------------------------
        //  Element builders
        // ------------------------------------------------------------------

        /// <summary>Empty RectTransform anchored at a point (default: center).</summary>
        public static RectTransform CreateRect(Transform parent, string name, Vector2? anchor = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Vector2 a = anchor ?? new Vector2(0.5f, 0.5f);
            rt.anchorMin = rt.anchorMax = rt.pivot = a;
            return rt;
        }

        /// <summary>Full-stretch colored panel (backgrounds, dim overlays).</summary>
        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            var rt = CreateRect(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true; // overlays block clicks to panels beneath
            return img;
        }

        /// <summary>Positioned image. Rounded by default; pass circle:true for round icons.</summary>
        public static Image CreateImage(Transform parent, string name, Color color,
            Vector2 pos, Vector2 size, Vector2? anchor = null, bool circle = false)
        {
            var rt = CreateRect(parent, name, anchor);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = circle ? Circle : Rounded;
            img.type = circle ? Image.Type.Simple : Image.Type.Sliced;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Text label. Overflows its box so exact sizing is never fiddly.</summary>
        public static Text CreateText(Transform parent, string name, string content, int fontSize,
            Color color, Vector2 pos, Vector2 size, Vector2? anchor = null,
            TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var rt = CreateRect(parent, name, anchor);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = content;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Rounded button with label, click sound and scale-punch feedback baked in.</summary>
        public static Button CreateButton(Transform parent, string name, string label,
            Vector2 pos, Vector2 size, Color color, UnityAction onClick,
            int fontSize = 44, Color? textColor = null, Vector2? anchor = null)
        {
            var img = CreateImage(parent, name, color, pos, size, anchor);
            img.raycastTarget = true;

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            CreateText(img.transform, "Label", label, fontSize, textColor ?? Color.white, Vector2.zero, size);

            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySfx(SfxId.Button);
                Juice.PunchScale(img.transform, 0.12f, 0.18f);
            });
            btn.onClick.AddListener(onClick);
            return btn;
        }

        /// <summary>Horizontal 0-1 slider with pastel styling.</summary>
        public static Slider CreateSlider(Transform parent, string name,
            Vector2 pos, Vector2 size, float initial, UnityAction<float> onChanged)
        {
            var root = CreateRect(parent, name);
            root.anchoredPosition = pos;
            root.sizeDelta = size;

            // Track background.
            var bg = CreateImage(root, "Background", new Color(0f, 0f, 0f, 0.12f), Vector2.zero, Vector2.zero);
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.sizeDelta = new Vector2(0f, 16f);

            // Fill area + fill.
            var fillArea = CreateRect(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-44f, 16f);
            fillArea.anchoredPosition = Vector2.zero;

            var fill = CreateImage(fillArea, "Fill", ColorPalette.UiAccent, Vector2.zero, Vector2.zero);
            var fillRt = fill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.sizeDelta = new Vector2(22f, 0f);

            // Handle area + round handle.
            var handleArea = CreateRect(root, "Handle Slide Area");
            handleArea.anchorMin = new Vector2(0f, 0.5f);
            handleArea.anchorMax = new Vector2(1f, 0.5f);
            handleArea.sizeDelta = new Vector2(-44f, 0f);
            handleArea.anchoredPosition = Vector2.zero;

            var handle = CreateImage(handleArea, "Handle", Color.white, Vector2.zero, new Vector2(46f, 46f), circle: true);
            handle.raycastTarget = true;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initial;
            slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        // ------------------------------------------------------------------
        //  Procedural sprites
        // ------------------------------------------------------------------

        static Sprite MakeRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance from the nearest corner circle center; inside body = opaque.
                    float cx = Mathf.Clamp(x, radius, size - 1 - radius);
                    float cy = Mathf.Clamp(y, radius, size - 1 - radius);
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    float alpha = Mathf.Clamp01(radius - dist + 1f); // 1px anti-aliased edge
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            // 9-slice borders so buttons of any size keep crisp corners.
            var border = new Vector4(radius + 4, radius + 4, radius + 4, radius + 4);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, border);
        }

        static Sprite MakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half));
                    float alpha = Mathf.Clamp01(half - dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
