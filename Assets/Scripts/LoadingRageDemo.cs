using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "끝난 줄 알았는데 하나 더" 로딩바.
///
/// 매번 진짜로 100%까지 채운다. 가짜로 멈추는 것보다, 완료 후에
/// 더 작고 사소한 단계가 또 나타나는 쪽이 배신감이 크다 — 실제 앱들이
/// "추가 리소스 다운로드" 로 하는 짓을 과장한 것뿐이다.
///
/// 씬에는 이 컴포넌트 하나만 있으면 된다. 카메라/캔버스/UI를
/// 전부 코드로 만들어서, 클립마다 씬 파일을 손으로 꾸밀 필요가 없다.
/// </summary>
public class LoadingRageDemo : MonoBehaviour
{
    [System.Serializable]
    struct Stage
    {
        public string label;
        public float duration;
        public float barScale; // 1 = 풀사이즈, 갈수록 작아진다.

        public Stage(string label, float duration, float barScale)
        {
            this.label = label;
            this.duration = duration;
            this.barScale = barScale;
        }
    }

    static readonly Color BgColor = new Color32(18, 18, 28, 255);
    static readonly Color TrackColor = new Color32(38, 38, 56, 255);
    static readonly Color FillColor = new Color32(91, 141, 239, 255);
    static readonly Color CheckColor = new Color32(76, 209, 145, 255);
    static readonly Color TextColor = new Color32(235, 236, 245, 255);
    static readonly Color DimTextColor = new Color32(140, 142, 165, 200);

    readonly Stage[] _stages =
    {
        new Stage("설치 중...", 2.2f, 1f),
        new Stage("추가 데이터 확인 중...", 1.4f, 0.72f),
        new Stage("설정 동기화 중...", 1.0f, 0.5f),
    };

    Text _statusText;
    Text _percentText;
    RectTransform _barContainer;
    Image _fillImage;
    RectTransform _fillRect;
    GameObject _checkBadge;
    Image _blackout;

    Font _font;

    void Start()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
        StartCoroutine(PlaySequence());
    }

    IEnumerator PlaySequence()
    {
        for (int i = 0; i < _stages.Length; i++)
        {
            Stage stage = _stages[i];
            SetBarScale(stage.barScale);
            _statusText.text = stage.label;

            yield return FillBar(stage.duration);
            yield return PopCheck();
            yield return new WaitForSeconds(i == _stages.Length - 1 ? 0.3f : 0.4f);

            HideCheck();
        }

        yield return Blackout();
    }

    IEnumerator FillBar(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            // 이즈아웃 — 처음엔 빠르게 차오르다 끝에서 살짝 늦춰야
            // "곧 끝난다" 는 기대감이 붙는다.
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            _fillRect.anchorMax = new Vector2(eased, 1f);
            _percentText.text = Mathf.RoundToInt(eased * 100f) + "%";
            yield return null;
        }
        _fillRect.anchorMax = new Vector2(1f, 1f);
        _percentText.text = "100%";
    }

    IEnumerator PopCheck()
    {
        _checkBadge.SetActive(true);
        var rt = _checkBadge.GetComponent<RectTransform>();

        float t = 0f;
        const float pop = 0.22f;
        while (t < pop)
        {
            t += Time.deltaTime;
            float k = t / pop;
            // 0 -> 1.15 -> 1.0 으로 살짝 튀는 스케일. 뿅 하고 나타나는 느낌.
            float scale = k < 0.7f
                ? Mathf.Lerp(0f, 1.15f, k / 0.7f)
                : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f);
            rt.localScale = Vector3.one * scale;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    void HideCheck()
    {
        _checkBadge.SetActive(false);
        _fillRect.anchorMax = new Vector2(0f, 1f);
        _percentText.text = "0%";
    }

    IEnumerator Blackout()
    {
        // 완료 표시가 뜨자마자 화면이 꺼진다 — 보상이 아예 없는 결말.
        yield return new WaitForSeconds(0.15f);
        _blackout.gameObject.SetActive(true);
        _blackout.color = new Color(0f, 0f, 0f, 1f);
        yield return new WaitForSeconds(1.2f);
    }

    void SetBarScale(float scale)
    {
        _barContainer.localScale = Vector3.one * scale;
    }

    // ------------------------------------------------------------------
    // UI 구성 — 전부 코드로. 라운드 사각형은 절차적으로 텍스처를 그려서
    // 9-slice 스프라이트로 만든다 (외부 이미지 없이 둥근 모서리를 쓰기 위함).
    // ------------------------------------------------------------------

    void BuildUI()
    {
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        var bg = CreateImage(canvasGO.transform, "Background", BgColor);
        StretchFull(bg.rectTransform);

        // 상태 문구
        _statusText = CreateText(canvasGO.transform, "StatusText", "", 42, TextColor, FontStyle.Bold);
        var statusRT = _statusText.rectTransform;
        statusRT.anchorMin = statusRT.anchorMax = new Vector2(0.5f, 0.5f);
        statusRT.anchoredPosition = new Vector2(0f, 90f);
        statusRT.sizeDelta = new Vector2(900f, 80f);

        // 바 컨테이너 (스케일로 단계마다 크기를 줄인다)
        var barContainerGO = new GameObject("BarContainer", typeof(RectTransform));
        _barContainer = barContainerGO.GetComponent<RectTransform>();
        _barContainer.SetParent(canvasGO.transform, false);
        _barContainer.anchorMin = _barContainer.anchorMax = new Vector2(0.5f, 0.5f);
        _barContainer.sizeDelta = new Vector2(760f, 56f);
        _barContainer.anchoredPosition = Vector2.zero;

        var track = CreateRoundedImage(_barContainer, "Track", TrackColor, 28);
        StretchFull(track.rectTransform);

        var fillContainerGO = new GameObject("FillContainer", typeof(RectTransform));
        var fillContainerRT = fillContainerGO.GetComponent<RectTransform>();
        fillContainerRT.SetParent(_barContainer, false);
        StretchFull(fillContainerRT);
        // 패딩을 살짝 줘서 트랙 테두리 안쪽에 필이 뜨도록.
        fillContainerRT.offsetMin = new Vector2(6f, 6f);
        fillContainerRT.offsetMax = new Vector2(-6f, -6f);

        _fillImage = CreateRoundedImage(fillContainerRT, "Fill", FillColor, 22);
        _fillRect = _fillImage.rectTransform;
        _fillRect.anchorMin = new Vector2(0f, 0f);
        _fillRect.anchorMax = new Vector2(0f, 1f);
        _fillRect.offsetMin = Vector2.zero;
        _fillRect.offsetMax = Vector2.zero;

        // 퍼센트 문구
        _percentText = CreateText(canvasGO.transform, "PercentText", "0%", 34, DimTextColor, FontStyle.Normal);
        var percentRT = _percentText.rectTransform;
        percentRT.anchorMin = percentRT.anchorMax = new Vector2(0.5f, 0.5f);
        percentRT.anchoredPosition = new Vector2(0f, -70f);
        percentRT.sizeDelta = new Vector2(300f, 60f);

        // 완료 체크 배지
        _checkBadge = CreateCircleBadge(canvasGO.transform);
        _checkBadge.SetActive(false);

        // 시리즈 스탬프
        var stamp = CreateText(canvasGO.transform, "Stamp", "Made in Unity · Day 1", 24, DimTextColor, FontStyle.Italic);
        var stampRT = stamp.rectTransform;
        stampRT.anchorMin = stampRT.anchorMax = new Vector2(1f, 0f);
        stampRT.pivot = new Vector2(1f, 0f);
        stampRT.anchoredPosition = new Vector2(-32f, 32f);
        stampRT.sizeDelta = new Vector2(500f, 50f);

        // 블랙아웃 — 맨 위에, 처음엔 꺼둔다.
        _blackout = CreateImage(canvasGO.transform, "Blackout", new Color(0f, 0f, 0f, 0f));
        StretchFull(_blackout.rectTransform);
        _blackout.gameObject.SetActive(false);

        if (Camera.main == null)
        {
            var camGO = new GameObject("Main Camera", typeof(Camera));
            camGO.tag = "MainCamera";
            camGO.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        }

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }
    }

    GameObject CreateCircleBadge(Transform parent)
    {
        var go = new GameObject("CheckBadge", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(120f, 120f);
        rt.anchoredPosition = new Vector2(0f, 220f);

        var circle = CreateRoundedImage(rt, "Circle", CheckColor, 60);
        StretchFull(circle.rectTransform);

        var check = CreateText(rt, "Check", "✓", 64, Color.white, FontStyle.Bold);
        check.alignment = TextAnchor.MiddleCenter;
        StretchFull(check.rectTransform);

        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    Image CreateRoundedImage(Transform parent, string name, Color color, int radius)
    {
        var img = CreateImage(parent, name, color);
        img.sprite = GetRoundedSprite(radius);
        img.type = Image.Type.Sliced;
        return img;
    }

    Text CreateText(Transform parent, string name, string content, int size, Color color, FontStyle style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = _font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = content;
        return text;
    }

    static readonly System.Collections.Generic.Dictionary<int, Sprite> _spriteCache = new();

    /// <summary>
    /// 둥근 사각형을 절차적으로 그려 9-slice 스프라이트로 만든다.
    /// 반지름별로 캐싱해서 같은 크기를 여러 번 안 그린다.
    /// </summary>
    static Sprite GetRoundedSprite(int radius)
    {
        if (_spriteCache.TryGetValue(radius, out var cached)) return cached;

        int size = radius * 2 + 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = RoundedAlpha(x, y, size, radius);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        sprite.name = "RoundedRect_" + radius;

        _spriteCache[radius] = sprite;
        return sprite;
    }

    /// <summary>모서리 안쪽이면 1, 바깥이면 0, 경계는 안티에일리어싱.</summary>
    static float RoundedAlpha(int x, int y, int size, int radius)
    {
        float cx = Mathf.Clamp(x, radius, size - radius - 1);
        float cy = Mathf.Clamp(y, radius, size - radius - 1);
        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

        // 코너 밖(모서리 원 바깥)에서만 거리 기반으로 깎아낸다.
        bool inCornerZone = (x < radius || x >= size - radius) && (y < radius || y >= size - radius);
        if (!inCornerZone) return 1f;

        return Mathf.Clamp01(radius - dist + 0.5f);
    }
}
