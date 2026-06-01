using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossHealthBarUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform bossTransform;
    [SerializeField] private Health bossHealth;

    [Header("UI")]
    [SerializeField] private string bossName = "침식된 후사르 기사";
    [SerializeField] private TextMeshProUGUI bossNameText;
    [SerializeField] private Image hpFill;
    [SerializeField] private GameObject healthBarRoot;
    [SerializeField] private bool hideWhenDead = true;

    [Header("Auto Setup")]
    [SerializeField] private bool autoBuildMissingUI = true;
    [SerializeField] private bool configureTopCenterLayout = true;
    [SerializeField] private Vector2 healthBarSize = new Vector2(1000f, 32f);
    [SerializeField] private float yOffset = -50f;
    [SerializeField] private Color nameColor = new Color(1f, 0.91f, 0.72f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color fillColor = new Color(0.84f, 0.12f, 0.1f, 1f);

    private CanvasGroup _canvasGroup;
    private int _lastCurrentHp = -1;
    private int _lastMaxHp = -1;

    private void Awake()
    {
        ResolveBossHealth();

        if (healthBarRoot == null)
        {
            healthBarRoot = gameObject;
        }

        if (autoBuildMissingUI)
        {
            BuildMissingUI();
        }

        ConfigureLayout();
        ConfigureFillRectTransform(1f);
        ApplyBossName();
        CacheCanvasGroup();
        UpdateHealthBar(true);
    }

    private void OnValidate()
    {
        healthBarSize.x = Mathf.Clamp(healthBarSize.x, 900f, 1100f);
        healthBarSize.y = Mathf.Clamp(healthBarSize.y, 24f, 36f);
        yOffset = Mathf.Clamp(yOffset, -60f, -40f);

        ConfigureFillRectTransform(1f);

        if (bossNameText != null)
        {
            bossNameText.text = bossName;
        }
    }

    private void Update()
    {
        ResolveBossHealth();
        UpdateHealthBar(false);
    }

    public void SetBossTransform(Transform target)
    {
        bossTransform = target;
        bossHealth = null;
        ResolveBossHealth();
        UpdateHealthBar(true);
    }

    public void SetBossHealth(Health target)
    {
        bossHealth = target;
        bossTransform = target != null ? target.transform : null;
        UpdateHealthBar(true);
    }

    private void ResolveBossHealth()
    {
        if (bossHealth != null)
        {
            return;
        }

        if (bossTransform == null)
        {
            return;
        }

        bossHealth = bossTransform.GetComponent<Health>();
        if (bossHealth == null)
        {
            bossHealth = bossTransform.GetComponentInParent<Health>(true);
        }

        if (bossHealth == null)
        {
            bossHealth = bossTransform.GetComponentInChildren<Health>(true);
        }
    }

    private void UpdateHealthBar(bool force)
    {
        if (bossHealth == null || hpFill == null)
        {
            SetVisible(false);
            return;
        }

        int maxHp = Mathf.Max(0, bossHealth.MaxHP);
        int currentHp = Mathf.Clamp(bossHealth.CurrentHp, 0, maxHp);

        if (!force && currentHp == _lastCurrentHp && maxHp == _lastMaxHp)
        {
            return;
        }

        _lastCurrentHp = currentHp;
        _lastMaxHp = maxHp;

        float ratio = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;
        ConfigureFillRectTransform(ratio);

        bool isDead = bossHealth.IsDead || currentHp <= 0;
        SetVisible(!isDead || !hideWhenDead);
    }

    private void SetVisible(bool visible)
    {
        CacheCanvasGroup();

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
        else if (healthBarRoot != null && healthBarRoot != gameObject)
        {
            healthBarRoot.SetActive(visible);
        }
    }

    private void ApplyBossName()
    {
        if (bossNameText != null)
        {
            bossNameText.text = bossName;
        }
    }

    private void CacheCanvasGroup()
    {
        GameObject root = healthBarRoot != null ? healthBarRoot : gameObject;
        _canvasGroup = root.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = root.AddComponent<CanvasGroup>();
        }
    }

    private void ConfigureLayout()
    {
        if (!configureTopCenterLayout)
        {
            return;
        }

        RectTransform rect = transform as RectTransform;
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, yOffset);
        rect.sizeDelta = new Vector2(healthBarSize.x, healthBarSize.y + 34f);
    }

    private void ConfigureFillRectTransform(float healthRatio)
    {
        if (hpFill == null)
        {
            return;
        }

        RectTransform fillRect = hpFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(healthRatio), 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    private void BuildMissingUI()
    {
        if (bossNameText == null)
        {
            Transform existingName = transform.Find("BossNameText");
            bossNameText = existingName != null ? existingName.GetComponent<TextMeshProUGUI>() : null;
        }

        if (hpFill == null)
        {
            Transform existingFill = transform.Find("HPBackground/HPFill");
            hpFill = existingFill != null ? existingFill.GetComponent<Image>() : null;
        }

        if (bossNameText == null)
        {
            bossNameText = CreateNameText();
        }

        if (hpFill == null)
        {
            hpFill = CreateHealthImages();
        }
    }

    private TextMeshProUGUI CreateNameText()
    {
        GameObject textObject = new GameObject("BossNameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(healthBarSize.x, 28f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = bossName;
        text.fontSize = 24f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = nameColor;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        return text;
    }

    private Image CreateHealthImages()
    {
        GameObject backgroundObject = CreateImageObject(transform, "HPBackground", backgroundColor);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 1f);
        backgroundRect.anchorMax = new Vector2(0.5f, 1f);
        backgroundRect.pivot = new Vector2(0.5f, 1f);
        backgroundRect.anchoredPosition = new Vector2(0f, -34f);
        backgroundRect.sizeDelta = healthBarSize;

        GameObject fillObject = CreateImageObject(backgroundObject.transform, "HPFill", fillColor);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        return fillObject.GetComponent<Image>();
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
}
