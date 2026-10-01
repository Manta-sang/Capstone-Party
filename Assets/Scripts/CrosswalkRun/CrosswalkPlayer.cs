using System;
using UnityEngine;
using UnityEngine.Events;

namespace CrosswalkRun
{
    /// <summary>
    /// 다른 브랜치나 프로젝트에서 가져온 플레이어 프리팹에 붙이는 미니게임 연동 전용 컴포넌트입니다.
    /// 기존 이동 스크립트(PlayerMovement 등)를 전혀 수정할 필요 없이,
    /// 이 컴포넌트만 프리팹에 추가하면 게임 매니저 자동 등록 및 자동차/데스존 탈락 처리가 완벽히 연동됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CrosswalkPlayer : MonoBehaviour, IKillable
    {
        [Header("플레이어 정보")]
        [SerializeField] private string playerName = "Player";

        [Header("탈락 시 비활성화할 컴포넌트 (선택 사항)")]
        [Tooltip("사망 시 멈추게 할 이동 스크립트(PlayerMovement 등)를 드래그해 넣으세요. (비워두면 자동으로 이동 스크립트를 찾아 끕니다)")]
        [SerializeField] private Behaviour[] componentsToDisableOnDeath;

        [Header("탈락 연출 이벤트")]
        [Tooltip("사망 시 사운드 재생, 파티클 생성, 애니메이션 트리거 등을 인스펙터에서 자유롭게 연결할 수 있습니다.")]
        public UnityEvent<string> onEliminated;

        private bool isDead = false;

        public bool IsDead => isDead;
        public string PlayerName => playerName;

        private void Start()
        {
            // 게임 매니저에 이 플레이어를 참가자로 자동 등록
            if (CrosswalkGameManager.Instance != null)
            {
                CrosswalkGameManager.Instance.RegisterPlayer(gameObject);
            }
            else
            {
                Debug.LogWarning("[CrosswalkPlayer] CrosswalkGameManager 인스턴스를 찾지 못했습니다. 매니저가 씬에 배치되어 있는지 확인하세요.");
            }
        }

        /// <summary>
        /// IKillable 구현: 자동차나 카메라 데스존에 닿았을 때 호출됩니다.
        /// </summary>
        public void Kill(string reason = "")
        {
            if (isDead) return;

            isDead = true;
            Debug.Log($"<color=orange>[CrosswalkPlayer]</color> {playerName} ({gameObject.name}) 탈락! 사유: {reason}");

            // 1. 이동 및 조작 스크립트 비활성화
            DisablePlayerControls();

            // 2. 물리적 튕김 연출 (Rigidbody가 있는 경우)
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.freezeRotation = false;
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * 15f, ForceMode.Impulse);
            }

            // 3. 인스펙터 이벤트 호출 (이펙트, 사운드 등)
            onEliminated?.Invoke(reason);

            // 4. 게임 매니저에 탈락 통보 (생존자 카운트 차감 및 최후 1인 판정)
            if (CrosswalkGameManager.Instance != null)
            {
                CrosswalkGameManager.Instance.PlayerDied(gameObject, reason);
            }
        }

        private void DisablePlayerControls()
        {
            // 인스펙터에 지정된 컴포넌트가 있다면 그것들을 끔
            if (componentsToDisableOnDeath != null && componentsToDisableOnDeath.Length > 0)
            {
                foreach (var comp in componentsToDisableOnDeath)
                {
                    if (comp != null) comp.enabled = false;
                }
            }
            else
            {
                // 비워두었을 경우, 이름에 movement, controller, input 등이 들어간 조작 스크립트 자동 비활성화
                MonoBehaviour[] allScripts = GetComponents<MonoBehaviour>();
                foreach (var s in allScripts)
                {
                    if (s == this) continue;
                    string sName = s.GetType().Name.ToLower();
                    if (sName.Contains("movement") || sName.Contains("controller") || sName.Contains("input"))
                    {
                        s.enabled = false;
                    }
                }
            }
        }
    }
}
