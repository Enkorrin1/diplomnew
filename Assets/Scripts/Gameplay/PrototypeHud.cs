using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>Temporary HUD that keeps the first slice playable without UI assets.</summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] private GameRunController run;

        GUIStyle headingStyle;
        GUIStyle valueStyle;
        GUIStyle endStyle;

        public void Configure(GameRunController controller)
        {
            run = controller;
        }

        void OnGUI()
        {
            if (run == null)
                return;

            EnsureStyles();
            const float panelWidth = 280f;
            GUI.Box(new Rect(18f, 18f, panelWidth, 132f), string.Empty);
            GUI.Label(new Rect(34f, 28f, 230f, 28f), "ROGUE DRIVE", headingStyle);
            GUI.Label(new Rect(34f, 62f, 230f, 22f), $"Прочность: {run.Health:0} / {run.MaxHealth:0}", valueStyle);
            GUI.Label(new Rect(34f, 86f, 230f, 22f), $"Топливо: {run.Fuel:0} / {run.MaxFuel:0}", valueStyle);
            GUI.Label(new Rect(34f, 110f, 230f, 22f), $"Дистанция: {run.Distance:0} м", valueStyle);
            GUI.Label(new Rect(18f, 164f, 500f, 24f), "A / D или ← / → — сменить полосу", valueStyle);

            if (!run.IsGameOver)
                return;

            float width = 360f;
            float height = 180f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(panel, string.Empty);
            GUI.Label(new Rect(panel.x, panel.y + 26f, width, 40f), "ЗАЕЗД ЗАВЕРШЁН", endStyle);
            GUI.Label(new Rect(panel.x, panel.y + 72f, width, 24f), run.EndReason, endStyle);

            if (GUI.Button(new Rect(panel.x + 94f, panel.y + 120f, 172f, 36f), "Начать заново"))
                run.Restart();
        }

        void EnsureStyles()
        {
            if (headingStyle != null)
                return;

            headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white }
            };
            endStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }
    }
}
