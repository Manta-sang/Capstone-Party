using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// 게임 시작 시 Unity Gaming Services를 초기화하고 익명 로그인합니다.
/// </summary>
public class UgsBootstrap : MonoBehaviour
{
    /// <summary>
    /// Unity Services 초기화와 익명 로그인을 한 번 실행합니다.
    /// </summary>
    // 타이틀과 방 화면이 같은 인증 작업을 기다리며 실패 시 다시 시도한다.
    private static System.Threading.Tasks.Task pending;
    public static System.Threading.Tasks.Task EnsureLogin()
    {
        if (pending == null || pending.IsCompleted) pending = Login();
        return pending;
    }
    private static async System.Threading.Tasks.Task Login()
    {
        // Unity Dashboard에 연결된 프로젝트의 서비스를 초기화합니다.
        if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();

        // 아직 로그인하지 않았다면 익명 계정으로 로그인합니다.
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

    }
    private async void Start()
    {
        try
        {
            await EnsureLogin();
            Debug.Log($"[UGS] 로그인 완료 / Player ID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception exception)
        {
            // 오류는 Console에만 기록하고 Editor를 중단시키지 않습니다.
            Debug.LogError($"[UGS] 초기화 또는 로그인 실패: {exception.Message}");
        }
    }
}