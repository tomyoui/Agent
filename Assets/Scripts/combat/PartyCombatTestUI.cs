using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartyCombatTestUI : MonoBehaviour
{
    [SerializeField] private PartyManager2D partyManager;
    [SerializeField] private Vector2 anchoredPosition = new Vector2(10f, -10f);
    [SerializeField] private Vector2 panelSize = new Vector2(230f, 158f);
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;
    [SerializeField, Min(1f)] private float hudScale = 1.6f;
    [SerializeField] private int hudSortingOrder = 110;
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.28f);
    [SerializeField] private Color rowColor = new Color(0.045f, 0.05f, 0.058f, 0.68f);
    [SerializeField] private Color activeRowColor = new Color(0.11f, 0.095f, 0.045f, 0.72f);
    [SerializeField] private Color activeBorderColor = new Color(0.95f, 0.72f, 0.24f, 0.85f);
    [SerializeField] private Color inactiveBorderColor = new Color(1f, 1f, 1f, 0.13f);
    [SerializeField] private Color deadRowColor = new Color(0.018f, 0.018f, 0.02f, 0.64f);
    [SerializeField] private Color normalTextColor = new Color(0.9f, 0.92f, 0.94f, 1f);
    [SerializeField] private Color activeTextColor = new Color(1f, 0.86f, 0.46f, 1f);
    [SerializeField] private Color deadTextColor = new Color(0.42f, 0.42f, 0.42f, 1f);
    [SerializeField] private Color hpBackColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color hpFillColor = new Color(0.32f, 0.72f, 0.42f, 1f);
    [SerializeField] private Color lowHpFillColor = new Color(0.9f, 0.3f, 0.26f, 1f);
    [SerializeField] private Color readyTextColor = new Color(0.68f, 0.92f, 0.7f, 1f);
    [SerializeField] private Color cooldownTextColor = new Color(0.92f, 0.7f, 0.34f, 1f);

    private const int MaxVisibleMembers = 3;

    private readonly MemberRow[] _rows = new MemberRow[MaxVisibleMembers];
    private TextMeshProUGUI _skillTitleText;
    private TextMeshProUGUI _eSkillText;
    private TextMeshProUGUI _qSkillText;
    private GameObject _hudRoot;
    private RectTransform _panelRect;
    private Canvas _hudCanvas;
    private bool _loggedHudState;
    private bool _loggedStoppedHudState;

    private sealed class MemberRow
    {
        public Image Border;
        public Image Background;
        public TextMeshProUGUI NumberText;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI HpText;
        public Image HpFill;
    }

    private void Awake()
    {
        if (partyManager == null)
        {
            partyManager = PartyManager2D.Instance != null
                ? PartyManager2D.Instance
                : FindFirstObjectByType<PartyManager2D>();
        }

        BuildUI();
        ConfigureCanvasScaler();
        LogHudState("Awake");
    }

    private void OnEnable()
    {
        EnsureHudVisible();
    }

    private void Update()
    {
        EnsureHudVisible();

        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            LogStoppedHudState("Update");
        }
        else
        {
            _loggedStoppedHudState = false;
        }

        if (partyManager == null)
        {
            return;
        }

        UpdateMemberRows();
        UpdateSkillStatus();
    }

    private void LateUpdate()
    {
        EnsureHudVisible();

        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            LogStoppedHudState("LateUpdate");
        }
    }

    public void EnsureStoppedHudVisible(string context)
    {
        EnsureHudVisible();
        LogStoppedHudState(context);
    }

    private void BuildUI()
    {
        Transform existingRoot = transform.Find("PartyCombatTestHUD");
        if (existingRoot != null)
        {
            _hudRoot = existingRoot.gameObject;
            _panelRect = existingRoot.Find("PartyCombatTestPanel") as RectTransform;
            _hudCanvas = _hudRoot.GetComponent<Canvas>();
            return;
        }

        GameObject rootObject = new GameObject("PartyCombatTestHUD", typeof(RectTransform));
        rootObject.transform.SetParent(transform, false);
        _hudRoot = rootObject;
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        _hudCanvas = rootObject.AddComponent<Canvas>();
        _hudCanvas.overrideSorting = true;
        _hudCanvas.sortingOrder = hudSortingOrder;

        BuildPartyPanel(rootObject.transform);
    }

    private void BuildPartyPanel(Transform root)
    {
        GameObject panelObject = CreateImageObject(root, "PartyCombatTestPanel", panelColor);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        _panelRect = panelRect;
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = panelSize;
        panelRect.localScale = Vector3.one * hudScale;

        TextMeshProUGUI titleText = CreateText(panelObject.transform, "TitleText", new Vector2(9f, -6f), new Vector2(210f, 18f), 13f);
        titleText.text = "PARTY";
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(1f, 1f, 1f, 0.86f);

        for (int i = 0; i < MaxVisibleMembers; i++)
        {
            _rows[i] = CreateMemberRow(panelObject.transform, i);
        }

        _skillTitleText = CreateText(panelObject.transform, "SkillTitleText", new Vector2(9f, -126f), new Vector2(48f, 16f), 11f);
        _skillTitleText.text = "SKILL";
        _skillTitleText.fontStyle = FontStyles.Bold;
        _skillTitleText.color = new Color(1f, 1f, 1f, 0.68f);

        _eSkillText = CreateText(panelObject.transform, "ESkillText", new Vector2(58f, -126f), new Vector2(76f, 16f), 11f);
        _eSkillText.fontStyle = FontStyles.Bold;

        _qSkillText = CreateText(panelObject.transform, "QSkillText", new Vector2(140f, -126f), new Vector2(82f, 16f), 11f);
        _qSkillText.fontStyle = FontStyles.Bold;
    }

    private MemberRow CreateMemberRow(Transform parent, int index)
    {
        GameObject borderObject = CreateImageObject(parent, $"MemberSlot_{index + 1}", inactiveBorderColor);
        RectTransform borderRect = borderObject.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0f, 1f);
        borderRect.anchorMax = new Vector2(0f, 1f);
        borderRect.pivot = new Vector2(0f, 1f);
        borderRect.anchoredPosition = new Vector2(8f, -27f - (index * 32f));
        borderRect.sizeDelta = new Vector2(214f, 28f);

        GameObject backgroundObject = CreateImageObject(borderObject.transform, "Background", rowColor);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = new Vector2(1f, 1f);
        backgroundRect.offsetMax = new Vector2(-1f, -1f);

        TextMeshProUGUI numberText = CreateText(backgroundObject.transform, "NumberText", new Vector2(6f, -3f), new Vector2(28f, 15f), 12f);
        TextMeshProUGUI nameText = CreateText(backgroundObject.transform, "NameText", new Vector2(34f, -3f), new Vector2(92f, 15f), 12f);
        TextMeshProUGUI hpText = CreateText(backgroundObject.transform, "HpText", new Vector2(128f, -3f), new Vector2(76f, 15f), 11f);
        hpText.alignment = TextAlignmentOptions.MidlineRight;

        Image hpFill = CreateHpBar(backgroundObject.transform, "HpBar", new Vector2(34f, -20f), new Vector2(170f, 4f));

        return new MemberRow
        {
            Border = borderObject.GetComponent<Image>(),
            Background = backgroundObject.GetComponent<Image>(),
            NumberText = numberText,
            NameText = nameText,
            HpText = hpText,
            HpFill = hpFill
        };
    }

    private GameObject CreateImageObject(Transform parent, string objectName, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return imageObject;
    }

    private Image CreateHpBar(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject backObject = CreateImageObject(parent, objectName, hpBackColor);
        RectTransform backRect = backObject.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 1f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 1f);
        backRect.anchoredPosition = anchoredPosition;
        backRect.sizeDelta = size;

        GameObject fillObject = CreateImageObject(backObject.transform, "Fill", hpFillColor);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        return fillObject.GetComponent<Image>();
    }

    private TextMeshProUGUI CreateText(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, float fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = normalTextColor;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;

        return text;
    }

    private void ConfigureCanvasScaler()
    {
        CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
        if (scaler == null)
        {
            return;
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = matchWidthOrHeight;

        Canvas canvas = scaler.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.enabled = true;
            canvas.overrideSorting = false;
        }
    }

    private void EnsureHudVisible()
    {
        if (_hudRoot != null && !_hudRoot.activeSelf)
        {
            _hudRoot.SetActive(true);
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && !canvas.enabled)
        {
            canvas.enabled = true;
        }

        if (_hudRoot != null && _hudCanvas == null)
        {
            _hudCanvas = _hudRoot.GetComponent<Canvas>();
            if (_hudCanvas == null)
            {
                _hudCanvas = _hudRoot.AddComponent<Canvas>();
            }
        }

        if (_hudCanvas != null)
        {
            _hudCanvas.overrideSorting = true;
            _hudCanvas.sortingOrder = hudSortingOrder;
        }

        if (_panelRect == null && _hudRoot != null)
        {
            _panelRect = _hudRoot.transform.Find("PartyCombatTestPanel") as RectTransform;
        }

        if (_panelRect == null)
        {
            return;
        }

        _panelRect.anchorMin = new Vector2(0f, 1f);
        _panelRect.anchorMax = new Vector2(0f, 1f);
        _panelRect.pivot = new Vector2(0f, 1f);
        _panelRect.anchoredPosition = anchoredPosition;
        _panelRect.sizeDelta = panelSize;
        _panelRect.localScale = Vector3.one * hudScale;
    }

    private void LogHudState(string context)
    {
        if (_loggedHudState)
        {
            return;
        }

        _loggedHudState = true;

        CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
        Canvas canvas = scaler != null ? scaler.GetComponent<Canvas>() : GetComponentInParent<Canvas>();
        RectTransform rootRect = _hudRoot != null ? _hudRoot.GetComponent<RectTransform>() : null;
        CanvasGroup group = _hudRoot != null ? _hudRoot.GetComponentInParent<CanvasGroup>() : null;

        string scalerInfo = scaler != null
            ? $"mode={scaler.uiScaleMode}, ref={scaler.referenceResolution}, matchMode={scaler.screenMatchMode}, match={scaler.matchWidthOrHeight}, scaleFactor={(canvas != null ? canvas.scaleFactor : 0f):0.###}"
            : "CanvasScaler 없음";
        string canvasInfo = canvas != null
            ? $"enabled={canvas.enabled}, renderMode={canvas.renderMode}, sortingOrder={canvas.sortingOrder}"
            : "Canvas 없음";
        string rootInfo = rootRect != null
            ? $"scale={rootRect.localScale}, size={rootRect.sizeDelta}, pos={rootRect.anchoredPosition}, activeSelf={_hudRoot.activeSelf}, activeInHierarchy={_hudRoot.activeInHierarchy}"
            : "HUD Root 없음";
        string groupInfo = group != null
            ? $"alpha={group.alpha}, interactable={group.interactable}, blocksRaycasts={group.blocksRaycasts}"
            : "CanvasGroup 없음";

        Debug.Log($"[HUD] {context} CanvasScaler 확인: {scalerInfo}", this);
        Debug.Log($"[HUD] {context} Canvas 확인: {canvasInfo}", this);
        Debug.Log($"[HUD] {context} Root Rect 확인: {rootInfo}", this);
        Debug.Log($"[HUD] {context} CanvasGroup 확인: {groupInfo}", this);
    }

    private void LogStoppedHudState(string context)
    {
        if (_loggedStoppedHudState)
        {
            return;
        }

        _loggedStoppedHudState = true;

        CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
        Canvas parentCanvas = scaler != null ? scaler.GetComponent<Canvas>() : GetComponentInParent<Canvas>();
        RectTransform rootRect = _hudRoot != null ? _hudRoot.GetComponent<RectTransform>() : null;
        CanvasGroup group = _hudRoot != null ? _hudRoot.GetComponentInParent<CanvasGroup>() : null;

        string rootActive = _hudRoot != null
            ? $"rootActive={_hudRoot.activeSelf}, hierarchy={_hudRoot.activeInHierarchy}"
            : "HUD Root 없음";
        string parentCanvasInfo = parentCanvas != null
            ? $"canvasEnabled={parentCanvas.enabled}, renderMode={parentCanvas.renderMode}, sortingOrder={parentCanvas.sortingOrder}"
            : "부모 Canvas 없음";
        string hudCanvasInfo = _hudCanvas != null
            ? $"hudCanvasEnabled={_hudCanvas.enabled}, overrideSorting={_hudCanvas.overrideSorting}, sortingOrder={_hudCanvas.sortingOrder}"
            : "HUD Canvas 없음";
        string scalerInfo = scaler != null
            ? $"scaleMode={scaler.uiScaleMode}, ref={scaler.referenceResolution}, match={scaler.matchWidthOrHeight}"
            : "CanvasScaler 없음";
        string rectInfo = rootRect != null
            ? $"scale={rootRect.localScale}, pos={rootRect.anchoredPosition}, size={rootRect.sizeDelta}"
            : "Root Rect 없음";
        string panelInfo = _panelRect != null
            ? $"panelScale={_panelRect.localScale}, panelPos={_panelRect.anchoredPosition}, panelSize={_panelRect.sizeDelta}"
            : "Panel Rect 없음";
        string groupInfo = group != null
            ? $"alpha={group.alpha}, interactable={group.interactable}, blocksRaycasts={group.blocksRaycasts}"
            : "CanvasGroup 없음";

        Debug.Log($"[HUD] 정지 상태 HUD 확인({context}): timeScale={Time.timeScale}, {rootActive}, {parentCanvasInfo}, {hudCanvasInfo}, {scalerInfo}, {rectInfo}, {panelInfo}, {groupInfo}", this);
    }

    private void UpdateMemberRows()
    {
        int activeIndex = partyManager.CurrentIndex;

        for (int i = 0; i < _rows.Length; i++)
        {
            MemberRow row = _rows[i];
            if (row == null)
            {
                continue;
            }

            GameObject member = partyManager.GetPartyMember(i);
            Health health = partyManager.GetPartyMemberHealth(i);
            bool isActive = i == activeIndex;
            bool isDead = health != null && health.IsDead;

            row.Border.color = isActive ? activeBorderColor : inactiveBorderColor;
            row.Background.color = isDead ? deadRowColor : isActive ? activeRowColor : rowColor;

            Color textColor = isDead ? deadTextColor : isActive ? activeTextColor : normalTextColor;
            row.NumberText.color = textColor;
            row.NameText.color = textColor;
            row.HpText.color = textColor;

            row.NumberText.text = $"[{i + 1}]";

            if (member == null)
            {
                row.NameText.text = "EMPTY";
                row.HpText.text = "HP n/a";
                SetHpFill(row.HpFill, 0f, isDead);
                continue;
            }

            row.NameText.text = CleanMemberName(member.name);
            row.HpText.text = health != null ? $"{health.CurrentHP}/{health.MaxHP}" : "HP n/a";
            SetHpFill(row.HpFill, GetHealthRatio(health), isDead);
        }
    }

    private void UpdateSkillStatus()
    {
        BasePlayableCombat2D combat = partyManager.GetCurrentCombat();

        if (_eSkillText == null || _qSkillText == null)
        {
            return;
        }

        if (combat == null)
        {
            _eSkillText.text = "E N/A";
            _eSkillText.color = cooldownTextColor;
            _qSkillText.text = "Q 0%";
            _qSkillText.color = normalTextColor;
            return;
        }

        float remaining = combat.SkillCooldownRemaining;
        bool skillReady = remaining <= 0f;
        _eSkillText.text = skillReady ? "E READY" : $"E {remaining:0.0}";
        _eSkillText.color = skillReady ? readyTextColor : cooldownTextColor;

        int percent = Mathf.RoundToInt(Mathf.Clamp01(combat.UltimateGaugeRatio) * 100f);
        _qSkillText.text = $"Q {percent}%";
        _qSkillText.color = combat.IsUltimateReady ? activeTextColor : normalTextColor;
    }

    private void SetHpFill(Image fill, float ratio, bool isDead)
    {
        if (fill == null)
        {
            return;
        }

        RectTransform rect = fill.rectTransform;
        Vector3 scale = rect.localScale;
        scale.x = Mathf.Clamp01(ratio);
        rect.localScale = scale;
        fill.color = isDead || ratio <= 0f ? deadTextColor : ratio <= 0.3f ? lowHpFillColor : hpFillColor;
    }

    private float GetHealthRatio(Health health)
    {
        if (health == null || health.MaxHP <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)health.CurrentHP / health.MaxHP);
    }

    private string CleanMemberName(string rawName)
    {
        return string.IsNullOrWhiteSpace(rawName)
            ? "Unknown"
            : rawName.Replace("(Clone)", "").Trim();
    }
}
