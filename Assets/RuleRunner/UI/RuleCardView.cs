using System;
using RuleRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>
    /// One rule card row: [#] IF [NOT] [condition] THEN [action]  hits  [^][v][X]
    /// The owner (GameScreen) holds the rule list; the card only raises events.
    /// </summary>
    public class RuleCardView : MonoBehaviour
    {
        public event Action<RuleCardView> ToggleNot, PickCondition, PickAction, MoveUp, MoveDown, Remove;

        Image background;
        Text indexText, hitsText;
        Button notButton, condButton, actionButton, upButton, downButton, removeButton;

        public static RuleCardView Create(Transform parent)
        {
            var bg = UIKit.Panel(parent, "RuleCard", UIKit.CardColor, Vector2.zero, Vector2.one);
            UIKit.Layout(bg, preferredHeight: 50);
            var row = bg.rectTransform;
            UIKit.HRow(row, 6, new RectOffset(8, 8, 6, 6));
            var card = bg.gameObject.AddComponent<RuleCardView>();
            card.background = bg;

            card.indexText = UIKit.Label(row, "Index", "1", 20, TextAnchor.MiddleCenter, UIKit.DimText);
            UIKit.Layout(card.indexText, preferredWidth: 26);
            UIKit.Layout(UIKit.Label(row, "If", "IF", 20, TextAnchor.MiddleCenter), preferredWidth: 22);

            card.notButton = UIKit.Button(row, "Not", "NOT", 18, UIKit.ButtonColor, () => card.ToggleNot?.Invoke(card));
            UIKit.Layout(card.notButton, preferredWidth: 58);

            card.condButton = UIKit.Button(row, "Condition", "", 19, UIKit.ButtonColor, () => card.PickCondition?.Invoke(card));
            UIKit.Layout(card.condButton, preferredWidth: 180, flexibleWidth: 1);

            var then = UIKit.Label(row, "Then", "THEN", 20, TextAnchor.MiddleCenter);
            then.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIKit.Layout(then, preferredWidth: 60);

            card.actionButton = UIKit.Button(row, "Action", "", 19, UIKit.ButtonColor, () => card.PickAction?.Invoke(card));
            UIKit.Layout(card.actionButton, preferredWidth: 150, flexibleWidth: 1);

            card.hitsText = UIKit.Label(row, "Hits", "", 18, TextAnchor.MiddleCenter, UIKit.DimText);
            UIKit.Layout(card.hitsText, preferredWidth: 44);

            card.upButton = UIKit.Button(row, "Up", "^", 20, UIKit.ButtonColor, () => card.MoveUp?.Invoke(card));
            UIKit.Layout(card.upButton, preferredWidth: 34);
            card.downButton = UIKit.Button(row, "Down", "v", 20, UIKit.ButtonColor, () => card.MoveDown?.Invoke(card));
            UIKit.Layout(card.downButton, preferredWidth: 34);
            card.removeButton = UIKit.Button(row, "Remove", "X", 20, UIKit.DangerColor, () => card.Remove?.Invoke(card));
            UIKit.Layout(card.removeButton, preferredWidth: 34);
            return card;
        }

        public int Index { get; private set; }

        public void Bind(int index, Rule rule, bool isFirst, bool isLast, bool editable)
        {
            Index = index;
            indexText.text = (index + 1).ToString();

            var notText = UIKit.ButtonText(notButton);
            notText.text = "NOT";
            notText.color = rule.Negate ? UIKit.TextColor : new Color(1, 1, 1, 0.3f);
            ((Image)notButton.targetGraphic).color = rule.Negate ? UIKit.NotOn : UIKit.ButtonColor;

            UIKit.ButtonText(condButton).text = Vocabulary.Display(rule.Condition);
            UIKit.ButtonText(actionButton).text = ActionLabel(rule.Action);

            notButton.interactable = editable;
            condButton.interactable = editable;
            actionButton.interactable = editable;
            upButton.interactable = editable && !isFirst;
            downButton.interactable = editable && !isLast;
            removeButton.interactable = editable;
        }

        public void SetHits(int hits) => hitsText.text = hits > 0 ? "x" + hits : "";

        public void SetActive(bool firing, bool starved = false) =>
            background.color = firing ? UIKit.CardActive : starved ? UIKit.DangerColor : UIKit.CardColor;

        public static string ActionLabel(ActionType a)
        {
            int cost = Vocabulary.Cost(a);
            return Vocabulary.Display(a) + (cost == 0 ? "  (free)" : "  (-" + cost + " qi)");
        }
    }
}
