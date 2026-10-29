using UnityEngine;
using TMPro;
using Unity.Netcode;

public class QteRunner : NetworkBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI qteText; // QteBox 자식의 QteText 연결

    [Header("Movement & QTE Settings")]
    public float baseMoveSpeed = 3f;     // 기본 상시 전진 속도
    public float qteBoostDistance = 1.5f; // QTE 한 세트 성공 시 앞으로 튀어나가는 거리
    public int sequenceLength = 4;        // 한 번에 풀어야 할 QTE 키 개수 (W, A, S, D 중 4개)

    private string currentSequence = "";  // 생성된 랜덤 QTE 조합
    private int currentIndex = 0;         // 현재 맞춰야 할 글자 인덱스

    // 사용할 수 있는 QTE 키 풀(Pool)
    private readonly char[] availableKeys = { 'W', 'A', 'S', 'D' };

    public override void OnNetworkSpawn()
    {
        // 내 캐릭터(Local Client) 화면에만 QTE UI가 작동하도록 설정
        if (!IsOwner)
        {
            if (qteText != null) qteText.transform.root.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        // 게임 시작 시 첫 번째 랜덤 QTE 생성
        GenerateNewQte();
    }

    void Update()
    {
        if (!IsOwner) return;

        // 1. 플레이어 키보드 입력 및 판정 체크
        CheckQteInput();

        // 2. 상시 기본 전진 (원하시면 0으로 두고 오직 QTE 성공으로만 전진하게 바꿀 수 있습니다)
        transform.Translate(Vector3.forward * baseMoveSpeed * Time.deltaTime);
    }

    // [핵심 1] 랜덤 키 조합 생성 알고리즘
    void GenerateNewQte()
    {
        currentSequence = "";
        currentIndex = 0;

        // 지정된 길이(예: 4)만큼 W, A, S, D 중 랜덤으로 조합 생성
        for (int i = 0; i < sequenceLength; i++)
        {
            int randomIndex = Random.Range(0, availableKeys.Length);
            currentSequence += availableKeys[randomIndex];
        }

        // UI 텍스트에 반영 (보기 좋게 글자 사이에 공백을 줄 수도 있습니다)
        if (qteText != null)
        {
            // 예: "WASD" -> "W A S D" 형태로 보기 좋게 출력
            qteText.text = string.Join(" ", currentSequence.ToCharArray());
        }
    }

    // [핵심 2] 키 입력 판정 및 순서 검증 알고리즘
    void CheckQteInput()
    {
        if (string.IsNullOrEmpty(currentSequence)) return;

        // 현재 맞춰야 할 타겟 문자
        char targetChar = currentSequence[currentIndex];
        bool inputMatched = false;

        // 입력 감지
        if (targetChar == 'W' && Input.GetKeyDown(KeyCode.W)) inputMatched = true;
        else if (targetChar == 'A' && Input.GetKeyDown(KeyCode.A)) inputMatched = true;
        else if (targetChar == 'S' && Input.GetKeyDown(KeyCode.S)) inputMatched = true;
        else if (targetChar == 'D' && Input.GetKeyDown(KeyCode.D)) inputMatched = true;

        // 잘못된 키를 눌렀을 때의 페널티 (원하신다면 여기에 스턴이나 속도 감소 추가 가능)
        // 여기서는 오직 정답을 순서대로 누르는 것에 집중합니다.

        if (inputMatched)
        {
            currentIndex++; // 다음 글자로 진행

            // [핵심 3] 한 세트(예: 4글자)를 모두 맞췄을 때!
            if (currentIndex >= currentSequence.Length)
            {
                // 서버에 전진 성공 신호 전송
                SubmitQteSuccessServerRpc();

                // 즉시 새로운 랜덤 QTE 생성
                GenerateNewQte();
            }
        }
    }

    // [핵심 4] 서버와 동기화하여 앞으로 전진시키는 ServerRpc
    [ServerRpc]
    void SubmitQteSuccessServerRpc()
    {
        // 서버 권한으로 캐릭터를 앞으로 툭 튀어나오게 전진시킴
        transform.position += Vector3.forward * qteBoostDistance;
    }
}