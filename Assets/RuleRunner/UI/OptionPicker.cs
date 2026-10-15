using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>Modal list of choices (used instead of a dropdown for condition / action selection).</summary>
    public class OptionPicker : MonoBehaviour
    {
        RectTransform list;
        Text title;

        public static OptionPicker Create(Transform canvas)
        {
            var root = UIKit.Fill(canvas, "OptionPicker");
            var blocker = root.gameObject.AddComponent<Image>();
            blocker.color = new Color(0, 0, 0, 0.55f);
            var picker = root.gameObject.AddComponent<OptionPicker>();
            var closeBtn = root.gameObject.AddComponent<Button>();
            closeBtn.transition = Selectable.Transition.None;
            closeBtn.onClick.AddListener(picker.Hide);

            var box = UIKit.Panel(root, "Box", UIKit.PanelLight, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            box.rectTransform.sizeDelta = new Vector2(360, 10);
            var fitter = box.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIKit.VColumn(box.rectTransform, 8, new RectOffset(16, 16, 14, 16));

            picker.title = UIKit.Label(box.transform, "Title", "", 22, TextAnchor.MiddleCenter);
            UIKit.Layout(picker.title, preferredHeight: 34);
            picker.list = UIKit.Rect(box.transform, "List", Vector2.zero, Vector2.one);
            UIKit.VColumn(picker.list, 6);

            root.gameObject.SetActive(false);
            return picker;
        }

        public void Show(string heading, IList<string> options, int selected, Action<int> onPick)
        {
            title.text = heading;
            UIKit.DestroyChildren(list);
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var b = UIKit.Button(list, "Option " + i, options[i], 22,
                    i == selected ? UIKit.AccentColor : UIKit.ButtonColor,
                    () => { Hide(); onPick(index); });
                UIKit.Layout(b, preferredHeight: 46);
            }
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
