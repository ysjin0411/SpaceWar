// 저장된 사용자 목록의 한 행에 필요한 정보.
public class SavedProfileInfo
{
    // .json 확장자를 제외한 파일명.
    public string UserId;

    // 해당 파일에 저장된 유저 레벨.
    public int UserLevel;

    // 파일에서 읽은 정보를 목록 표시용으로 보관한다.
    public SavedProfileInfo(string userId, int userLevel)
    {
        UserId = userId;
        UserLevel = userLevel;
    }
}