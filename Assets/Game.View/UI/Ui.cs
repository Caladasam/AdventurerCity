using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdventurerCity.View
{
    public sealed class Ui
    {
        readonly UiWindow window;
        readonly Stack<VisualElement> stack = new Stack<VisualElement>();
        internal readonly StringBuilder Signature = new StringBuilder();
        internal readonly List<Action> Actions = new List<Action>();
        internal readonly List<(string Key, ScrollView View)> Scrolls = new List<(string, ScrollView)>();

        internal Ui(UiWindow window, VisualElement root)
        {
            this.window = window;
            stack.Push(root);
        }

        VisualElement Current => stack.Peek();

        static void Size(VisualElement element, float width, float height)
        {
            if (width > 0f)
            {
                element.style.width = width;
                element.style.flexShrink = 0;
            }
            if (height > 0f) element.style.height = height;
        }

        public Label Text(string text, float width = 0f, string cls = null)
        {
            var label = new Label(text) { enableRichText = true, pickingMode = PickingMode.Ignore };
            label.AddToClassList("text");
            if (cls != null) label.AddToClassList(cls);
            Size(label, width, 0f);
            Current.Add(label);
            Signature.Append('L').Append(text).Append('\u0001');
            return label;
        }

        public Label Small(string text, float width = 0f) => Text(text, width, "small");

        public Label Title(string text) => Text(text, 0f, "title");

        public Button Button(string text, Action onClick, bool enabled = true, string tip = null, float width = 0f, float height = 0f,
            bool on = false, string cls = null)
        {
            var index = Actions.Count;
            Actions.Add(onClick);
            var button = new Button(() => window.Invoke(index)) { text = text, enableRichText = true };
            button.AddToClassList("btn");
            if (cls != null)
                foreach (var name in cls.Split(' '))
                    button.AddToClassList(name);
            if (on) button.AddToClassList("on");
            button.SetEnabled(enabled);
            Size(button, width, height);
            if (tip != null) window.Owner.AttachTip(button, tip);
            Current.Add(button);
            Signature.Append('B').Append(text).Append(enabled ? '1' : '0').Append(on ? '1' : '0').Append(cls).Append(tip).Append('\u0001');
            return button;
        }

        public void Check(bool value, Action onToggle, bool enabled = true) =>
            Button(value ? "✔" : "", onToggle, enabled, null, 18f, 18f, value, "check");

        public void Row(Action content, string cls = null)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("row");
            if (cls != null) row.AddToClassList(cls);
            Nest(row, content, 'R');
        }

        public void Column(Action content, float width = 0f)
        {
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.AddToClassList("col");
            Size(column, width, 0f);
            Nest(column, content, 'C');
        }

        void Nest(VisualElement element, Action content, char token)
        {
            Current.Add(element);
            stack.Push(element);
            Signature.Append(token).Append('(');
            content();
            Signature.Append(')');
            stack.Pop();
        }

        public void Flex()
        {
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.AddToClassList("flex");
            Current.Add(spacer);
            Signature.Append('F');
        }

        public void Space(float pixels)
        {
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.height = pixels;
            spacer.style.width = pixels;
            spacer.style.flexShrink = 0;
            Current.Add(spacer);
            Signature.Append('S');
        }

        public void Bar(float ratio, Color color, float width, float height = 9f)
        {
            ratio = Mathf.Clamp01(ratio);
            var background = new VisualElement { pickingMode = PickingMode.Ignore };
            background.AddToClassList("bar-bg");
            background.style.width = width;
            background.style.height = height;
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("bar-fill");
            fill.style.width = Length.Percent(ratio * 100f);
            fill.style.backgroundColor = color;
            background.Add(fill);
            Current.Add(background);
            Signature.Append('b').Append(Mathf.RoundToInt(ratio * 100f)).Append(ColorUtility.ToHtmlStringRGB(color));
        }

        public void Swatch(Color color)
        {
            var swatch = new VisualElement { pickingMode = PickingMode.Ignore };
            swatch.AddToClassList("swatch");
            swatch.style.backgroundColor = color;
            Current.Add(swatch);
        }

        public void Scroll(string key, float maxHeight, Action content)
        {
            var view = new ScrollView(ScrollViewMode.Vertical);
            view.AddToClassList("scroll");
            view.style.maxHeight = maxHeight;
            view.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            Scrolls.Add((key, view));
            Current.Add(view);
            stack.Push(view.contentContainer);
            Signature.Append('V').Append(key).Append('(');
            content();
            Signature.Append(')');
            stack.Pop();
        }

        public void Canvas(float width, float height, Action content)
        {
            var canvas = new VisualElement { pickingMode = PickingMode.Ignore };
            canvas.style.width = width;
            canvas.style.height = height;
            canvas.style.flexShrink = 0;
            Nest(canvas, content, 'c');
        }

        public VisualElement Absolute(float left, float top, float width, float height, Color background, Action content)
        {
            var box = new VisualElement();
            box.AddToClassList("site");
            box.style.left = left;
            box.style.top = top;
            box.style.width = width;
            box.style.height = height;
            box.style.backgroundColor = background;
            Current.Add(box);
            stack.Push(box);
            Signature.Append('A').Append(left).Append(',').Append(top).Append('(');
            content();
            Signature.Append(')');
            stack.Pop();
            return box;
        }

        public void Key(object key) => Signature.Append('K').Append(key);

        public void Width(float width)
        {
            window.Root.style.width = width;
            Signature.Append('W').Append(width);
        }
    }
}
