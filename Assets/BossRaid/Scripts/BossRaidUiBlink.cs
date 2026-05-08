using UnityEngine;
using UnityEngine.UI;

namespace BossRaid
{
    public sealed class BossRaidUiBlink : MonoBehaviour
    {
        public float intervalSeconds = 0.9f;
        public float onAlpha = 1f;
        public float offAlpha = 0.25f;
        public bool smooth;
        public float startOffsetSeconds;

        private CanvasGroup canvasGroup;
        private Graphic graphic;
        private Color baseColor;
        private float baseAlpha = 1f;
        private float timer;
        private bool baseCaptured;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                graphic = GetComponent<Graphic>();
            }

            CaptureBase();
            timer = startOffsetSeconds;
        }

        private void OnEnable()
        {
            timer = startOffsetSeconds;
        }

        private void OnDisable()
        {
            if (!baseCaptured)
            {
                return;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = baseAlpha;
            }
            else if (graphic != null)
            {
                graphic.color = baseColor;
            }
        }

        private void CaptureBase()
        {
            if (baseCaptured)
            {
                return;
            }

            if (canvasGroup != null)
            {
                baseAlpha = canvasGroup.alpha;
                baseCaptured = true;
            }
            else if (graphic != null)
            {
                baseColor = graphic.color;
                baseCaptured = true;
            }
        }

        private void Update()
        {
            if (canvasGroup == null && graphic == null)
            {
                return;
            }

            CaptureBase();
            timer += Time.deltaTime;
            var period = Mathf.Max(0.05f, intervalSeconds);
            float multiplier;
            if (smooth)
            {
                var phase = (Mathf.Sin(timer * Mathf.PI * 2f / period) + 1f) * 0.5f;
                multiplier = Mathf.Lerp(offAlpha, onAlpha, phase);
            }
            else
            {
                var step = (timer % period) / period;
                multiplier = step < 0.5f ? onAlpha : offAlpha;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = baseAlpha * multiplier;
            }
            else if (graphic != null)
            {
                var color = baseColor;
                color.a = baseColor.a * multiplier;
                graphic.color = color;
            }
        }
    }
}
