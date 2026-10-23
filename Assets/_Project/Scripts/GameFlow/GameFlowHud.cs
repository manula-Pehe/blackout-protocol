using UnityEngine;
using UnityEngine.UI;

namespace BlackoutProtocol.GameFlow
{
    /// <summary>Owns an additive text-only overlay; the existing combat HUD remains untouched.</summary>
    [RequireComponent(typeof(GameFlowController))]
    [DisallowMultipleComponent]
    public sealed class GameFlowHud : MonoBehaviour
    {
        private GameFlowController controller;
        private GameObject canvasObject;
        private Text objective;
        private Text prompt;
        private Text result;

        private void Start()
        {
            controller = GetComponent<GameFlowController>();
            canvasObject = new GameObject("Game Flow HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            objective = Label("Objective", new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(580f, 60f), 21);
            prompt = Label("Interaction", new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(640f, 60f), 23);
            result = Label("Result", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 200f), 36);
        }

        private Text Label(string name, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            item.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            item.GetComponent<Outline>().effectColor = Color.black;
            return text;
        }

        private void Update()
        {
            if (controller == null || controller.State == null || objective == null) return;
            bool terminal = controller.State.IsTerminal;
            objective.text = terminal ? string.Empty : controller.Objective;
            prompt.text = terminal ? string.Empty : controller.InteractionPrompt;
            result.text = terminal ? controller.Objective + "\n\n[R / START] RESTART" : string.Empty;
        }

        private void OnDestroy() { if (canvasObject != null) Destroy(canvasObject); }
    }
}
