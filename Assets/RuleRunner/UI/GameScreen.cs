using System;
using System.Collections.Generic;
using RuleRunner.Core;
using RuleRunner.View;
using UnityEngine;
using UnityEngine.UI;

namespace RuleRunner.UI
{
    /// <summary>
    /// Edit-and-run screen (1280x720 reference).
    ///   top:     header (back, level chip, title, hint)
    ///   middle:  the map, drawn by the camera, with a tile legend underneath
    ///   bottom:  the runner's loadout (portrait + qi pool + skill cards) and the run controls / log
    /// Owns the rule list being edited; the App layer drives the run.
    /// </summary>
    public class GameScreen : MonoBehaviour
    {
        public event Action Back, Run, Reset;
        public event Action<float> SpeedChanged;

        const float HeaderPx = 64f;
        const float BottomPx = 300f;
        public const float HeaderFraction = HeaderPx / 720f;
        public const float BottomFraction = BottomPx / 720f;
        const int MaxLogLines = 250;
        const int MaxPips = 24;

        public List<Rule> Rules { get; private set; } = new List<Rule>();
        public LevelDef Level { get; private set; }
        public RectTransform CanvasRect { get; private set; }
        public float Speed => speed;

        Text levelChipText, titleText, hintText, loadoutText, qiText, floatText, defaultText;
        RectTransform cardRow, pipGrid, logContent, floatBubble;
        ScrollRect logScroll;
        Button runButton, resetButton;
        readonly List<Button> speedButtons = new List<Button>();
        OptionPicker picker;
        readonly List<RuleCardView> cards = new List<RuleCardView>();
        readonly List<EmptySlotView> emptySlots = new List<EmptySlotView>();
        readonly List<Image> pipImages = new List<Image>();
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
            screen.BuildLoadout(root);
            screen.BuildControls(root);
            screen.BuildFloatingBubble(root);
            return screen;
        }

        // ---------- Layout ----------

        void BuildHeader(Transform root)
        {
            var header = UIKit.Panel(root, "Header", Theme.Surface, new Vector2(0, 1), Vector2.one, new Vector2(0, -HeaderPx), Vector2.zero);
            UIKit.Panel(header.transform, "Edge", Theme.Line, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));

            var back = UIKit.Button(header.transform, "Back", "<  Levels", 17, Theme.Raised, () => Back?.Invoke(), 10f);
            Place(back.transform, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(112, 40), new Vector2(0, 0.5f));

            var chip = UIKit.Box(header.transform, "LevelChip", Theme.Gold, 12f, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            Place(chip.transform, new Vector2(0, 0.5f), new Vector2(142, 0), new Vector2(64, 40), new Vector2(0, 0.5f));
            levelChipText = UIKit.Label(chip.transform, "Text", "1-1", 20, TextAnchor.MiddleCenter, Theme.Background, FontStyle.Bold);

            titleText = UIKit.Label(UIKit.Rect(header.transform, "Title", new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(222, -2), new Vector2(-20, -6)),
                "Text", "", 24, TextAnchor.LowerLeft, Theme.Text, FontStyle.Bold);
            hintText = UIKit.Label(UIKit.Rect(header.transform, "Hint", new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(222, 2), new Vector2(-20, 0)),
                "Text", "", 15, TextAnchor.UpperLeft, Theme.Dim, FontStyle.Italic);
        }

        void BuildLegend(Transform root)
        {
            var legend = UIKit.Rect(root, "Legend", new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, BottomPx + 2), new Vector2(-16, BottomPx + 28));
            UIKit.HRow(legend, 6, null, false, TextAnchor.MiddleCenter);
            var items = new (string, Color)[]
            {
                ("Ground", Palette.Ground), ("Pit", Palette.PitHole), ("Spike", Palette.Spike),
                ("Low wall (jump over)", Palette.LowWall), ("High wall / edge", Palette.HighWall),
                ("Trap (up 2, down 2)", Palette.TrapUp), ("Qi orb +2", Palette.Orb), ("Finish", Palette.Finish),
            };
            foreach (var (name, color) in items)
            {
                var swatch = UIKit.Rect(legend, "Swatch", Vector2.zero, Vector2.one);
                UIKit.Round(swatch.gameObject.AddComponent<Image>(), 3f).color = color;
                UIKit.Layout(swatch, 12, 12);
                var label = UIKit.Label(legend, "Label", name, 14, TextAnchor.MiddleLeft, Theme.Dim);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                UIKit.Layout(label, label.preferredWidth + 14, 20);
            }
        }

        void BuildLoadout(Transform root)
        {
            var tray = UIKit.Box(root, "Loadout", Theme.Surface, 16f, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(12, 12), new Vector2(-376, BottomPx - 8));
            UIKit.DropShadow(tray, 4f, 0.35f);

            var head = UIKit.TopStrip(tray.transform, "Head", 8f, 26f, 18f, 18f);
            UIKit.Label(head, "Title", "LOADOUT", 15, TextAnchor.MiddleLeft, Theme.Gold, FontStyle.Bold);
            loadoutText = UIKit.Label(UIKit.Rect(head, "Info", Vector2.zero, Vector2.one, new Vector2(84, 0), Vector2.zero),
                "Text", "", 14, TextAnchor.MiddleLeft, Theme.Dim);
            defaultText = UIKit.Label(head, "Default", "No card fires -> Walk (free)", 14, TextAnchor.MiddleRight, Theme.Faint);

            cardRow = UIKit.Rect(tray.transform, "Row", Vector2.zero, Vector2.one, new Vector2(14, 12), new Vector2(-14, -40));
            UIKit.HRow(cardRow, 12, null, false, TextAnchor.MiddleLeft);
            BuildRunnerPanel(cardRow);
        }

        void BuildRunnerPanel(RectTransform row)
        {
            var panel = UIKit.Box(row, "Runner", Theme.Surface2, 14f, Vector2.zero, Vector2.one);
            UIKit.Layout(panel, 138, RuleCardView.CardSize.y);
            var p = panel.transform;

            UIKit.Label(UIKit.TopStrip(p, "Label", 10f, 18f), "Text", "RUNNER", 13, TextAnchor.MiddleCenter, Theme.Dim, FontStyle.Bold);
            // Portrait: the same yellow block as in the map, on a little pedestal.
            var portraitBg = UIKit.Shape(p, "Glow", UISprites.Circle, Palette.Runner.WithAlpha(0.12f), Vector2.zero, new Vector2(84, 84));
            Top(portraitBg.rectTransform, 66f);
            var body = UIKit.Shape(p, "Body", UISprites.Rounded, Palette.Runner, Vector2.zero, new Vector2(46, 46));
            UIKit.Round(body, 8f);
            Top(body.rectTransform, 66f);
            var eye = UIKit.Shape(p, "Eye", UISprites.Rounded, Palette.RunnerEye, Vector2.zero, new Vector2(8, 12));
            Top(eye.rectTransform, 62f, 10f);

            UIKit.Panel(p, "Divider", Theme.Line, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -122), new Vector2(-14, -121));
            UIKit.Label(UIKit.TopStrip(p, "QiLabel", 128f, 16f, 12f, 12f), "Text", "QI POOL", 12, TextAnchor.MiddleCenter, Theme.Dim, FontStyle.Bold);
            qiText = UIKit.Label(UIKit.TopStrip(p, "QiValue", 145f, 24f, 12f, 12f), "Text", "0", 22, TextAnchor.MiddleCenter, Theme.Qi, FontStyle.Bold);

            pipGrid = UIKit.TopStrip(p, "Pips", 176f, 46f, 12f, 12f);
            var grid = pipGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(11, 11);
            grid.spacing = new Vector2(3, 3);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 8;
            grid.childAlignment = TextAnchor.UpperCenter;
        }

        static void Top(RectTransform rt, float centerFromTop, float x = 0f)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(x, -centerFromTop);
        }

        void BuildControls(Transform root)
        {
            var controls = UIKit.Box(root, "Controls", Theme.Surface, 16f, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-364, BottomPx - 8 - 112), new Vector2(-12, BottomPx - 8));
            UIKit.DropShadow(controls, 4f, 0.35f);

            runButton = UIKit.Button(controls.transform, "Run", "RUN", 24, Theme.Accent, () => Run?.Invoke(), 12f);
            var runRt = (RectTransform)runButton.transform;
            runRt.anchorMin = new Vector2(0, 1);
            runRt.anchorMax = new Vector2(1, 1);
            runRt.offsetMin = new Vector2(12, -64);
            runRt.offsetMax = new Vector2(-12, -12);
            UIKit.DropShadow((Image)runButton.targetGraphic, 3f, 0.4f);

            var row = UIKit.Rect(controls.transform, "Row", new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 10), new Vector2(-12, 44));
            UIKit.HRow(row, 6, null, true, TextAnchor.MiddleLeft);
            resetButton = UIKit.Button(row, "Reset", "Reset", 16, Theme.Raised, () => Reset?.Invoke(), 9f);
            UIKit.Layout(resetButton, -1, 34, 1.4f);
            var gap = UIKit.Rect(row, "Gap", Vector2.zero, Vector2.one);
            UIKit.Layout(gap, 8, 34);
            foreach (var s in new[] { 1f, 2f, 4f })
            {
                float value = s;
                var b = UIKit.Button(row, "Speed" + s, s + "x", 15, Theme.Raised, () => SetSpeed(value), 9f);
                UIKit.Layout(b, -1, 34, 1f);
                speedButtons.Add(b);
            }
            SetSpeed(1f);

            var log = UIKit.Box(root, "Log", Theme.Surface, 16f, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-364, 12), new Vector2(-12, BottomPx - 8 - 112 - 10));
            UIKit.DropShadow(log, 4f, 0.35f);
            UIKit.Label(UIKit.TopStrip(log.transform, "Title", 8f, 20f, 14f, 14f), "Text", "RUN LOG", 13, TextAnchor.MiddleLeft, Theme.Dim, FontStyle.Bold);
            var logArea = UIKit.Rect(log.transform, "Area", Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -30));
            logContent = UIKit.ScrollView(logArea, "Scroll", out logScroll);
            UIKit.VColumn(logContent, 3, new RectOffset(6, 6, 2, 4));
        }

        void BuildFloatingBubble(Transform root)
        {
            var bubble = UIKit.Box(root, "FloatingBubble", Theme.Background.WithAlpha(0.92f), 12f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            floatBubble = bubble.rectTransform;
            floatBubble.sizeDelta = new Vector2(170, 32);
            bubble.raycastTarget = false;
            bubble.gameObject.AddComponent<Outline>().effectColor = Theme.Gold.WithAlpha(0.8f);
            floatText = UIKit.Label(floatBubble, "Text", "", 16, TextAnchor.MiddleCenter, Theme.Gold, FontStyle.Bold);
            floatText.horizontalOverflow = HorizontalWrapMode.Overflow;
            floatBubble.gameObject.SetActive(false);
        }

        static void Place(Transform t, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        // ---------- Level setup and rule editing ----------

        public void Setup(LevelDef level, List<Rule> rules)
        {
            Level = level;
            Rules = rules;
            levelChipText.text = level.Id;
            titleText.text = level.Title;
            hintText.text = level.Hint;
            SetEditable(true);
            ClearLog();
            Log("Equip cards, then press RUN.", Theme.Dim);
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
            int slots = Level != null ? Level.Slots : 0;
            while (cards.Count < Rules.Count)
            {
                var card = RuleCardView.Create(cardRow);
                card.ToggleNot += c => Edit(c.Index, r => r.Negate = !r.Negate);
                card.PickCondition += OnPickCondition;
                card.PickAction += OnPickAction;
                card.MoveLeft += c => Move(c.Index, -1);
                card.MoveRight += c => Move(c.Index, +1);
                card.Remove += c => { if (editable) { Rules.RemoveAt(c.Index); RebuildCards(); } };
                cards.Add(card);
            }
            while (emptySlots.Count < slots - Rules.Count)
                emptySlots.Add(EmptySlotView.Create(cardRow, AddRule));

            // Order in the row: runner panel, cards (priority order), empty slots.
            for (int i = 0; i < cards.Count; i++)
            {
                bool show = i < Rules.Count;
                cards[i].gameObject.SetActive(show);
                if (!show) continue;
                cards[i].transform.SetSiblingIndex(1 + i);
                cards[i].Bind(i, Rules[i], i == 0, i == Rules.Count - 1, editable);
                cards[i].SetState(false);
                cards[i].SetHits(0);
            }
            for (int i = 0; i < emptySlots.Count; i++)
            {
                bool show = i < slots - Rules.Count;
                emptySlots[i].gameObject.SetActive(show);
                emptySlots[i].transform.SetAsLastSibling();
                emptySlots[i].SetInteractable(editable);
            }

            loadoutText.text = Rules.Count + " / " + slots + " cards   -   checked left to right, the first true card fires";
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
            var rule = Rules[card.Index];
            picker.ShowConditions("IF ...", "What should card " + (card.Index + 1) + " look for?", Level.Conditions, rule.Negate,
                rule.Condition, c => Edit(card.Index, r => r.Condition = c));
        }

        void OnPickAction(RuleCardView card)
        {
            if (!editable) return;
            picker.ShowActions("THEN ...", "What should card " + (card.Index + 1) + " do?", Level.Actions,
                Rules[card.Index].Action, a => Edit(card.Index, r => r.Action = a));
        }

        void SetSpeed(float value)
        {
            speed = value;
            float[] values = { 1f, 2f, 4f };
            for (int i = 0; i < speedButtons.Count; i++)
                ((Image)speedButtons[i].targetGraphic).color = Mathf.Approximately(values[i], value) ? Theme.Qi.WithAlpha(0.85f) : Theme.Raised;
            SpeedChanged?.Invoke(speed);
        }

        // ---------- Run feedback ----------

        public void SetQi(int qi, int max)
        {
            qiText.text = qi + " / " + max;
            int total = Mathf.Min(Mathf.Max(max, qi), MaxPips);
            while (pipImages.Count < total)
            {
                var pip = UIKit.Rect(pipGrid, "Pip", Vector2.zero, Vector2.one);
                var img = pip.gameObject.AddComponent<Image>();
                img.sprite = UISprites.Circle;
                img.raycastTarget = false;
                pipImages.Add(img);
            }
            for (int i = 0; i < pipImages.Count; i++)
            {
                pipImages[i].gameObject.SetActive(i < total);
                pipImages[i].color = i < qi ? Theme.Qi : Theme.Raised;
            }
        }

        public void HighlightRule(int fired, int starved)
        {
            for (int i = 0; i < Rules.Count && i < cards.Count; i++)
                cards[i].SetState(i == fired, i == starved);
        }

        public void SetHits(int[] hits)
        {
            for (int i = 0; i < Rules.Count && i < cards.Count; i++)
                cards[i].SetHits(hits != null && i < hits.Length ? hits[i] : 0);
        }

        public void Log(string line, Color? color = null)
        {
            var text = UIKit.Label(logContent, "Line", line, 14, TextAnchor.UpperLeft, color ?? Theme.Text);
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

        /// <summary>Shows a short bubble (e.g. "Rule 2: Jump -1") above the runner for a moment.</summary>
        public void ShowFloat(Vector2 canvasPos, string text, float seconds)
        {
            floatBubble.anchoredPosition = canvasPos + new Vector2(0f, 18f);
            floatText.text = text;
            floatBubble.sizeDelta = new Vector2(floatText.preferredWidth + 28f, 32f);
            floatBubble.gameObject.SetActive(true);
            floatTimer = seconds;
        }

        public void HideFloat()
        {
            floatBubble.gameObject.SetActive(false);
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
