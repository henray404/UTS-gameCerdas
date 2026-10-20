using UnityEngine;

namespace SneakHouse
{
    [DisallowMultipleComponent]
    public class FootstepSurface : MonoBehaviour
    {
        public AudioClip[] clips = new AudioClip[0];

        public static AudioClip ClipAt(Vector3 position, int step, AudioClip fallback)
        {
            if (Physics.Raycast(position + Vector3.up * .25f, Vector3.down, out RaycastHit hit,
                .85f, 1 << 8, QueryTriggerInteraction.Ignore))
            {
                FootstepSurface surface = hit.collider.GetComponent<FootstepSurface>();
                if (surface != null && surface.clips.Length > 0)
                    return surface.clips[step % surface.clips.Length] ?? fallback;
            }
            return fallback;
        }
    }
}
