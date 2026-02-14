using UnityEngine;
using UnityEngine.UI;
using static Game.UI.Layout.UiElements;

namespace Game.UI.Layout {
    internal static class MenuUiBuilder {
        public static MenuView Create(Transform parent) {
            var canvas = CreateCanvas("MenuCanvas", parent);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var title = CreateText("Title", canvas.transform, font, TextAnchor.MiddleCenter, 64, Color.white, "АРЕНА");
            SetAnchor(title.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 100f));
            var summary = CreateText("Progress", canvas.transform, font, TextAnchor.MiddleCenter, 30, new Color(0.7f, 0.9f, 0.85f));
            SetAnchor(summary.rectTransform, new Vector2(0.5f, 0.61f), new Vector2(0.5f, 0.61f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 150f));
            var instructions = CreateText("Instructions", canvas.transform, font, TextAnchor.MiddleCenter, 27, Color.white);
            SetAnchor(instructions.rectTransform, new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1800f, 150f));
            var status = CreateText("SaveStatus", canvas.transform, font, TextAnchor.MiddleCenter, 22, Color.gray);
            SetAnchor(status.rectTransform, new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 60f));
            var play = CreateButton("PlayButton", canvas.transform, font, "Начать бой", 32);
            play.GetComponent<Image>().color = new Color(0.18f, 0.65f, 0.24f, 1f);
            SetAnchor(play.GetComponent<RectTransform>(), new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330f, 82f));
            var view = canvas.gameObject.AddComponent<MenuView>();
            view.Initialize(summary, instructions, status, play);
            return view;
        }
    }
}
