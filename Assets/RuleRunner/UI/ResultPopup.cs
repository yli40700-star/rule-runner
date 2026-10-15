using System;
using RuleRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>End-of-run message with a cause-specific hint (design doc section 5.2).</summary>
    public class ResultPopup : MonoBehaviour
    {
        public event Action NextLevel, EditRules, BackToLevels;

        Text title, message, hint;
        Button nextButton, editButton;
        Image box;

        public static ResultPopup Create(Transform canvas)
        {
            var root = UIKit.Fill(canvas, "ResultPopup");
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
            var popup = root.gameObject.AddComponent<ResultPopup>();

            popup.box = UIKit.Panel(root, "Box", UIKit.PanelLight, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            popup.box.rectTransform.sizeDelta = new Vector2(560, 300);

            var titleRt = UIKit.Rect(popup.box.transform, "TitleArea", new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -70), new Vector2(-20, -16));
            popup.title = UIKit.Label(titleRt, "Title", "", 36, TextAnchor.MiddleCenter);
            var msgRt = UIKit.Rect(popup.box.transform, "MessageArea", new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -130), new Vector2(-24, -74));
            popup.message = UIKit.Label(msgRt, "Message", "", 22, TextAnchor.MiddleCenter);
            var hintRt = UIKit.Rect(popup.box.transform, "HintArea", new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -210), new Vector2(-24, -134));
            popup.hint = UIKit.Label(hintRt, "Hint", "", 22, TextAnchor.MiddleCenter, new Color32(0xf5, 0xd0, 0x3b, 0xff));
            popup.hint.fontStyle = FontStyle.Italic;

            var buttons = UIKit.Rect(popup.box.transform, "Buttons", new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 20), new Vector2(-24, 74));
            UIKit.HRow(buttons, 12, null, expandWidth: true);
            var levels = UIKit.Button(buttons, "Levels", "Level list", 22, UIKit.ButtonColor, () => popup.Fire(popup.BackToLevels));
            UIKit.Layout(levels, flexibleWidth: 1);
            popup.editButton = UIKit.Button(buttons, "Edit", "Edit rules", 22, UIKit.ButtonColor, () => popup.Fire(popup.EditRules));
            UIKit.Layout(popup.editButton, flexibleWidth: 1);
            popup.nextButton = UIKit.Button(buttons, "Next", "Next level", 22, UIKit.AccentColor, () => popup.Fire(popup.NextLevel));
            UIKit.Layout(popup.nextButton, flexibleWidth: 1);

            root.gameObject.SetActive(false);
            return popup;
        }

        void Fire(Action a)
        {
            Hide();
            a?.Invoke();
        }

        public void Show(RunResult result, bool hasNextLevel)
        {
            bool won = result.Won;
            title.text = won ? "Level clear!" : "Run failed";
            title.color = won ? new Color32(0x7d, 0xe0, 0x8c, 0xff) : new Color32(0xff, 0x8a, 0x8a, 0xff);
            message.text = won
                ? "Qi left: " + result.QiLeft + "   Steps: " + result.Steps
                : Vocabulary.Describe(result.Outcome) + " (step " + result.Steps + ", qi left " + result.QiLeft + ")";
            hint.text = won ? "" : HintFor(result);
            nextButton.gameObject.SetActive(won && hasNextLevel);
            UIKit.ButtonText(editButton).text = won ? "Try again" : "Edit rules";
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);

        public static string HintFor(RunResult r)
        {
            if (r.RanOutOfQi) return "Did a rule fire more often than needed?";
            switch (r.Outcome)
            {
                case Outcome.Pit: return "Which rule should have fired before the pit?";
                case Outcome.Spike: return "Was the landing spot safe?";
                case Outcome.Trap: return "Was the trap up when you stepped on it?";
                case Outcome.Timeout: return "Is a rule making the runner wait or bump into a wall forever?";
                default: return "";
            }
        }
    }
}
