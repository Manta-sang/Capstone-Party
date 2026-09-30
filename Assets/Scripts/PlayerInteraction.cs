using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    // 다른 플레이어와 부딪혔을 때 자동으로 실행되는 물리 충돌 함수
    void OnCollisionEnter(Collision collision)
    {
        // 만약 부딪힌 상대방의 태그(Tag)가 "Player"라면?
        if (collision.gameObject.CompareTag("Player"))
        {
            // 상대방 캐릭터에 붙어있는 Rigidbody(물리) 컴포넌트를 가져옵니다.
            Rigidbody otherRb = collision.gameObject.GetComponent<Rigidbody>();

            if (otherRb != null)
            {
                // 상대방을 내 위치에서 바깥쪽으로 밀어내는 방향 계산
                Vector3 pushDirection = collision.transform.position - transform.position;
                pushDirection.y = 0; // 위아래로 뜨는 것 방지

                float pushPower = 10f; // 튕겨나가는 힘 (원하는 만큼 조절 가능)

                // 상대방에게 순간적인 충격 힘(Impulse) 가하기
                otherRb.AddForce(pushDirection.normalized * pushPower, ForceMode.Impulse);
            }
        }
    }
}