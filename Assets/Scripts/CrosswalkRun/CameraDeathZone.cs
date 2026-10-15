using UnityEngine;

namespace CrosswalkRun
{
    /// <summary>
    /// 오토스크롤 카메라 후방에 배치되어, 화면 밖으로 뒤처진 플레이어를 감지하고 탈락시키는 데스존입니다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CameraDeathZone : MonoBehaviour
    {
        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            EliminateTarget(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            EliminateTarget(collision.gameObject);
        }

        private void EliminateTarget(GameObject target)
        {
            IKillable killable = target.GetComponent<IKillable>() ?? target.GetComponentInParent<IKillable>();
            if (killable != null && !killable.IsDead)
            {
                killable.Kill("Camera Screen Boundary");
                return;
            }

            // IKillable이 없는 Player 태그 오브젝트 대응
            if (target.CompareTag("Player"))
            {
                Debug.Log($"[CameraDeathZone] Player 태그 오브젝트 화면 이탈 감지: {target.name}");
                target.SetActive(false);
            }
        }
    }
}
