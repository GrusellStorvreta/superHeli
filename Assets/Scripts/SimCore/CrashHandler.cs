using System.Collections;
using UnityEngine;

namespace SimCore
{
    public class CrashHandler : MonoBehaviour
    {
        public float fadeDuration = 1.2f;
        public float holdDuration = 0.6f;

        private SimulatorDriver driver;
        private GUIStyle crashStyle;
        private Texture2D overlayTexture;

        private float overlayAlpha = 0f;
        private bool  showCrashText = false;
        private bool  isCrashing = false;

        void Start()
        {
            driver = GetComponent<SimulatorDriver>();

            var checker = GetComponent<LandingChecker>();
            if (checker != null)
                checker.OnLanding += OnLanding;
        }

        void OnEnable()
        {
            overlayTexture = new Texture2D(1, 1);
            overlayTexture.SetPixel(0, 0, Color.black);
            overlayTexture.Apply();
        }

        void OnLanding(LandingResult result)
        {
            if (!result.success && !isCrashing)
                StartCoroutine(CrashSequence());
        }

        IEnumerator CrashSequence()
        {
            isCrashing    = true;
            showCrashText = true;

            // Fade in to black
            for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
            {
                overlayAlpha = t / fadeDuration;
                yield return null;
            }
            overlayAlpha = 1f;

            yield return new WaitForSeconds(holdDuration);

            driver?.ResetToSpawnPoint();
            showCrashText = false;

            // Fade out
            for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
            {
                overlayAlpha = 1f - t / fadeDuration;
                yield return null;
            }
            overlayAlpha = 0f;
            isCrashing   = false;
        }

        void OnGUI()
        {
            if (overlayAlpha <= 0f) return;

            // Full-screen dark overlay
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, overlayAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlayTexture);
            GUI.color = prev;

            if (!showCrashText) return;

            if (crashStyle == null)
            {
                crashStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 80,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            crashStyle.normal.textColor = new Color(1f, 1f, 1f, Mathf.Clamp01(overlayAlpha * 2f));
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "CRASH!", crashStyle);
        }

        void OnDestroy()
        {
            if (overlayTexture != null) Destroy(overlayTexture);
        }
    }
}
