using System.Collections;
using UnityEngine;

namespace BK.Kit
{
    // Reuse the authored mesh and materials, without keeping its collision in the board.
    public sealed class BlockBreakEffect : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var start = transform.position;
            var scale = transform.localScale;
            var rotation = transform.localRotation;
            float elapsed = 0;
            while (elapsed < .24f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / .24f);
                transform.position = start + Vector3.up * (.65f * t);
                transform.localRotation = rotation * Quaternion.Euler(0, t * 35, 0);
                transform.localScale = scale * (1 + .25f * Mathf.Sin(t * Mathf.PI)) * (1 - t * t);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
