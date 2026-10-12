using UnityEngine;

public class UIFlashEffect : MonoBehaviour
{
    public float flashInterval = 0.1f;
    public int flashCount = 6;

    void Start()
    {
        StartCoroutine(FlashAnimate());
    }

    System.Collections.IEnumerator FlashAnimate()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        for (int i = 0; i < flashCount; i++)
        {
            cg.alpha = 1;
            yield return new WaitForSeconds(flashInterval);
            cg.alpha = 0;
            yield return new WaitForSeconds(flashInterval);
        }

        Destroy(gameObject);
    }
}