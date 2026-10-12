using UnityEngine;

public class UITrailEffect : MonoBehaviour
{
    public float duration = 0.3f;

    public void Play(Vector2 startPos, Vector2 endPos)
    {
        RectTransform rt = GetComponent<RectTransform>();
        rt.anchoredPosition = startPos;

        StartCoroutine(MoveAnimate(rt, startPos, endPos));
    }

    System.Collections.IEnumerator MoveAnimate(RectTransform rt, Vector2 start, Vector2 end)
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            rt.anchoredPosition = Vector2.Lerp(start, end, t);
            cg.alpha = 1 - t; // µ­³ö

            yield return null;
        }

        Destroy(gameObject);
    }
}