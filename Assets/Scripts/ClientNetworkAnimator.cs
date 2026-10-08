using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent]
public class ClientNetworkAnimator : NetworkAnimator
{
    // 클라이언트가 직접 애니메이션을 동기화하도록 권한을 줍니다.
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}