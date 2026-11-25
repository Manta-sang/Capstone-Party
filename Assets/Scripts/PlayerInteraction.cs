using UnityEngine;
using Unity.Netcode;

public class PlayerInteraction : NetworkBehaviour
{
    [Header("밀치기 설정")]
    public float pushPower = 20f;
    public float pushRadius = 2f;
    public float pushCooldown = 3f;
    public float stunDuration = 0.8f; // ★ 기절(넉백 및 조작 불가) 시간 추가

    [Header("밀치기 성공 연출")]
    // 배열 설정이 없는 기존 프리팹의 단일 이펙트 참조도 계속 사용할 수 있습니다.
    [SerializeField, HideInInspector] private GameObject pushEffectPrefab;
    [Tooltip("비어 있지 않은 항목 중 하나를 서버가 같은 확률로 선택합니다.")]
    [SerializeField] private GameObject[] pushEffectPrefabs = new GameObject[0];
    [SerializeField, Min(0.01f)] private float pushEffectScale = 2f;
    [SerializeField] private Vector3 pushEffectOffset = new Vector3(0f, 1f, 0f);
    [SerializeField, Min(0.1f)] private float pushEffectLifetime = 3f;
    [SerializeField] private AudioClip pushHitClip;
    [SerializeField, Range(0f, 1f)] private float pushHitVolume = 0.6f;

    private float lastPushTime = -100f;
    // The local HUD reads the same clock and duration used by the actual input gate.
    public float PushCooldownProgress => pushCooldown <= 0f ? 1f : Mathf.Clamp01((Time.time - lastPushTime) / pushCooldown);

    void Update()
    {
        // 횡단보도 게임은 이동과 회피에 집중하도록 밀치기 입력을 차단합니다.
        if (gameObject.scene.name == "CrosswalkRun") return;
        if (!IsOwner || GameStartPresentation.BlocksGameplayInput) return;

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
        // 서버에서도 같은 씬 규칙을 검사하여 넉백과 타격 연출 요청을 차단합니다.
        if (gameObject.scene.name == "CrosswalkRun") return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObj))
        {
            PlayerInteraction targetInteraction = targetObj.GetComponent<PlayerInteraction>();
            if (targetInteraction != null)
            {
                targetInteraction.ApplyKnockbackClientRpc(direction, power);
                // 서버가 넉백을 전달한 시점의 위치를 공유하여 모든 참가자에게 같은 타격을 보여줍니다.
                PlayPushFeedbackClientRpc(targetObj.transform.position + pushEffectOffset, SelectPushEffectIndex());
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

    /// <summary>빈 항목을 제외하고 동일 확률로 선택합니다. -1이면 기존 단일 설정을 사용합니다.</summary>
    private int SelectPushEffectIndex()
    {
        int selected = -1;
        int validCount = 0;
        if (pushEffectPrefabs == null) return selected;

        // 유효한 항목 수에 따른 선택으로 임시 목록 할당 없이 균등하게 추첨합니다.
        for (int i = 0; i < pushEffectPrefabs.Length; i++)
        {
            if (pushEffectPrefabs[i] == null) continue;
            validCount++;
            if (Random.Range(0, validCount) == 0) selected = i;
        }
        return selected;
    }

    /// <summary>
    /// 서버가 확인한 밀치기에 한해 각 클라이언트에서 시각 효과와 3D 타격음을 재생합니다.
    /// 연출용 오브젝트는 네트워크 스폰 없이 로컬 생성하고 재생 후 정리합니다.
    /// </summary>
    [ClientRpc]
    private void PlayPushFeedbackClientRpc(Vector3 position, int effectIndex)
    {
        // 서버에서 고른 배열 인덱스를 공유하여 참가자마다 다른 이펙트가 나오지 않게 합니다.
        GameObject selectedEffect = effectIndex < 0 ? pushEffectPrefab
            : pushEffectPrefabs != null && effectIndex < pushEffectPrefabs.Length ? pushEffectPrefabs[effectIndex] : null;
        if (selectedEffect != null)
        {
            GameObject effect = Instantiate(selectedEffect, position, selectedEffect.transform.rotation);
            effect.transform.localScale *= pushEffectScale;
            Destroy(effect, pushEffectLifetime);
        }

        if (pushHitClip != null)
        {
            // 캐릭터가 이동하거나 사라져도 타격 위치에서 소리를 끝까지 재생합니다.
            GameObject sound = new GameObject("Push Hit Sound");
            sound.transform.position = position;
            AudioSource source = sound.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = 25f;
            source.dopplerLevel = 0f;
            source.PlayOneShot(pushHitClip, pushHitVolume);
            Destroy(sound, pushHitClip.length + 0.1f);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, pushRadius);
    }
}
