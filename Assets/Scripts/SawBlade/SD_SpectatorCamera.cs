using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내 플레이어가 죽으면(또는 제거되면) 살아있는 다른 플레이어의 시점으로 카메라를 옮기고,
/// 이전/다음 키로 생존자들을 돌려가며 볼 수 있게 한다. (톱날 피하기 미니게임 전용)
///
/// 생존자 목록은 이 컴퓨터에 있는 플레이어들(SD_PlayerDeath.All)의 죽음 상태로 직접 만든다.
/// 죽은 플레이어의 오브젝트는 몇 초 뒤 제거되는데, 제거된 뒤에도 관전은 계속된다.
/// 다시 시작해서 내 플레이어가 새로 생성되면 SD_GameManager가 SetLocalPlayer()를 다시 불러 원래대로 돌아온다.
///
/// 카메라 오브젝트에 SD_CameraFollow와 함께 붙인다.
/// </summary>
[RequireComponent(typeof(SD_CameraFollow))]
public class SD_SpectatorCamera : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("내 플레이어. 멀티에서는 코드(SetLocalPlayer)로 지정된다")]
    [SerializeField] private SD_PlayerDeath localPlayer;

    [Header("조작")]
    [SerializeField] private KeyCode previousKey = KeyCode.Q;
    [SerializeField] private KeyCode nextKey = KeyCode.E;

    private SD_CameraFollow cameraFollow;
    private SD_PlayerDeath spectated; // 지금 보고 있는 플레이어
    private bool isSpectating;
    private bool hasLocalPlayer;     // 내 플레이어가 한 번이라도 지정됐는지 (제거된 경우와 아직 없는 경우를 구분)
    private readonly List<SD_PlayerDeath> alive = new List<SD_PlayerDeath>();

    private void Awake()
    {
        cameraFollow = GetComponent<SD_CameraFollow>();
        hasLocalPlayer = localPlayer != null;
    }

    /// <summary>내 플레이어를 지정한다 (내 플레이어가 생성될 때마다 호출된다)</summary>
    public void SetLocalPlayer(SD_PlayerDeath player)
    {
        localPlayer = player;
        hasLocalPlayer = player != null;

        // 새로 생성된 내 플레이어로 돌아온다 (카메라 대상은 SD_GameManager가 지정한다)
        isSpectating = false;
        spectated = null;
    }

    private void Update()
    {
        if (!hasLocalPlayer) return;

        // 내 플레이어가 죽었거나, 죽어서 이미 제거된 경우 (제거되면 localPlayer가 null이 된다)
        bool dead = localPlayer == null || localPlayer.IsDead;

        if (!dead)
        {
            // 살아있으면 내 캐릭터를 따라간다 (재시작으로 되살아난 경우 관전을 끝내고 돌아온다)
            if (isSpectating)
            {
                isSpectating = false;
                spectated = null;
                cameraFollow.SetTarget(localPlayer.transform);
            }
            return;
        }

        isSpectating = true;

        CollectAlivePlayers();
        if (alive.Count == 0) return;

        // 보고 있던 플레이어가 없거나 죽었으면 첫 번째 생존자로
        int index = alive.IndexOf(spectated);
        if (index < 0)
        {
            Spectate(alive[0]);
            return;
        }

        if (Input.GetKeyDown(nextKey))
        {
            Spectate(alive[(index + 1) % alive.Count]);
        }
        else if (Input.GetKeyDown(previousKey))
        {
            Spectate(alive[(index - 1 + alive.Count) % alive.Count]);
        }
    }

    // 나를 뺀, 살아있는 플레이어들
    private void CollectAlivePlayers()
    {
        alive.Clear();
        foreach (SD_PlayerDeath player in SD_PlayerDeath.All)
        {
            if (player != null && player != localPlayer && !player.IsDead) alive.Add(player);
        }
    }

    private void Spectate(SD_PlayerDeath player)
    {
        spectated = player;
        cameraFollow.SetTarget(player.transform, false); // 순간이동하지 않고 부드럽게 옮겨간다
    }
}