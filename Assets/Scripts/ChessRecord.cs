using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ChessRecord : MonoBehaviour
{
    [System.Serializable]
    public class RecordEntry
    {
        public int MoveNumber;      // 第几回合
        public string RedMove;      // 红方走法
        public string BlackMove;    // 黑方走法（可能为空）
    }

    [Header("棋谱 UI")]
    public Transform Content;           // ScrollView 的 Content
    public GameObject RecordTextPrefab; // 棋谱文字预制体
    public bool autoScrollEnabled = true;

    [Header("显示样式")]
    public float rowHeight = 68f;
    public float rowSpacing = 10f;
    public float rowLeft = 12.00005f;
    public float rowRight = 10.12165f;
    public float textLeft = 7.99999f;
    public float textRight = 7.75660f;
    public float textTop = -8f;
    public float textBottom = -10f;
    public int recordFontSize = 22;
    public Color oddRowColor = new Color(0.16f, 0.09f, 0.035f, 0.78f);
    public Color evenRowColor = new Color(0.22f, 0.13f, 0.055f, 0.78f);
    public Color textColor = new Color(0.96f, 0.82f, 0.50f, 1f);
    public Color shadowColor = new Color(0f, 0f, 0f, 0.65f);

    private readonly List<RecordEntry> records = new List<RecordEntry>();
    private int currentMoveNumber = 1;

    public void AddMove(ChessPiece.Side side, string notation)
    {
        if (side == ChessPiece.Side.Red)
        {
            RecordEntry entry = new RecordEntry
            {
                MoveNumber = currentMoveNumber,
                RedMove = notation,
                BlackMove = ""
            };
            records.Add(entry);
            CreateRecordRow(entry);
        }
        else
        {
            if (records.Count > 0)
            {
                RecordEntry lastEntry = records[records.Count - 1];
                lastEntry.BlackMove = notation;
                UpdateLastRecordText(lastEntry);
            }

            currentMoveNumber++;
        }

        if (autoScrollEnabled)
            ScrollToBottom();
    }

    public void RemoveLastMove(ChessPiece.Side pieceSide)
    {
        if (records.Count == 0)
            return;

        RecordEntry lastEntry = records[records.Count - 1];

        if (pieceSide == ChessPiece.Side.Black)
        {
            lastEntry.BlackMove = "";
            UpdateLastRecordText(lastEntry);
        }
        else
        {
            records.RemoveAt(records.Count - 1);

            if (Content != null && Content.childCount > 0)
                Destroy(Content.GetChild(Content.childCount - 1).gameObject);

            currentMoveNumber = Mathf.Max(1, currentMoveNumber - 1);
        }

        UpdateContentHeight();
    }

    public void ClearRecords()
    {
        records.Clear();
        currentMoveNumber = 1;

        if (Content == null)
            return;

        for (int i = Content.childCount - 1; i >= 0; i--)
            Destroy(Content.GetChild(i).gameObject);

        RectTransform contentRt = Content.GetComponent<RectTransform>();
        if (contentRt != null)
            contentRt.anchoredPosition = Vector2.zero;
    }

    void CreateRecordRow(RecordEntry entry)
    {
        if (Content == null || RecordTextPrefab == null)
            return;

        GameObject row = new GameObject($"RecordRow_{entry.MoveNumber:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.transform.SetParent(Content, false);

        RectTransform rowRt = row.GetComponent<RectTransform>();
        ApplyRowRect(rowRt, records.Count - 1);

        Image rowImage = row.GetComponent<Image>();
        rowImage.color = (entry.MoveNumber % 2 == 0) ? evenRowColor : oddRowColor;
        rowImage.raycastTarget = false;

        GameObject textObj = Instantiate(RecordTextPrefab, row.transform);
        textObj.name = "RecordText";

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        if (textRt != null)
        {
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.offsetMin = new Vector2(textLeft, textBottom);
            textRt.offsetMax = new Vector2(-textRight, -textTop);
        }

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        ConfigureRecordText(text);
        if (text != null)
            text.text = FormatRecord(entry);

        UpdateContentHeight();
    }

    void ConfigureRecordText(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.richText = true;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.fontSize = recordFontSize;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.Left;
        text.characterSpacing = 1f;
        text.lineSpacing = 14f;
        text.raycastTarget = false;

        Shadow shadow = text.GetComponent<Shadow>();
        if (shadow == null)
            shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = shadowColor;
        shadow.effectDistance = new Vector2(1.2f, -1.2f);
    }

    void UpdateLastRecordText(RecordEntry entry)
    {
        TextMeshProUGUI text = GetLastRecordText();
        if (text != null)
        {
            ConfigureRecordText(text);
            text.text = FormatRecord(entry);
        }
    }

    TextMeshProUGUI GetLastRecordText()
    {
        if (Content == null || Content.childCount == 0)
            return null;

        Transform lastRow = Content.GetChild(Content.childCount - 1);
        TextMeshProUGUI text = lastRow.GetComponent<TextMeshProUGUI>();
        if (text != null)
            return text;

        return lastRow.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    string FormatRecord(RecordEntry entry)
    {
        string blackMove = string.IsNullOrEmpty(entry.BlackMove) ? "--" : entry.BlackMove;
        string blackColor = string.IsNullOrEmpty(entry.BlackMove) ? "#8E7A5A" : "#E7D7B0";

        return $"<color=#D9B35E>{entry.MoveNumber:00}</color>  " +
               $"<color=#C43126>红</color> <color=#FFE3A0>{entry.RedMove}</color>\n" +
               $"    <color={blackColor}>黑</color> <color=#F1D28A>{blackMove}</color>";
    }

    void ApplyRowRect(RectTransform rowRt, int index)
    {
        if (rowRt == null)
            return;

        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.offsetMin = new Vector2(rowLeft, -rowHeight);
        rowRt.offsetMax = new Vector2(-rowRight, 0f);
        rowRt.anchoredPosition = new Vector2(0f, -index * (rowHeight + rowSpacing));
        rowRt.sizeDelta = new Vector2(rowRt.sizeDelta.x, rowHeight);
    }

    void UpdateContentHeight()
    {
        if (Content == null)
            return;

        float height = records.Count * (rowHeight + rowSpacing) + 10f;
        RectTransform rt = Content.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);

        for (int i = 0; i < Content.childCount; i++)
        {
            RectTransform childRt = Content.GetChild(i).GetComponent<RectTransform>();
            if (childRt == null)
                continue;

            ApplyRowRect(childRt, i);
        }
    }

    void ScrollToBottom()
    {
        Invoke(nameof(DoScrollToBottom), 0.05f);
    }

    void DoScrollToBottom()
    {
        if (Content == null || Content.parent == null)
            return;

        RectTransform contentRt = Content.GetComponent<RectTransform>();
        RectTransform viewportRt = Content.parent.GetComponent<RectTransform>();
        if (contentRt == null || viewportRt == null)
            return;

        float contentHeight = contentRt.sizeDelta.y;
        float viewportHeight = viewportRt.sizeDelta.y;

        if (contentHeight > viewportHeight)
            contentRt.anchoredPosition = new Vector2(0f, contentHeight - viewportHeight);
    }
}
