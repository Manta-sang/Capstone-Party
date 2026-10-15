using UnityEngine;

namespace CrosswalkRun
{
    /// <summary>
    /// 자동차 피격 또는 화면 밖 이탈 시 호출되는 범용 탈락 인터페이스입니다.
    /// 다른 브랜치에서 가져올 플레이어 스크립트에 이 인터페이스만 붙여주면 즉시 호환됩니다.
    /// </summary>
    public interface IKillable
    {
        /// <summary>
        /// 탈락 처리 함수
        /// </summary>
        /// <param name="reason">탈락 사유 (예: "Car Hit", "Camera Out")</param>
        void Kill(string reason = "");

        /// <summary>
        /// 이미 사망/탈락 상태인지 여부
        /// </summary>
        bool IsDead { get; }
    }
}
