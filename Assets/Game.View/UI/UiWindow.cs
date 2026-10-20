using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdventurerCity.View
{
    public sealed class UiWindow
    {
        const float RefreshSeconds = 0.12f;

        readonly Action<Ui> build;
        readonly VisualElement content;
        readonly Dictionary<string, Vector2> scrollOffsets = new Dictionary<string, Vector2>();
        List<(string Key, ScrollView View)> scrolls = new List<(string, ScrollView)>();
        List<Action> actions = new List<Action>();
        string signature;
        float nextRefresh;
        bool visible = true;

        public UiWindow(GameUi owner, VisualElement parent, Action<Ui> build, string cls = null, bool framed = true)
        {
            Owner = owner;
            this.build = build;
            Root = new VisualElement();
            if (framed)
            {
                Root.AddToClassList("win");
                content = new VisualElement();
                content.AddToClassList("win-inner");
                Root.Add(content);
            }
            else
            {
                Root.pickingMode = PickingMode.Ignore;
                content = Root;
            }
            if (cls != null)
                foreach (var name in cls.Split(' '))
                    Root.AddToClassList(name);
            Root.style.maxHeight = Length.Percent(100f);
            Root.style.overflow = Overflow.Hidden;
            parent.Add(Root);
        }

        public GameUi Owner { get; }
        public VisualElement Root { get; }

        public bool Visible
        {
            get => visible;
            set
            {
                if (visible == value) return;
                visible = value;
                Root.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                if (value) Refresh(true);
            }
        }

        public void Tick()
        {
            if (!visible || Time.unscaledTime < nextRefresh) return;
            Refresh(false);
        }

        public void Refresh(bool force)
        {
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            var fresh = new VisualElement { pickingMode = PickingMode.Ignore };
            var ui = new Ui(this, fresh);
            build(ui);
            var next = ui.Signature.ToString();
            if (next == signature)
            {
                actions = ui.Actions;
                return;
            }
            if (!force && Input.GetMouseButton(0)) return;

            foreach (var (key, view) in scrolls) scrollOffsets[key] = view.scrollOffset;
            Owner.HideTipIfInside(Root);
            content.Clear();
            while (fresh.childCount > 0) content.Add(fresh[0]);
            actions = ui.Actions;
            scrolls = ui.Scrolls;
            signature = next;
            foreach (var (key, view) in scrolls)
                if (scrollOffsets.TryGetValue(key, out var offset))
                {
                    var target = view;
                    target.schedule.Execute(() => target.scrollOffset = offset);
                    target.scrollOffset = offset;
                }
        }

        internal void Invoke(int index)
        {
            if (index < actions.Count) actions[index]?.Invoke();
            Owner.RefreshAll();
        }
    }
}
