using System;
using UnityEngine;

namespace Beast.Core
{
    /// <summary>Saves and restores this object's position and rotation.</summary>
    [DisallowMultipleComponent]
    public sealed class SaveableTransform : MonoBehaviour, ISaveable
    {
        [SerializeField, Tooltip("Unique, stable ID used in save files (e.g. \"player\").")]
        string saveId;

        public string SaveId => saveId;

        [Serializable]
        struct State
        {
            public Vector3 position;
            public Quaternion rotation;
        }

        void OnEnable()
        {
            if (string.IsNullOrEmpty(saveId))
            {
                Debug.LogWarning($"[Save] {name} has no SaveId and won't be saved.", this);
                return;
            }
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        void OnDisable()
        {
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        public string CaptureState() =>
            JsonUtility.ToJson(new State { position = transform.position, rotation = transform.rotation });

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);

            // A CharacterController overrides transform changes unless it's disabled while teleporting.
            bool toggleController = TryGetComponent(out CharacterController controller) && controller.enabled;
            if (toggleController) controller.enabled = false;
            transform.SetPositionAndRotation(state.position, state.rotation);
            if (toggleController) controller.enabled = true;
        }
    }
}
