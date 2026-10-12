using UnityEngine;
using UnityEngine.UI;

public class UIEffect : MonoBehaviour
{
    public float duration = 0.5f;
    public float maxScale = 1.5f;

    void Start()
    {
        // 缩放动画
        RectTransform rt = GetComponent<RectTransform>();
        rt.localScale = Vector3.zero;

        // 用DOTween或Coroutine动画
        StartCoroutine(Animate());
    }

    System.Collections.IEnumerator Animate()
    {
        RectTransform rt = GetComponent<RectTransform>();
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        float elapsed = 0;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * maxScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 缩放：先放大后缩小
            float scaleT = Mathf.Sin(t * Mathf.PI); // 0→1→0
            rt.localScale = Vector3.Lerp(startScale, endScale, scaleT);

            // 淡出
            cg.alpha = 1 - t;

            yield return null;
        }

        Destroy(gameObject);
    }
}