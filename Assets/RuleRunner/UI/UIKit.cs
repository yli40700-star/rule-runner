using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>
    /// Tiny factory for building uGUI from code (no prefabs, no art).
    /// Only ASCII text is used: WebGL has no system font fallback.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color PanelColor = new Color32(0x26, 0x2d, 0x3a, 0xf2);
        public static readonly Color PanelLight = new Color32(0x33, 0x3c, 0x4d, 0xff);
        public static readonly Color CardColor = new Color32(0x3a, 0x44, 0x57, 0xff);
        public static readonly Color CardActive = new Color32(0x7a, 0x6a, 0x1f, 0xff);
        public static readonly Color ButtonColor = new Color32(0x4a, 0x56, 0x6d, 0xff);
        public static readonly Color AccentColor = new Color32(0x3f, 0x9b, 0x5a, 0xff);
        public static readonly Color DangerColor = new Color32(0xa8, 0x42, 0x42, 0xff);
        public static readonly Color TextColor = new Color32(0xee, 0xf1, 0xf5, 0xff);
        public static readonly Color DimText = new Color32(0x9a, 0xa4, 0xb5, 0xff);
        public static readonly Color NotOn = new Color32(0xd9, 0x6c, 0x2e, 0xff);

        static Font font;
        public static Font Font => font ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>A rect that fills its parent with an inset in pixels.</summary>
        public static RectTransform Fill(Transform parent, string name, float inset = 0f) =>
            Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));

        public static Image Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var rt = Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size,
            TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var rt = Fill(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextColor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        public static Button Button(Transform parent, string name, string text, int size, Color color, UnityAction onClick)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(onClick);
            var label = Label(rt, "Text", text, size, TextAnchor.MiddleCenter);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return b;
        }

        public static Text ButtonText(Button b) => b.GetComponentInChildren<Text>();

        /// <summary>Adds a LayoutElement with a preferred / flexible size.</summary>
        public static LayoutElement Layout(Component c, float preferredWidth = -1, float preferredHeight = -1,
            float flexibleWidth = -1, float flexibleHeight = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.preferredHeight = preferredHeight;
            le.flexibleWidth = flexibleWidth;
            le.flexibleHeight = flexibleHeight;
            if (preferredWidth >= 0) le.minWidth = preferredWidth;
            if (preferredHeight >= 0) le.minHeight = preferredHeight;
            // A fixed size means "don't stretch" unless flexibility was asked for explicitly
            // (nested layout groups otherwise report flexible size 1 and fill the parent).
            if (preferredWidth >= 0 && flexibleWidth < 0) le.flexibleWidth = 0;
            if (preferredHeight >= 0 && flexibleHeight < 0) le.flexibleHeight = 0;
            return le;
        }

        public static HorizontalLayoutGroup HRow(RectTransform rt, float spacing, RectOffset padding = null,
            bool expandWidth = false)
        {
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset(0, 0, 0, 0);
            g.childAlignment = TextAnchor.MiddleLeft;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = expandWidth;
            g.childForceExpandHeight = true;
            return g;
        }

        public static VerticalLayoutGroup VColumn(RectTransform rt, float spacing, RectOffset padding = null)
        {
            var g = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset(0, 0, 0, 0);
            g.childAlignment = TextAnchor.UpperLeft;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = false;
            return g;
        }

        /// <summary>A vertical scroll view; returns the content rect (grows with its children).</summary>
        public static RectTransform ScrollView(Transform parent, string name, out ScrollRect scroll)
        {
            var view = Fill(parent, name);
            var bg = view.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.18f);
            view.gameObject.AddComponent<RectMask2D>();
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            var content = Rect(view, "Content", new Vector2(0, 1), new Vector2(1, 1));
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = view;
            return content;
        }

        public static void DestroyChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        public static void SafeInvoke(Action a)
        {
            try { a?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
