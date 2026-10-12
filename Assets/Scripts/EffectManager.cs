using DG.Tweening;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    [Header("UI效果预制体")]
    public GameObject eatEffectPrefab;       // UI吃子爆炸
    public GameObject moveTrailPrefab;       // UI走子拖尾
    public GameObject jiangJunEffectPrefab;  // UI将军闪光

    [Header("父容器（Canvas下的空物体）")]
    public RectTransform effectsContainer;

    [Header("UI动态效果")]
    public float jiangJunShakeDuration = 0.5f;
    public float jiangJunShakeStrength = 20f;

    void Awake()
    {
        Instance = this;
    }

    // 播放吃子爆炸效果
    public void PlayEatEffect(Vector2 anchoredPos)
    {
        if (eatEffectPrefab == null) return;

        GameObject effect = Instantiate(eatEffectPrefab, effectsContainer);
        RectTransform rt = effect.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;

        // 确保在最上层
        rt.SetAsLastSibling();
    }

    // 播放走子拖尾效果
    public void PlayMoveTrail(Vector2 startAnchoredPos, Vector2 endAnchoredPos)
    {
        if (moveTrailPrefab == null) return;

        // 创建多个残影点模拟拖尾
        int segmentCount = 5;
        for (int i = 0; i < segmentCount; i++)
        {
            float t = (float)i / segmentCount;
            Vector2 pos = Vector2.Lerp(startAnchoredPos, endAnchoredPos, t);

            GameObject dot = Instantiate(moveTrailPrefab, effectsContainer);
            RectTransform rt = dot.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.SetAsLastSibling();

            // 渐隐动画
            CanvasGroup cg = dot.GetComponent<CanvasGroup>();
            if (cg == null) cg = dot.AddComponent<CanvasGroup>();

            cg.alpha = 1 - t * 0.5f; // 前面亮，后面淡
            cg.DOFade(0, 0.3f).SetDelay(0.1f).OnComplete(() => Destroy(dot));
        }
    }

    // 播放将军闪光效果
    public void PlayJiangJunEffect(Vector2 anchoredPos)
    {
        if (jiangJunEffectPrefab == null) return;

        GameObject effect = Instantiate(jiangJunEffectPrefab, effectsContainer);
        RectTransform rt = effect.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.SetAsLastSibling(); // 确保在最上层

        UIFlashEffect flash = effect.GetComponent<UIFlashEffect>();
        if (flash != null) flash.enabled = true;
    }

    // 将军提示UI震动
    public void ShakeJiangJunText(RectTransform target)
    {
        target.DOShakeAnchorPos(jiangJunShakeDuration, jiangJunShakeStrength, 10, 90, false, true);
    }
}