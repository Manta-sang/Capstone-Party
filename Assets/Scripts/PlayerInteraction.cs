using UnityEngine;
using Unity.Netcode;

public class PlayerInteraction : NetworkBehaviour
{
    [Header("밀치기 설정")]
    public float pushPower = 20f;
    public float pushRadius = 2f;
    public float pushCooldown = 3f;
    public float stunDuration = 0.8f; // ★ 기절(넉백 및 조작 불가) 시간 추가

    private float lastPushTime = -100f;

    void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            if (Time.time >= lastPushTime + pushCooldown)
            {
                PushOthers();
                lastPushTime = Time.time;
                Debug.Log("밀치기 얍! (쿨타임 도는 중...)");
            }
            else
            {
                float remainTime = (lastPushTime + pushCooldown) - Time.time;
                Debug.Log($"아직 밀칠 수 없습니다! 남은 쿨타임: {remainTime:F1}초");
            }
        }
    }

    void PushOthers()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, pushRadius);

        foreach (Collider hit in hitColliders)
        {
            if (hit.CompareTag("Player") && hit.gameObject != this.gameObject)
            {
                NetworkObject targetNetObj = hit.GetComponent<NetworkObject>();

                if (targetNetObj != null)
                {
                    Vector3 pushDirection = hit.transform.position - transform.position;
                    pushDirection.y = 0;

                    ApplyKnockbackServerRpc(targetNetObj.NetworkObjectId, pushDirection.normalized, pushPower);
                }
            }
        }
    }

    [ServerRpc]
    void ApplyKnockbackServerRpc(ulong targetNetworkObjectId, Vector3 direction, float power)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObj))
        {
            PlayerInteraction targetInteraction = targetObj.GetComponent<PlayerInteraction>();
            if (targetInteraction != null)
            {
                targetInteraction.ApplyKnockbackClientRpc(direction, power);
            }
        }
    }

    [ClientRpc]
    void ApplyKnockbackClientRpc(Vector3 direction, float power)
    {
        if (!IsOwner) return;

        PlayerMovement myMovement = GetComponent<PlayerMovement>();
        Rigidbody myRb = GetComponent<Rigidbody>();
        Animator myAnim = GetComponent<Animator>();

        if (myMovement != null)
        {
            myMovement.isKnockedBack = true;
            // ★ 기존 0.5f에서 인스펙터에 설정한 stunDuration(0.8초)로 변경
            myMovement.Invoke("ResetKnockback", stunDuration);
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