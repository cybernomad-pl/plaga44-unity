using UnityEngine;

namespace Plaga44.AI
{
    /// <summary>
    /// Marker spawn pointa. Umieszczaj w scenie gdzie mają się pojawiać wrogowie.
    /// Automatycznie wykrywany przez EnemySpawner.
    /// Tworzone przez: CYBERNOMAD > NPC > Spawn Point
    /// </summary>
    public class SpawnPointMarker : MonoBehaviour
    {
        [Tooltip("Opcjonalna nazwa punktu (dla debugowania)")]
        public string pointLabel = "Spawn Point";

        [Tooltip("Jesli true -- ten punkt jest aktywny i moze byc uzyty przez spawner")]
        public bool isActive = true;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = isActive
                ? new Color(1f, 0.2f, 0.2f, 0.85f)
                : new Color(0.5f, 0.5f, 0.5f, 0.4f);

            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawRay(transform.position, transform.forward * 0.7f);

            // Strzalka kierunku spawna
            Vector3 right = transform.position + transform.forward * 0.7f;
            Gizmos.DrawLine(right, right - transform.forward * 0.2f + transform.right * 0.15f);
            Gizmos.DrawLine(right, right - transform.forward * 0.2f - transform.right * 0.15f);

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.5f,
                string.IsNullOrEmpty(pointLabel) ? gameObject.name : pointLabel
            );
        }
#endif
    }
}
