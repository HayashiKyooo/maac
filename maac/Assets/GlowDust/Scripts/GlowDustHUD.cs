using UnityEngine;
using UnityEngine.UI;

namespace GlowDust
{
    /// <summary>Simple runtime-built HUD: crosshair, powder count (bottom-left), messages, clear screen.</summary>
    public class GlowDustHUD : MonoBehaviour
    {
        static readonly Color Accent = new Color(0.65f, 0.55f, 1f, 1f);
        static readonly Color Dim = new Color(0.35f, 0.32f, 0.5f, 0.5f);

        Font font;
        Text countText, messageText, hintText, clearText;
        Image[] pips;
        GameObject clearPanel;
        float messageUntil, hintUntil;

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            // crosshair
            MakeImage(root, "CrosshairDot", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5, 5), new Color(1, 1, 1, 0.85f));
            MakeImage(root, "CrossL", new Vector2(0.5f, 0.5f), new Vector2(-14, 0), new Vector2(10, 2), new Color(1, 1, 1, 0.45f));
            MakeImage(root, "CrossR", new Vector2(0.5f, 0.5f), new Vector2(14, 0), new Vector2(10, 2), new Color(1, 1, 1, 0.45f));
            MakeImage(root, "CrossU", new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(2, 10), new Color(1, 1, 1, 0.45f));
            MakeImage(root, "CrossD", new Vector2(0.5f, 0.5f), new Vector2(0, -14), new Vector2(2, 10), new Color(1, 1, 1, 0.45f));

            // powder count, bottom-left
            var label = MakeText(root, "PowderLabel", new Vector2(0, 0), new Vector2(40, 118), new Vector2(400, 30), 22, TextAnchor.LowerLeft);
            label.text = "GLOW POWDER";
            label.color = new Color(0.75f, 0.75f, 0.9f, 0.8f);
            countText = MakeText(root, "PowderCount", new Vector2(0, 0), new Vector2(40, 40), new Vector2(300, 80), 64, TextAnchor.LowerLeft);
            countText.color = Accent;
            pips = new Image[0];

            messageText = MakeText(root, "Message", new Vector2(0.5f, 0.5f), new Vector2(-400, -140), new Vector2(800, 50), 30, TextAnchor.MiddleCenter);
            messageText.color = new Color(1f, 0.6f, 0.7f, 0f);

            hintText = MakeText(root, "Hint", new Vector2(0, 1), new Vector2(30, -170), new Vector2(900, 150), 22, TextAnchor.UpperLeft);
            hintText.text = "WASD : Move    Mouse : Look    Space : Jump    Shift : Run\n" +
                            "Left Click : Throw glow powder    R : Restart\n" +
                            "Find the golden crystal. Beware of holes, cracked floors and moving platforms.\n" +
                            "Falling into the abyss = GAME OVER";
            hintText.color = new Color(0.8f, 0.8f, 0.95f, 0.85f);
            hintUntil = Time.time + 10f;

            clearPanel = new GameObject("ClearPanel", typeof(RectTransform), typeof(Image));
            clearPanel.transform.SetParent(root, false);
            var cpr = (RectTransform)clearPanel.transform;
            cpr.anchorMin = Vector2.zero; cpr.anchorMax = Vector2.one; cpr.offsetMin = cpr.offsetMax = Vector2.zero;
            clearPanel.GetComponent<Image>().color = new Color(0.02f, 0.01f, 0.05f, 0.6f);
            clearText = MakeText(clearPanel.transform, "ClearText", new Vector2(0.5f, 0.5f), new Vector2(-600, -150), new Vector2(1200, 300), 56, TextAnchor.MiddleCenter);
            clearText.color = new Color(1f, 0.85f, 0.45f, 1f);
            clearPanel.SetActive(false);
        }

        public void Bind(PowderThrower thrower)
        {
            if (thrower == null) return;
            BuildPips(thrower.MaxCount);
            thrower.CountChanged += OnCountChanged;
            thrower.ThrowRejected += () => ShowMessage("Out of glow powder", 2f);
            OnCountChanged(thrower.Count, thrower.MaxCount);
        }

        void BuildPips(int max)
        {
            var root = countText.transform.parent;
            pips = new Image[max];
            for (int i = 0; i < max; i++)
            {
                var img = MakeImage(root, "Pip" + i, new Vector2(0, 0), new Vector2(220 + (i % 10) * 20, 82 - (i / 10) * 22), new Vector2(11, 11), Accent);
                img.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                pips[i] = img;
            }
        }

        void OnCountChanged(int count, int max)
        {
            countText.text = "x " + count;
            countText.color = count > 0 ? Accent : new Color(1f, 0.45f, 0.5f, 1f);
            for (int i = 0; i < pips.Length; i++) pips[i].color = i < count ? Accent : Dim;
            if (count == 0) ShowMessage("Last powder used", 2f);
        }

        public void ShowMessage(string msg, float duration)
        {
            messageText.text = msg;
            messageUntil = Time.time + duration;
        }

        public void ShowClear(float seconds, int powderLeft)
        {
            clearPanel.SetActive(true);
            clearText.text = $"GOAL REACHED\n<size=32>Time  {seconds:0.0}s     Powder left  {powderLeft}\n\nPress R to play again</size>";
        }

        public void ShowGameOver(float seconds)
        {
            clearPanel.SetActive(true);
            clearPanel.GetComponent<Image>().color = new Color(0.08f, 0.0f, 0.03f, 0.7f);
            clearText.color = new Color(1f, 0.4f, 0.5f, 1f);
            clearText.text = $"GAME OVER\n<size=32>You fell into the abyss  ({seconds:0.0}s)\n\nPress R to retry</size>";
        }

        void Update()
        {
            var mc = messageText.color;
            mc.a = Mathf.MoveTowards(mc.a, Time.time < messageUntil ? 1f : 0f, Time.deltaTime * 3f);
            messageText.color = mc;
            var hc = hintText.color;
            hc.a = Mathf.MoveTowards(hc.a, Time.time < hintUntil ? 0.85f : 0f, Time.deltaTime * 0.5f);
            hintText.color = hc;
        }

        Image MakeImage(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
