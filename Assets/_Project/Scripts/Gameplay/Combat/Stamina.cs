using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>Stamina pool for dodging and blocking. Regenerates after a short delay.</summary>
    [DisallowMultipleComponent]
    public sealed class Stamina : MonoBehaviour
    {
        public float Max { get; private set; } = 100f;
        public float Current { get; private set; } = 100f;
        public bool HasAny => Current > 0.01f;

        float regenPerSecond = 35f;
        float regenDelay = 0.7f;
        float lastUseTime = float.NegativeInfinity;

        public void Initialize(float max, float regenPerSecond, float regenDelay)
        {
            Max = Current = max;
            this.regenPerSecond = regenPerSecond;
            this.regenDelay = regenDelay;
        }

        /// <summary>Spends stamina if there's enough; otherwise spends nothing and returns false.</summary>
        public bool TryConsume(float amount)
        {
            if (amount <= 0f) return true;
            if (Current < amount) return false;
            Spend(amount);
            return true;
        }

        /// <summary>Spends up to the amount available (lenient: lets a dodge happen on low stamina).</summary>
        public void Spend(float amount)
        {
            Current = Mathf.Max(0f, Current - amount);
            lastUseTime = Time.time;
        }

        public void Refill() => Current = Max;

        /// <summary>Changes max stamina/regen (levels, gear) while keeping the current fill percentage.</summary>
        public void SetMax(float max, float regenPerSecond)
        {
            Current = Max > 0f ? Current / Max * max : max;
            Max = max;
            this.regenPerSecond = regenPerSecond;
        }

        public void Restore(float amount)
        {
            if (amount > 0f) Current = Mathf.Min(Max, Current + amount);
        }

        void Update()
        {
            if (Current < Max && Time.time - lastUseTime > regenDelay)
                Current = Mathf.Min(Max, Current + regenPerSecond * Time.deltaTime);
        }
    }
}
