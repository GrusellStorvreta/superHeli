using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SimCore
{
    [RequireComponent(typeof(CanvasGroup))]
    public class SceneFader : MonoBehaviour
    {
        public float fadeDuration = 0.8f;

        private CanvasGroup _group;

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha          = 0f;
            _group.blocksRaycasts = false;
            _group.interactable   = false;

            var img = GetComponent<Image>();
            if (img != null) img.color = Color.black;
        }

        public IEnumerator FadeToBlack()   => Fade(0f, 1f);
        public IEnumerator FadeFromBlack() => Fade(1f, 0f);

        IEnumerator Fade(float from, float to)
        {
            _group.alpha          = from;
            _group.blocksRaycasts = true;
            float t = 0f;
            while (t < fadeDuration)
            {
                t           += Time.deltaTime;
                _group.alpha = Mathf.Lerp(from, to, t / fadeDuration);
                yield return null;
            }
            _group.alpha          = to;
            _group.blocksRaycasts = to > 0f;
        }
    }
}
