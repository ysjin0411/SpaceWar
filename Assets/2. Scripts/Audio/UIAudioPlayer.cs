using UnityEngine;

// 기존 호출 코드를 유지하기 위한 UI 효과음 종류.
public enum UISound
{
    Typing,
    LoginSuccess,
    LoginFailure,
    ButtonClick
}

// 기존 UI 호출을 새로운 공용 재생기로 전달하는 어댑터.
public sealed class UIAudioPlayer : MonoBehaviour
{
    /// <summary>기존 MainMenu·설정 코드의 효과음 요청을 공용 재생기로 전달합니다.</summary>
    public static void Play(UISound sound)
    {
        // 기존 호출 위치를 바꾸지 않고 동일한 종류의 사운드를 선택한다.
        switch (sound)
        {
            case UISound.Typing:
                GameAudio.Play(GameSound.UiTyping);
                break;
            case UISound.LoginSuccess:
                GameAudio.Play(GameSound.UiLoginSuccess);
                break;
            case UISound.LoginFailure:
                GameAudio.Play(GameSound.UiLoginFailure);
                break;
            case UISound.ButtonClick:
                GameAudio.Play(GameSound.UiButtonClick);
                break;
            default:
                // 잘못된 식별자로 호출한 경우 원인을 알린다.
                Debug.LogError("지원하지 않는 UI 효과음: " + sound);
                break;
        }
    }
}
