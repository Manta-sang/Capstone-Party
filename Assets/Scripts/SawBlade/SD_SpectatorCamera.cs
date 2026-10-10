using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내 플레이어가 죽으면 살아있는 다른 플레이어의 시점으로 카메라를 옮기고,
/// 이전/다음 키로 생존자들을 돌려가며 볼 수 있게 한다. (톱날 피하기 미니게임 전용)
///
/// 카메라 오브젝트에 SD_CameraFollow와 함께 붙인다.
/// 멀티에서는 내 플레이어가 생성될 때 SetLocalPlayer()로 지정하면 된다.
/// </summary>
[RequireComponent(typeof(SD_CameraFollow))]
public class SD_SpectatorCamera : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("생존자 목록을 가진 게임 매니저. 비워두면 씬에서 자동으로 찾는다")]
    [SerializeField] private SD_GameManager gameManager;
    [Tooltip("내 플레이어. 멀티에서는 코드(SetLocalPlayer)로 내 플레이어를 지정한다")]
    [SerializeField] private SD_PlayerLife localPlayer;

    [Header("조작")]
    [SerializeField] private KeyCode previousKey = KeyCode.Q;
    [SerializeField] private KeyCode nextKey = KeyCode.E;

    private SD_CameraFollow cameraFollow;
    private SD_PlayerLife spectated; // 지금 보고 있는 플레이어
    private bool isSpectating;

    private void Awake()
    {
        cameraFollow = GetComponent<SD_CameraFollow>();
        if (gameManager == null) gameManager = FindObjectOfType<SD_GameManager>();
    }

    /// <summary>내 플레이어를 지정한다 (멀티에서 내 플레이어가 생성될 때 호출)</summary>
    public void SetLocalPlayer(SD_PlayerLife player)
    {
        localPlayer = player;
    }

    private void Update()
    {
        if (localPlayer == null || gameManager == null) return;

        // 살아있으면 내 캐릭터를 따라간다 (재시작으로 되살아난 경우 관전을 끝내고 돌아온다)
        if (!localPlayer.IsDead)
        {
            if (isSpectating)
            {
                isSpectating = false;
                spectated = null;
                cameraFollow.SetTarget(localPlayer.transform);
            }
            return;
        }

        isSpectating = true;

        IReadOnlyList<SD_PlayerLife> alive = gameManager.AlivePlayers;
        if (alive.Count == 0) return;

        // 보고 있던 플레이어가 없거나 죽었으면 첫 번째 생존자로
        int index = IndexOf(alive, spectated);
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

    private void Spectate(SD_PlayerLife player)
    {
        spectated = player;
        cameraFollow.SetTarget(player.transform, false); // 순간이동하지 않고 부드럽게 옮겨간다
    }

    private static int IndexOf(IReadOnlyList<SD_PlayerLife> list, SD_PlayerLife player)
    {
        if (player == null) return -1;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == player) return i;
        }
        return -1;
    }
}
