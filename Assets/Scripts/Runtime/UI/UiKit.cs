using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 정식 UI(2026-10-23, Docs/decision_log_auto.md UI1~UI5)를 코드로 만드는 도구. 색·글자 크기는 <see cref="Theme"/> 한 곳에서 바꾼다.
    /// </summary>
    public static class UiKit
    {
        /// <summary>색과 글자 크기. 어두운 반투명 패널 + 모래색 강조(UI4).</summary>
        public static class Theme
        {
            public static readonly Color Panel = new Color(0.10f, 0.09f, 0.08f, 0.86f);
            public static readonly Color PanelLight = new Color(0.20f, 0.18f, 0.15f, 0.92f);
            /// <summary>결과창처럼 뒤 지도가 비치면 읽기 어려운 창(결정 UV4).</summary>
            public static readonly Color PanelSolid = new Color(0.12f, 0.11f, 0.09f, 0.98f);
            public static readonly Color Text = new Color(0.94f, 0.91f, 0.85f);
            public static readonly Color Muted = new Color(0.68f, 0.63f, 0.56f);
            public static readonly Color Accent = new Color(0.90f, 0.72f, 0.42f);
            public static readonly Color Good = new Color(0.52f, 0.80f, 0.50f);
            public static readonly Color Warn = new Color(0.95f, 0.66f, 0.30f);
            public static readonly Color Danger = new Color(0.95f, 0.38f, 0.32f);
            public static readonly Color ButtonColor = new Color(0.28f, 0.25f, 0.21f, 1f);
            public static readonly Color ButtonOn = new Color(0.62f, 0.47f, 0.24f, 1f);
            public static readonly Color Water = new Color(0.42f, 0.78f, 0.92f);
            // 글자 크기(px, 1920×1080 기준). 2026-10-23 검토 UX1: 12px이 작아 Small 12 → 14, Body 14 → 15, Title 16 → 17.
            public const int Huge = 24, Big = 20, Title = 17, Body = 15, Small = 14;
        }

        private static Font _font;
        /// <summary>기본 동적 폰트. 한글은 운영체제 글꼴로 대체된다(UI1).</summary>
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>앵커와 위치. anchorMin/Max는 부모 기준 0~1, pivot은 자기 기준.</summary>
        public static RectTransform Place(this RectTransform r, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.pivot = pivot;
            r.anchoredPosition = position;
            r.sizeDelta = size;
            return r;
        }

        public static RectTransform Panel(string name, Transform parent, Color? color = null)
        {
            RectTransform r = Rect(name, parent);
            r.gameObject.AddComponent<Image>().color = color ?? Theme.Panel;
            return r;
        }

        /// <summary>
        /// 코드로 만든 글자·버튼 오브젝트의 이름: 종류 + 처음 문구(결정 UK1). 플레이 중 Hierarchy에서 화면 문구로 찾을 수 있다(예: "Button 저장 F5").
        /// 이름은 만들 때 한 번만 정한다(나중에 바뀌는 글자는 따라가지 않음).
        /// </summary>
        private static string NameFor(string kind, string text)
        {
            if (string.IsNullOrEmpty(text)) return kind;
            string plain = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "").Replace('\n', ' ').Trim();
            if (plain.Length > 24) plain = plain.Substring(0, 24) + "…";
            return plain.Length == 0 ? kind : kind + " " + plain;
        }

        public static Text Label(Transform parent, string text, int size = Theme.Body, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            RectTransform r = Rect(NameFor("Text", text), parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.color = color ?? Theme.Text;
            t.alignment = align;
            t.supportRichText = true;
            // 지도 위에서도 읽히게 글자에 옅은 그림자(결정 UV4).
            var shadow = r.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(1f, -1f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;
            return t;
        }

        public static UnityEngine.UI.Button Btn(Transform parent, string text, Action onClick, float width = -1, float height = 30, int size = Theme.Body)
        {
            RectTransform r = Rect(NameFor("Button", text), parent);
            var image = r.gameObject.AddComponent<Image>();
            image.color = Theme.ButtonColor;
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = image;
            ColorBlock colors = b.colors;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            Text label = Label(r, text, size, Theme.Text, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, new Vector2(6, 2));
            var le = r.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            if (width > 0) { le.minWidth = width; le.preferredWidth = width; }
            return b;
        }

        /// <summary>버튼 글자를 바꾼다.</summary>
        public static void SetText(this UnityEngine.UI.Button b, string text) => b.GetComponentInChildren<Text>().text = text;

        /// <summary>선택된 도구처럼 켜진 버튼 색.</summary>
        public static void SetOn(this UnityEngine.UI.Button b, bool on) => ((Image)b.targetGraphic).color = on ? Theme.ButtonOn : Theme.ButtonColor;

        /// <summary>마우스를 올리면 툴팁을 띄운다(결정 HT1). 문구 함수는 툴팁이 보이는 동안 0.2초마다 다시 부른다. 요소에 마우스를 받는 그림(Image 등)이 있어야 한다.</summary>
        public static void Tip(this Component c, Func<string> content)
        {
            HoverTip tip = c.GetComponent<HoverTip>() ?? c.gameObject.AddComponent<HoverTip>();
            tip.Content = content;
        }

        public static void Stretch(RectTransform r, Vector2 inset)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = inset;
            r.offsetMax = -inset;
        }

        public static HorizontalLayoutGroup Row(Transform parent, float spacing = 6, int padding = 0)
        {
            RectTransform r = Rect("Row", parent);
            var g = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.padding = new RectOffset(padding, padding, padding, padding);
            g.childAlignment = TextAnchor.MiddleLeft;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;
            return g;
        }

        public static VerticalLayoutGroup Column(Transform parent, float spacing = 4, int padding = 0)
        {
            RectTransform r = Rect("Column", parent);
            var g = r.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = new RectOffset(padding, padding, padding, padding);
            g.childAlignment = TextAnchor.UpperLeft;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = false;
            return g;
        }

        /// <summary>묶음 사이 세로 구분선(2026-10-27 LY1, 이슈 #5).</summary>
        public static void Divider(Transform parent, float height = 30)
        {
            RectTransform r = Rect("Divider", parent);
            r.gameObject.AddComponent<Image>().color = Theme.Muted;
            var le = r.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = 2;
            le.minHeight = le.preferredHeight = height;
        }

        /// <summary>
        /// 좁은 화면 통째 축소(2026-10-27 LY3, 이슈 #5): 필요한 폭이 쓸 수 있는 폭보다 넓을 때만 같은 비율로 줄인다(넉넉하면 1).
        /// 16:9·16:10은 1이 되도록 각 구역 폭을 맞춰 두었다. 1보다 작으면 글자가 조금 흐려질 수 있다.
        /// </summary>
        public static float FitScale(float available, float needed) => needed > 0 ? Mathf.Clamp(available / needed, 0.5f, 1f) : 1f;

        /// <summary>구역을 축소 비율대로 그린다(피벗 기준).</summary>
        public static void SetScale(RectTransform r, float s)
        {
            if (!Mathf.Approximately(r.localScale.x, s)) r.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>내용 크기에 맞춰 늘어나게(세로 목록·패널).</summary>
        public static void FitHeight(GameObject go)
        {
            var f = go.GetComponent<ContentSizeFitter>() ?? go.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>
        /// 자식을 모두 지운다. Destroy는 프레임 끝에 지우므로 먼저 끄고 부모에서 떼어 낸다(2026-10-26 TX2): 그러지 않으면 같은 프레임에
        /// 새 내용을 만들고 크기를 잴 때(가운데 창 FitWindow) 지울 예정인 옛 내용까지 레이아웃·childCount에 들어가 창 크기가 틀렸다.
        /// </summary>
        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject child = t.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(child);
            }
        }

        private static Sprite _circle;

        /// <summary>원형 그래프용 원 스프라이트(코드로 한 번 만듦).</summary>
        private static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                float r = n / 2f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(r - d)));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                return _circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        /// <summary>
        /// 원형 그래프(결정 FD5 식품 창고 표시). 바닥은 빈 칸 색 원, 그 위에 조각마다 '앞 조각까지의 합'만큼 채운 원을 겹친다(12시부터 시계 방향).
        /// </summary>
        public sealed class PieChart
        {
            private readonly Image[] _slices;

            internal PieChart(Transform parent, float size, int slices, Color empty)
            {
                RectTransform root = Rect("Pie", parent);
                root.gameObject.AddComponent<LayoutElement>().preferredWidth = size;
                root.GetComponent<LayoutElement>().preferredHeight = size;
                root.sizeDelta = new Vector2(size, size);
                Image Ring(string name, Color c)
                {
                    RectTransform r = Rect(name, root);
                    Stretch(r, Vector2.zero);
                    Image img = r.gameObject.AddComponent<Image>();
                    img.sprite = Circle;
                    img.color = c;
                    img.type = Image.Type.Filled;
                    img.fillMethod = Image.FillMethod.Radial360;
                    img.fillOrigin = (int)Image.Origin360.Top;
                    img.fillClockwise = true;
                    img.raycastTarget = false;
                    return img;
                }
                Ring("Empty", empty).fillAmount = 1;
                _slices = new Image[slices];
                // 뒤에서부터 그린다: 마지막 조각이 가장 많이 채워지므로 먼저 만들어 뒤에 깔린다.
                for (int i = slices - 1; i >= 0; i--) _slices[i] = Ring("Slice " + i, Color.white);
            }

            /// <summary>조각 값(0~1, 원 전체 대비)과 색. 합이 1보다 작으면 나머지는 빈 칸 색.</summary>
            public void Set(IReadOnlyList<float> shares, IReadOnlyList<Color> colors)
            {
                float sum = 0;
                for (int i = 0; i < _slices.Length; i++)
                {
                    float share = i < shares.Count ? Mathf.Max(0, shares[i]) : 0;
                    sum = Mathf.Min(1, sum + share);
                    _slices[i].fillAmount = sum;
                    _slices[i].color = i < colors.Count ? colors[i] : Color.white;
                    _slices[i].enabled = share > 0;
                }
            }
        }

        public static PieChart Pie(Transform parent, float size, int slices) => new PieChart(parent, size, slices, Theme.PanelLight);

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>리치 텍스트 색 입히기.</summary>
        public static string Paint(string text, Color c) => $"<color={Hex(c)}>{text}</color>";
    }
}
