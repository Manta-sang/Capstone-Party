using Unity.Netcode.Components;
using UnityEngine;

// 기존 NetworkTransform을 상속받아서 권한(Authority) 설정만 바꿉니다.
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    // "서버만 권한을 가질 것인가요?" 묻는 함수
    protected override bool OnIsServerAuthoritative()
    {
        // "아니요(false)! 각자 자기 캐릭터(클라이언트)가 직접 위치를 전송하게 해주세요!"
        return false;
    }
}