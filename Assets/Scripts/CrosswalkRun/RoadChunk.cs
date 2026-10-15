using UnityEngine;

namespace CrosswalkRun
{
    /// <summary>
    /// 도로 청크 프리팹의 루트에 부착되는 컴포넌트입니다.
    /// 청크의 Z축 길이를 자동으로 계산하거나 인스펙터 수치를 사용하여,
    /// 크기가 다른 여러 프리팹들이 빈틈없이 정확히 맞물려 생성되도록 지원합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoadChunk : MonoBehaviour
    {
        [Tooltip("청크의 실제 Z축 길이 (0이면 자식 오브젝트들의 바운드를 기반으로 자동 계산)")]
        [SerializeField] private float customLength = 0f;

        private float cachedLength = -1f;

        public float ChunkLength
        {
            get
            {
                if (customLength > 0f) return customLength;
                if (cachedLength < 0f) cachedLength = CalculateBoundsLength();
                return cachedLength;
            }
        }

        public void SetCustomLength(float length)
        {
            customLength = length;
            cachedLength = length;
        }

        private float CalculateBoundsLength()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 17f; // 기본 추정치

            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            foreach (var r in renderers)
            {
                // 횡단보도 줄무늬나 작은 장식물 제외하고 바닥 메쉬 기준으로 측정
                if (r.gameObject.name.StartsWith("Stripe")) continue;

                Bounds b = r.bounds;
                if (b.min.z < minZ) minZ = b.min.z;
                if (b.max.z > maxZ) maxZ = b.max.z;
            }

            if (minZ >= maxZ) return 17f;
            return maxZ - minZ;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Vector3 center = transform.position + new Vector3(0, 0, ChunkLength * 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(36f, 1f, ChunkLength));
        }
    }
}
