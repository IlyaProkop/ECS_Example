using UnityEngine;
using UnityEngine.UI;
using static Game.UI.Layout.UiElements;

namespace Game.UI.Layout {
    internal readonly struct ArenaUiViews {
        public readonly HudView Hud;
        public readonly ResultsView Results;
        public readonly EnemyHealthUiPresenter EnemyHealth;
        public ArenaUiViews(HudView hud, ResultsView results, EnemyHealthUiPresenter health) {
            this.Hud = hud; this.Results = results; this.EnemyHealth = health;
        }
    }
    internal static class ArenaUiBuilder {
        private static AbilityBarView CreateAbilityBar(Transform parent, Font font) {
            var root = new GameObject("AbilityBar", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            SetAnchor(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 65f), new Vector2(1120f, 62f));
            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            var slots = new AbilitySlotView[Game.Domain.Abilities.AbilityLoadout.MaxSlots];
            for (var i = 0; i < slots.Length; i++) {
                var button = CreateButton($"AbilitySlot{i}", root.transform, font, "", 20);
                button.GetComponent<Image>().color = new Color(0.08f, 0.2f, 0.26f, 0.95f);
                var size = button.gameObject.AddComponent<LayoutElement>(); size.preferredWidth = 270f; size.preferredHeight = 62f;
                var label = button.GetComponentInChildren<Text>();
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = 20;
                var progress = new GameObject("Cooldown", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                progress.transform.SetParent(button.transform, false);
                SetAnchor(progress.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, Vector2.zero, new Vector2(0f, 4f));
                progress.color = new Color(0.4f, 0.9f, 1f); progress.raycastTarget = false;
                slots[i] = button.gameObject.AddComponent<AbilitySlotView>();
                slots[i].Initialize(button, label, progress);
            }
            var view = root.AddComponent<AbilityBarView>(); view.Initialize(slots); return view;
        }
        public static ArenaUiViews Create(Transform parent) {
            var canvas = CreateCanvas("GameCanvas", parent);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var hpText = CreateText("PlayerHealth", canvas.transform, font, TextAnchor.UpperLeft, 34, Color.white);
            SetAnchor(hpText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(360f, 70f));

            var coinsText = CreateText("Coins", canvas.transform, font, TextAnchor.UpperRight, 34, Color.white);
            SetAnchor(coinsText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(360f, 70f));

            var enemyLabelsRoot = new GameObject("EnemyLabels", typeof(RectTransform)).GetComponent<RectTransform>();
            enemyLabelsRoot.SetParent(canvas.transform, false);
            SetAnchor(enemyLabelsRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            // Moving world labels rebuild their own batch. They share the parent's scaler
            // and sorting order and need no raycaster or second CanvasScaler.
            enemyLabelsRoot.gameObject.AddComponent<Canvas>();

            var objective = CreateText("Objective", canvas.transform, font, TextAnchor.UpperCenter, 28, Color.white);
            SetAnchor(objective.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(1150f, 55f));
            var controls = CreateText("Controls", canvas.transform, font, TextAnchor.LowerCenter, 24, new Color(0.8f, 0.85f, 0.9f));
            SetAnchor(controls.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(1700f, 45f));
            var statusEffects = CreateText("StatusEffects", canvas.transform, font, TextAnchor.LowerCenter, 23, new Color(0.4f, 0.9f, 1f));
            SetAnchor(statusEffects.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 125f), new Vector2(1400f, 45f));

            var panel = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            SetAnchor(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 690f));
            panel.GetComponent<Image>().color = new Color(0.03f, 0.055f, 0.085f, 0.98f);
            var title = CreateText("ResultTitle", panel.transform, font, TextAnchor.MiddleCenter, 52, Color.white);
            SetAnchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(920f, 90f));
            var details = CreateText("ResultDetails", panel.transform, font, TextAnchor.UpperLeft, 28, Color.white);
            SetAnchor(details.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -155f), new Vector2(900f, 400f));
            var restart = CreateButton("RestartButton", panel.transform, font, "Ещё бой");
            SetAnchor(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-190f, 35f), new Vector2(300f, 72f));
            var menu = CreateButton("MenuButton", panel.transform, font, "В меню");
            SetAnchor(menu.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(190f, 35f), new Vector2(300f, 72f));
            var hud = canvas.gameObject.AddComponent<HudView>();
            hud.Initialize(hpText, coinsText, objective, controls, statusEffects);
            hud.SetAbilityBar(CreateAbilityBar(canvas.transform, font));
            panel.transform.SetAsLastSibling();
            var results = canvas.gameObject.AddComponent<ResultsView>();
            results.Initialize(panel, title, details, restart, menu);
            var health = canvas.gameObject.AddComponent<EnemyHealthUiPresenter>();
            health.Initialize(enemyLabelsRoot, font, 22);
            return new ArenaUiViews(hud, results, health);
        }
    }
}
