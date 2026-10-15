using System;
using System.Collections.Generic;
using RuleRunner.Core;
using RuleRunner.View;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>
    /// Edit-and-run screen. Owns the rule list being edited; the App layer drives the run.
    /// Layout (1280x720 reference): header on top, map in the middle (drawn by the camera),
    /// rule cards bottom-left, controls and run log bottom-right.
    /// </summary>
    public class GameScreen : MonoBehaviour
    {
        public event Action Back, Run, Reset;
        public event Action<float> SpeedChanged;

        public const float HeaderFraction = 0.13f;
        public const float BottomFraction = 0.45f;
        const int MaxLogLines = 250;

        public List<Rule> Rules { get; private set; } = new List<Rule>();
        public LevelDef Level { get; private set; }
        public RectTransform CanvasRect { get; private set; }

        Text titleText, hintText, qiText, slotText, availableText, floatText;
        RectTransform qiFill, cardList, logContent;
        ScrollRect logScroll;
        Button addButton, runButton, resetButton, speedButton;
        OptionPicker picker;
        readonly List<RuleCardView> cards = new List<RuleCardView>();
        readonly Queue<GameObject> logLines = new Queue<GameObject>();
        bool editable = true;
        float speed = 1f;
        float floatTimer;

        public static GameScreen Create(RectTransform canvas, OptionPicker picker)
        {
            var root = UIKit.Fill(canvas, "GameScreen");
            var screen = root.gameObject.AddComponent<GameScreen>();
            screen.CanvasRect = canvas;
            screen.picker = picker;
            screen.BuildHeader(root);
            screen.BuildLegend(root);
            screen.BuildRulesPanel(root);
            screen.BuildRunPanel(root);

            var floatRt = UIKit.Rect(root, "FloatingText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            floatRt.sizeDelta = new Vector2(320, 40);
            screen.floatText = UIKit.Label(floatRt, "Text", "", 24, TextAnchor.MiddleCenter, new Color32(0xf5, 0xd0, 0x3b, 0xff));
            screen.floatText.fontStyle = FontStyle.Bold;
            screen.floatText.horizontalOverflow = HorizontalWrapMode.Overflow;
            screen.floatText.gameObject.AddComponent<Outline>().effectColor = Color.black;
            return screen;
        }

        void BuildHeader(Transform root)
        {
            var header = UIKit.Panel(root, "Header", UIKit.PanelColor, new Vector2(0, 1 - HeaderFraction), Vector2.one);
            var back = UIKit.Button(header.transform, "Back", "< Levels", 20, UIKit.ButtonColor, () => Back?.Invoke());
            SetRect(back.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, -24), new Vector2(134, 24));

            var titleRt = UIKit.Rect(header.transform, "Title", new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(150, 0), new Vector2(-330, -4));
            titleText = UIKit.Label(titleRt, "Text", "", 28);
            titleText.fontStyle = FontStyle.Bold;
            var hintRt = UIKit.Rect(header.transform, "Hint", new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(150, 4), new Vector2(-330, 0));
            hintText = UIKit.Label(hintRt, "Text", "", 18, TextAnchor.UpperLeft, UIKit.DimText);

            var qiBg = UIKit.Panel(header.transform, "QiBar", new Color(0, 0, 0, 0.35f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-316, -20), new Vector2(-16, 20));
            var fill = UIKit.Panel(qiBg.transform, "Fill", Palette.Orb, Vector2.zero, Vector2.one);
            qiFill = fill.rectTransform;
            qiText = UIKit.Label(qiBg.transform, "Text", "Qi", 22, TextAnchor.MiddleCenter);
            qiText.fontStyle = FontStyle.Bold;
            qiText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        }

        void BuildLegend(Transform root)
        {
            float y = BottomFraction;
            var legend = UIKit.Rect(root, "Legend", new Vector2(0, y), new Vector2(1, y), new Vector2(16, 4), new Vector2(-16, 30));
            UIKit.HRow(legend, 6);
            legend.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var items = new (string, Color)[]
            {
                ("Ground", Palette.Ground), ("Pit", Palette.PitHole), ("Spike", Palette.Spike),
                ("Low wall (jump over)", Palette.LowWall), ("High wall / edge", Palette.HighWall),
                ("Trap (up 2, down 2)", Palette.TrapUp), ("Qi orb +2", Palette.Orb), ("Finish", Palette.Finish),
            };
            foreach (var (name, color) in items)
            {
                var swatch = UIKit.Panel(legend, "Swatch", color, Vector2.zero, Vector2.one);
                UIKit.Layout(swatch, preferredWidth: 14, preferredHeight: 14);
                var label = UIKit.Label(legend, "Label", name, 15, TextAnchor.MiddleLeft, UIKit.DimText);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                UIKit.Layout(label, preferredWidth: label.preferredWidth + 12);
            }
        }

        void BuildRulesPanel(Transform root)
        {
            var panel = UIKit.Panel(root, "RulesPanel", UIKit.PanelColor, new Vector2(0, 0), new Vector2(0.62f, BottomFraction),
                new Vector2(10, 10), new Vector2(-5, -4));

            var top = UIKit.Rect(panel.transform, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -50), new Vector2(-14, -8));
            slotText = UIKit.Label(top, "Slots", "", 20);
            addButton = UIKit.Button(top, "Add", "+ Add rule", 20, UIKit.AccentColor, AddRule);
            SetRect(addButton.transform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-150, 0), Vector2.zero);

            cardList = UIKit.Rect(panel.transform, "Cards", new Vector2(0, 0), new Vector2(1, 1), new Vector2(14, 44), new Vector2(-14, -58));
            UIKit.VColumn(cardList, 6);

            var availRt = UIKit.Rect(panel.transform, "Available", new Vector2(0, 0), new Vector2(1, 0), new Vector2(14, 6), new Vector2(-14, 40));
            availableText = UIKit.Label(availRt, "Text", "", 16, TextAnchor.MiddleLeft, UIKit.DimText);
        }

        void BuildRunPanel(Transform root)
        {
            var panel = UIKit.Panel(root, "RunPanel", UIKit.PanelColor, new Vector2(0.62f, 0), new Vector2(1, BottomFraction),
                new Vector2(5, 10), new Vector2(-10, -4));

            var controls = UIKit.Rect(panel.transform, "Controls", new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -58), new Vector2(-12, -8));
            UIKit.HRow(controls, 8, null, expandWidth: true);
            runButton = UIKit.Button(controls, "Run", "RUN", 24, UIKit.AccentColor, () => Run?.Invoke());
            UIKit.Layout(runButton, flexibleWidth: 2);
            UIKit.ButtonText(runButton).fontStyle = FontStyle.Bold;
            resetButton = UIKit.Button(controls, "Reset", "Reset", 22, UIKit.ButtonColor, () => Reset?.Invoke());
            UIKit.Layout(resetButton, flexibleWidth: 1);
            speedButton = UIKit.Button(controls, "Speed", "1x", 22, UIKit.ButtonColor, CycleSpeed);
            UIKit.Layout(speedButton, flexibleWidth: 1);

            var logArea = UIKit.Rect(panel.transform, "Log", Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -66));
            logContent = UIKit.ScrollView(logArea, "Scroll", out logScroll);
            UIKit.VColumn(logContent, 2, new RectOffset(8, 8, 6, 6));
        }

        static void SetRect(Transform t, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
        }

        // ---------- Level setup and rule editing ----------

        public void Setup(LevelDef level, List<Rule> rules)
        {
            Level = level;
            Rules = rules;
            titleText.text = "Level " + level.Id + "   " + level.Title;
            hintText.text = level.Hint;

            var conds = new List<string>();
            foreach (var c in level.Conditions) conds.Add(Vocabulary.Display(c));
            var acts = new List<string>();
            foreach (var a in level.Actions) acts.Add(RuleCardView.ActionLabel(a));
            availableText.text = "IF: " + string.Join(", ", conds) + "     THEN: " + string.Join(", ", acts) +
                                 "     No rule true -> Walk (free)";

            SetEditable(true);
            ClearLog();
            Log("Level " + level.Id + ": write rules, then press RUN.", UIKit.DimText);
            HideFloat();
        }

        public void SetEditable(bool canEdit)
        {
            editable = canEdit;
            runButton.interactable = canEdit;
            RebuildCards();
        }

        void RebuildCards()
        {
            while (cards.Count < Rules.Count)
            {
                var card = RuleCardView.Create(cardList);
                card.ToggleNot += c => Edit(c.Index, r => r.Negate = !r.Negate);
                card.PickCondition += OnPickCondition;
                card.PickAction += OnPickAction;
                card.MoveUp += c => Move(c.Index, -1);
                card.MoveDown += c => Move(c.Index, +1);
                card.Remove += c => { if (editable) { Rules.RemoveAt(c.Index); RebuildCards(); } };
                cards.Add(card);
            }
            for (int i = 0; i < cards.Count; i++)
            {
                bool show = i < Rules.Count;
                cards[i].gameObject.SetActive(show);
                if (show)
                {
                    cards[i].Bind(i, Rules[i], i == 0, i == Rules.Count - 1, editable);
                    cards[i].SetActive(false);
                    cards[i].SetHits(0);
                }
            }
            int slots = Level != null ? Level.Slots : 0;
            slotText.text = "Rules  " + Rules.Count + " / " + slots + "   (checked top to bottom, first true rule fires)";
            addButton.interactable = editable && Rules.Count < slots;
        }

        void AddRule()
        {
            if (!editable || Level == null || Rules.Count >= Level.Slots) return;
            Rules.Add(new Rule(Level.Conditions[0], false, Level.Actions[0]));
            RebuildCards();
        }

        void Edit(int index, Action<Rule> change)
        {
            if (!editable) return;
            change(Rules[index]);
            RebuildCards();
        }

        void Move(int index, int delta)
        {
            int j = index + delta;
            if (!editable || j < 0 || j >= Rules.Count) return;
            (Rules[index], Rules[j]) = (Rules[j], Rules[index]);
            RebuildCards();
        }

        void OnPickCondition(RuleCardView card)
        {
            if (!editable) return;
            var options = Level.Conditions.ConvertAll(Vocabulary.Display);
            picker.Show("IF ...", options, Level.Conditions.IndexOf(Rules[card.Index].Condition),
                i => Edit(card.Index, r => r.Condition = Level.Conditions[i]));
        }

        void OnPickAction(RuleCardView card)
        {
            if (!editable) return;
            var options = Level.Actions.ConvertAll(RuleCardView.ActionLabel);
            picker.Show("THEN ...", options, Level.Actions.IndexOf(Rules[card.Index].Action),
                i => Edit(card.Index, r => r.Action = Level.Actions[i]));
        }

        void CycleSpeed()
        {
            speed = speed >= 4f ? 1f : speed * 2f;
            UIKit.ButtonText(speedButton).text = speed + "x";
            SpeedChanged?.Invoke(speed);
        }

        public float Speed => speed;

        // ---------- Run feedback ----------

        public void SetQi(int qi, int max)
        {
            int cap = Mathf.Max(max, qi, 1);
            qiFill.anchorMax = new Vector2(Mathf.Clamp01((float)qi / cap), 1f);
            qiText.text = "Qi  " + qi + " / " + max;
        }

        public void HighlightRule(int fired, int starved)
        {
            for (int i = 0; i < Rules.Count && i < cards.Count; i++)
                cards[i].SetActive(i == fired, i == starved);
        }

        public void SetHits(int[] hits)
        {
            for (int i = 0; i < Rules.Count && i < cards.Count; i++)
                cards[i].SetHits(hits != null && i < hits.Length ? hits[i] : 0);
        }

        public void Log(string line, Color? color = null)
        {
            var text = UIKit.Label(logContent, "Line", line, 17, TextAnchor.UpperLeft, color ?? UIKit.TextColor);
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            logLines.Enqueue(text.gameObject);
            while (logLines.Count > MaxLogLines) Destroy(logLines.Dequeue());
            Canvas.ForceUpdateCanvases();
            logScroll.verticalNormalizedPosition = 0f;
        }

        public void ClearLog()
        {
            while (logLines.Count > 0) Destroy(logLines.Dequeue());
        }

        /// <summary>Shows a short label (e.g. "Rule 2: Jump -1") at a canvas position for about a second.</summary>
        public void ShowFloat(Vector2 canvasPos, string text, float seconds)
        {
            var rt = (RectTransform)floatText.transform.parent;
            rt.anchoredPosition = canvasPos;
            floatText.text = text;
            floatText.enabled = true;
            floatTimer = seconds;
        }

        public void HideFloat()
        {
            floatText.enabled = false;
            floatTimer = 0f;
        }

        void Update()
        {
            if (floatTimer > 0f)
            {
                floatTimer -= Time.deltaTime;
                if (floatTimer <= 0f) HideFloat();
            }
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
