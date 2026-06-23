using System.Collections;
using UnityEngine;

namespace SimCore
{
    public class CrashHandler : MonoBehaviour
    {
        [Header("Crash")]
        public float fadeDuration = 1.2f;
        public float holdDuration = 0.6f;

        [Header("Nice landing")]
        public float niceLandingHold = 2.0f;
        public float niceLandingFade = 0.5f;

        private GUIStyle crashStyle;
        private GUIStyle niceStyle;
        private Texture2D overlayTexture;

        private float overlayAlpha     = 0f;
        private float niceLandingAlpha = 0f;
        private bool  showCrashText    = false;
        private bool  isCrashing       = false;

        void Start()
        {
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
            if (isCrashing) return;

            if (!result.success)
                StartCoroutine(CrashSequence());
            else
                StartCoroutine(NiceLandingSequence());
        }

        IEnumerator CrashSequence()
        {
            isCrashing    = true;
            showCrashText = true;

            // Freeze physics so helicopter doesn't tumble during fade
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
            {
                overlayAlpha = t / fadeDuration;
                yield return null;
            }
            overlayAlpha = 1f;

            yield return new WaitForSeconds(holdDuration);

            // Find driver fresh — avoids stale reference from Start()
            var driver = GetComponent<SimulatorDriver>();
            if (driver == null) driver = FindObjectOfType<SimulatorDriver>();

            if (rb != null) rb.isKinematic = false;
            driver?.ResetToSpawnPoint();
            showCrashText = false;

            for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
            {
                overlayAlpha = 1f - t / fadeDuration;
                yield return null;
            }
            overlayAlpha = 0f;
            isCrashing   = false;
        }

        IEnumerator NiceLandingSequence()
        {
            niceLandingAlpha = 1f;
            yield return new WaitForSeconds(niceLandingHold);

            for (float t = 0f; t < niceLandingFade; t += Time.deltaTime)
            {
                niceLandingAlpha = 1f - t / niceLandingFade;
                yield return null;
            }
            niceLandingAlpha = 0f;
        }

        void OnGUI()
        {
            if (overlayAlpha > 0f)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, overlayAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlayTexture);
                GUI.color = prev;

                if (showCrashText)
                {
                    if (crashStyle == null)
                        crashStyle = MakeCenteredStyle(80, FontStyle.Bold);

                    crashStyle.normal.textColor = new Color(1f, 1f, 1f, Mathf.Clamp01(overlayAlpha * 2f));
                    GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "CRASH!", crashStyle);
                }
            }

            if (niceLandingAlpha > 0f)
            {
                if (niceStyle == null)
                    niceStyle = MakeCenteredStyle(60, FontStyle.Bold);

                niceStyle.normal.textColor = new Color(0.4f, 1f, 0.4f, niceLandingAlpha);
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "Nice landing!", niceStyle);
            }
        }

        private static GUIStyle MakeCenteredStyle(int size, FontStyle style) =>
            new GUIStyle(GUI.skin.label)
            {
                fontSize  = size,
                fontStyle = style,
                alignment = TextAnchor.MiddleCenter
            };

        void OnDestroy()
        {
            if (overlayTexture != null) Destroy(overlayTexture);
        }
    }
}
