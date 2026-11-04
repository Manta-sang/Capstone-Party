using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PartyTitle : MonoBehaviour
{
    [SerializeField] private TMP_InputField nickname;
    [SerializeField] private TMP_Text message;
    [SerializeField] private TMP_Text settingsText;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private string nextScene = "Lobby";
    private bool starting;

    // 한글 8자도 랭킹의 FixedString32Bytes 안에 들어간다.
    public static string Nickname => PlayerPrefs.GetString("Party.Nickname", "플레이어");
    public static bool ValidName(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value ?? "", @"^[가-힣a-zA-Z0-9_]{1,8}$");

    private void Start()
    {
        nickname.text = PlayerPrefs.GetString("Party.Nickname", "");
        AudioListener.volume = PlayerPrefs.GetFloat("Party.Volume", 1f);
        if (PlayerPrefs.HasKey("Party.Fullscreen"))
            Screen.fullScreen = PlayerPrefs.GetInt("Party.Fullscreen") == 1;
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // 인증과 닉네임 확인이 끝나야 방 화면으로 이동한다.
    public async void EnterLobby()
    {
        if (starting) return;
        string value = nickname.text.Trim();
        if (!ValidName(value)) { message.text = "한글, 영문, 숫자, 밑줄로 1~8자를 입력하세요."; return; }
        starting = true;
        message.text = "로그인 중...";
        try
        {
            await UgsBootstrap.EnsureLogin();
            if (!this) return;
            PlayerPrefs.SetString("Party.Nickname", value);
            PlayerPrefs.Save();
            SceneManager.LoadScene(nextScene);
        }
        catch (Exception) { if (this) message.text = "로그인 실패. 연결을 확인하고 다시 시도하세요."; }
        finally { starting = false; }
    }

    // 설정 패널은 에디터에서 배치를 조정할 수 있다.
    public void ToggleSettings() { if (settingsPanel == null) return; settingsPanel.SetActive(!settingsPanel.activeSelf); RefreshSettings(); }
    public void VolumeUp() => ChangeVolume(0.1f);
    public void VolumeDown() => ChangeVolume(-0.1f);
    private void ChangeVolume(float delta)
    {
        AudioListener.volume = Mathf.Clamp01(AudioListener.volume + delta);
        PlayerPrefs.SetFloat("Party.Volume", AudioListener.volume);
        PlayerPrefs.Save();
        RefreshSettings();
    }
    public void ToggleFullscreen()
    {
        bool enabled = PlayerPrefs.GetInt("Party.Fullscreen", Screen.fullScreen ? 1 : 0) == 0;
        Screen.fullScreen = enabled;
        PlayerPrefs.SetInt("Party.Fullscreen", enabled ? 1 : 0);
        PlayerPrefs.Save();
        RefreshSettings();
    }
    private void RefreshSettings() { if(settingsText != null) settingsText.text = $"전체 음량 {Mathf.RoundToInt(AudioListener.volume * 100)}%  ·  전체화면 {(PlayerPrefs.GetInt("Party.Fullscreen", Screen.fullScreen ? 1 : 0) == 1 ? "켜짐" : "꺼짐")}"; }
    public void QuitGame() => Application.Quit();
}
