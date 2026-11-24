using UnityEngine;

namespace Fighter403.Player
{
    internal sealed class RootDeathPixels : MonoBehaviour
    {
        private SpriteRenderer display;
        private float age, duration;

        internal void Play(SpriteRenderer renderer, float seconds)
        {
            display = renderer;
            duration = seconds;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= duration)
            {
                Destroy(gameObject);
                return;
            }
            display.sprite = RootBoss.DeathFrame(age / duration);
        }
    }
}
