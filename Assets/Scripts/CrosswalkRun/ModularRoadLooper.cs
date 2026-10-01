using System.Collections.Generic;
using UnityEngine;

namespace CrosswalkRun
{
    /// <summary>
    /// 씬의 하이어라키에 실제 배치된 도로/인도 모듈들을 카메라 진행에 맞춰
    /// 앞쪽으로 이동(재활용)시켜 무한 루프 주행을 구현하는 스크립트입니다.
    /// 에디터에서 눈으로 보며 직접 수정한 차선의 설정(속도, 머티리얼 등)이 그대로 유지됩니다.
    /// </summary>
    public class ModularRoadLooper : MonoBehaviour
    {
        [Header("카메라 추적")]
        [SerializeField] private Transform targetCamera;

        [Header("루프 설정")]
        [Tooltip("카메라 뒤로 완전히 지나갔다고 판단하는 거리 (미터)")]
        [SerializeField] private float recycleDistanceBehind = 35f;

        public class TrackSegment
        {
            public Transform Transform;
            public float Length;
        }

        private readonly List<TrackSegment> segments = new List<TrackSegment>();
        private float totalTrackLength = 0f;

        private void Awake()
        {
            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }

            CollectSegments();
        }

        public void CollectSegments()
        {
            segments.Clear();
            totalTrackLength = 0f;

            // Map_Environment의 직계 자식 모듈들을 Z축 순서대로 수집
            List<Transform> children = new List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                children.Add(child);
            }

            // Z 오름차순 정렬
            children.Sort((a, b) => a.position.z.CompareTo(b.position.z));

            for (int i = 0; i < children.Count; i++)
            {
                Transform child = children[i];
                float length = 17f; // 기본 추정 길이

                // 다음 세그먼트와의 거리로 실제 길이 계산
                if (i < children.Count - 1)
                {
                    length = children[i + 1].position.z - child.position.z;
                }
                else if (segments.Count > 0)
                {
                    length = segments[segments.Count - 1].Length;
                }

                segments.Add(new TrackSegment { Transform = child, Length = length });
                totalTrackLength += length;
            }
        }

        private void Update()
        {
            if (targetCamera == null || segments.Count == 0) return;

            // 가장 뒤쪽에 있는 세그먼트가 카메라 뒤 일정 거리를 벗어났는지 확인
            TrackSegment rearSegment = segments[0];
            float rearEndZ = rearSegment.Transform.position.z + rearSegment.Length;

            if (targetCamera.position.z - rearEndZ > recycleDistanceBehind)
            {
                // 가장 앞쪽에 있는 세그먼트의 뒤로 재배치 (루핑)
                TrackSegment frontSegment = segments[segments.Count - 1];
                float newZ = frontSegment.Transform.position.z + frontSegment.Length;

                Vector3 pos = rearSegment.Transform.position;
                pos.z = newZ;
                rearSegment.Transform.position = pos;

                // 큐/리스트 순서 갱신 (맨 앞 요소를 맨 뒤로 이동)
                segments.RemoveAt(0);
                segments.Add(rearSegment);
            }
        }
    }
}
