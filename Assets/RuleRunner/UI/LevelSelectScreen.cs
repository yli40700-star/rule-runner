using System;
using System.Linq;
using RuleRunner.Core;
using RuleRunner.Levels;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>Title + one row of level buttons per chapter.</summary>
    public class LevelSelectScreen : MonoBehaviour
    {
        public event Action<LevelDef> LevelChosen;

        RectTransform chapters;
        Func<string, bool> isCleared;

        public static LevelSelectScreen Create(Transform canvas, Func<string, bool> isCleared)
        {
            var root = UIKit.Panel(canvas, "LevelSelect", new Color32(0x1d, 0x23, 0x2e, 0xff), Vector2.zero, Vector2.one);
            var screen = root.gameObject.AddComponent<LevelSelectScreen>();
            screen.isCleared = isCleared;

            var titleRt = UIKit.Rect(root.transform, "Title", new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -150), new Vector2(-40, -40));
            var title = UIKit.Label(titleRt, "Text", "RULE RUNNER", 72, TextAnchor.MiddleCenter, new Color32(0xf5, 0xd0, 0x3b, 0xff));
            title.fontStyle = FontStyle.Bold;

            var tagRt = UIKit.Rect(root.transform, "Tagline", new Vector2(0, 1), new Vector2(1, 1), new Vector2(120, -250), new Vector2(-120, -150));
            UIKit.Label(tagRt, "Text",
                "You never press jump during a run. Program your runner with IF-THEN rule cards.\n" +
                "Every rule fires every time its condition comes true - and every firing spends qi.",
                22, TextAnchor.MiddleCenter, UIKit.DimText);

            screen.chapters = UIKit.Rect(root.transform, "Chapters", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-460, -620), new Vector2(460, -280));
            UIKit.VColumn(screen.chapters, 24);

            var footRt = UIKit.Rect(root.transform, "Footer", new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 12), new Vector2(-20, 44));
            UIKit.Label(footRt, "Text", "CSCI 526 Beta build - grey-box prototype", 16, TextAnchor.MiddleCenter, UIKit.DimText);
            return screen;
        }

        public void Show()
        {
            Refresh();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        void Refresh()
        {
            UIKit.DestroyChildren(chapters);
            foreach (var group in LevelDatabase.All.GroupBy(l => l.Chapter))
            {
                var label = UIKit.Label(chapters, "Chapter " + group.Key, "Chapter " + group.Key, 26, TextAnchor.MiddleLeft, UIKit.DimText);
                UIKit.Layout(label, preferredHeight: 34);

                var row = UIKit.Rect(chapters, "Row " + group.Key, Vector2.zero, Vector2.one);
                UIKit.HRow(row, 14);
                UIKit.Layout(row, preferredHeight: 110);
                foreach (var level in group)
                {
                    var lvl = level;
                    bool cleared = isCleared(level.Id);
                    var color = !level.Playable ? UIKit.PanelLight : cleared ? UIKit.AccentColor : UIKit.ButtonColor;
                    string text = level.Id + "\n" + (level.Playable ? (cleared ? "Cleared" : level.Title) : "Coming soon");
                    var b = UIKit.Button(row, "Level " + level.Id, text, 20, color, () => LevelChosen?.Invoke(lvl));
                    var t = UIKit.ButtonText(b);
                    t.horizontalOverflow = HorizontalWrapMode.Wrap;
                    t.fontSize = 19;
                    b.interactable = level.Playable;
                    UIKit.Layout(b, preferredWidth: 170);
                }
            }
        }
    }
}
