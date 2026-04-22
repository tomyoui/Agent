// Editor/GameOverUIBuilder.cs
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class GameOverUIBuilder : Editor
{
    [MenuItem("Tools/UI/Setup GameOverPanel")]
    static void SetupGameOverPanel()
    {
        GameObject existing = GameObject.Find("GameOverPanel");
        if (existing != null) DestroyImmediate(existing);

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("Canvas 없음"); return; }

        // GameOverPanel 없으면 새로 생성
        GameObject panel = GameObject.Find("GameOverPanel");
        if (panel == null)
        {
            panel = new GameObject("GameOverPanel");
            panel.transform.SetParent(canvas.transform, false);
            Image img = panel.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.6f);
        }

        // 전체화면 stretch
        RectTransform panelRect = panel.GetComponent<RectTransform>() ?? panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // TitleText
        SetupText(panel, "TitleText", "GAME OVER", 72, new Vector2(0, 120));

        // RestartButton
        Button restartBtn = SetupButton(panel, "RestartButton", "다시 시작", new Vector2(0, 0));

        // QuitButton
        Button quitBtn = SetupButton(panel, "QuitButton", "종료", new Vector2(0, -100));

        // GameOverUI 스크립트 연결
        GameOverUI ui = panel.GetComponent<GameOverUI>() ?? panel.AddComponent<GameOverUI>();

        // SerializedObject로 필드 자동 할당
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("restartButton").objectReferenceValue = restartBtn;
        so.FindProperty("quitButton").objectReferenceValue = quitBtn;
        so.ApplyModifiedProperties();

        panel.SetActive(true); // 씬에선 켜둬야 Awake 실행됨
        EditorUtility.SetDirty(panel);
        Debug.Log("GameOverPanel 자동 세팅 완료!");
        Selection.activeGameObject = panel;
    }

    static void SetupText(GameObject parent, string name, string content, float fontSize, Vector2 pos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);
        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>() ?? obj.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        RectTransform r = obj.GetComponent<RectTransform>() ?? obj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0.5f);
        r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = new Vector2(600, 100);
    }

    static Button SetupButton(GameObject parent, string name, string label, Vector2 pos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);

        if (obj.GetComponent<RectTransform>() == null) obj.AddComponent<RectTransform>();
        RectTransform r = obj.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0.5f);
        r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = new Vector2(300, 70);

        if (obj.GetComponent<Image>() == null) obj.AddComponent<Image>();
        obj.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        if (obj.GetComponent<Button>() == null) obj.AddComponent<Button>();
        Button btn = obj.GetComponent<Button>();

        Transform textChild = obj.transform.Find("Text");
        GameObject textObj = textChild != null ? textChild.gameObject : new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);

        if (textObj.GetComponent<RectTransform>() == null) textObj.AddComponent<RectTransform>();
        RectTransform tr = textObj.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;

        if (textObj.GetComponent<TextMeshProUGUI>() == null) textObj.AddComponent<TextMeshProUGUI>();
        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;

        return btn;
    }
}
