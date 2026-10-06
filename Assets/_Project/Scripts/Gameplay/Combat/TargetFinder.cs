using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>Picks the best target in a cone, favouring close targets near the aim direction.</summary>
    public static class TargetFinder
    {
        static readonly Collider[] buffer = new Collider[32];

        public static Combatant FindBest(Vector3 origin, Vector3 aimDirection, float range, float maxAngle, int mask, Combatant exclude)
        {
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude < 0.0001f) return null;

            int count = Physics.OverlapSphereNonAlloc(origin, range, buffer, mask, QueryTriggerInteraction.Collide);
            Combatant best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var candidate = buffer[i].GetComponentInParent<Combatant>();
                if (candidate == null || candidate == exclude || candidate.IsDead) continue;

                Vector3 toCandidate = candidate.transform.position - origin;
                toCandidate.y = 0f;
                float angle = Vector3.Angle(aimDirection, toCandidate);
                if (angle > maxAngle) continue;

                // Distance matters most; angle breaks ties between similar distances.
                float score = toCandidate.magnitude * (1f + angle / 90f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }
    }
}
