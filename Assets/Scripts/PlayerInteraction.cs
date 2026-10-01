using UnityEngine;
using Unity.Netcode; // ★ 네트워크 패키지 추가

public class PlayerInteraction : NetworkBehaviour
{
    [Header("밀치기 설정")]
    public float pushPower = 20f;
    public float pushRadius = 2f;

    void Update()
    {
        // 내 캐릭터가 아니면 Shift 입력을 무시합니다.
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            PushOthers();
        }
    }

    void PushOthers()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, pushRadius);

        foreach (Collider hit in hitColliders)
        {
            if (hit.CompareTag("Player") && hit.gameObject != this.gameObject)
            {
                // 부딪힌 상대방의 네트워크 신분증(NetworkObject)을 가져옵니다.
                NetworkObject targetNetObj = hit.GetComponent<NetworkObject>();

                if (targetNetObj != null)
                {
                    Vector3 pushDirection = hit.transform.position - transform.position;
                    pushDirection.y = 0;

                    // ★ 내 컴퓨터에서 직접 상대를 튕겨내지 않고, 서버에게 부탁합니다.
                    // "이 ID(OwnerClientId)를 가진 유저를 이 방향으로 밀쳐주세요!"
                    ApplyKnockbackServerRpc(targetNetObj.OwnerClientId, pushDirection.normalized, pushPower);
                }
            }
        }
    }

    // ★ [ServerRpc]: 클라이언트(나)가 서버에게 요청하는 함수
    [ServerRpc]
    void ApplyKnockbackServerRpc(ulong targetClientId, Vector3 direction, float power)
    {
        // 서버가 요청을 받으면, 오직 '맞은 당사자'의 컴퓨터로만 명령을 보냅니다.
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetClientId } }
        };
        ApplyKnockbackClientRpc(direction, power, clientRpcParams);
    }

    // ★ [ClientRpc]: 서버가 클라이언트(맞은 사람)에게 실행하라고 명령하는 함수
    [ClientRpc]
    void ApplyKnockbackClientRpc(Vector3 direction, float power, ClientRpcParams rpcParams = default)
    {
        // 이 코드는 맞은 사람의 컴퓨터 안에서만 실행되어 넉백 효과를 줍니다!
        PlayerMovement myMovement = GetComponent<PlayerMovement>();
        Rigidbody myRb = GetComponent<Rigidbody>();
        Animator myAnim = GetComponent<Animator>();

        if (myMovement != null)
        {
            myMovement.isKnockedBack = true;
            myMovement.Invoke("ResetKnockback", 0.5f);
        }

        if (myAnim != null) myAnim.Play("Idle");

        if (myRb != null)
        {
            myRb.velocity = new Vector3(0f, myRb.velocity.y, 0f);
            Vector3 knockbackVelocity = direction * power;
            myRb.velocity = new Vector3(knockbackVelocity.x, myRb.velocity.y, knockbackVelocity.z);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, pushRadius);
    }
}