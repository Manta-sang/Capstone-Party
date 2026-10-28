using System.Collections.Generic;
using UnityEngine;

namespace CrosswalkRun
{
    // 통과 가능한 판정 영역입니다. 물리 충돌이나 플레이어 생명 상태를 변경하지 않습니다.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SlipperyZone : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float acceleration = 2.5f;
        private BoxCollider area;
        private static readonly HashSet<SlipperyZone> activeZones = new HashSet<SlipperyZone>();

        // 도메인 재로드를 꺼 둔 에디터에서도 이전 실행의 구역 목록을 남기지 않습니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => activeZones.Clear();

        // 교체한 외형에 Collider가 있더라도 발을 가로막지 않도록 Trigger로만 사용합니다.
        private void Awake()
        {
            area = GetComponent<BoxCollider>();
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.isTrigger = true;
        }

        // 구역은 도로와 함께 생성·삭제되며 활성 구역만 이동 판정에 참여합니다.
        private void OnEnable() => activeZones.Add(this);
        private void OnDisable() => activeZones.Remove(this);

        // 호스트와 공유한 설정을 생성 인스턴스에 적용합니다. 프리팹 원본은 바꾸지 않습니다.
        public void Configure(float value) => acceleration = Mathf.Max(0.1f, value);

        // 매 프레임 발 위치를 확인하여 점프·퇴장·구역 삭제 후 미끄러짐이 남지 않게 합니다.
        // Trigger 콜백의 도착 순서에 의존하지 않고 BoxCollider의 실제 영역을 검사합니다.
        public static bool TryGetAcceleration(Vector3 feet, out float value)
        {
            value = float.MaxValue;
            bool found = false;
            foreach (var zone in activeZones)
            {
                if (zone == null || zone.area == null || !zone.area.enabled) continue;
                if ((zone.area.ClosestPoint(feet) - feet).sqrMagnitude > 0.000001f) continue;
                value = Mathf.Min(value, zone.acceleration);
                found = true;
            }
            return found;
        }
    }
}
