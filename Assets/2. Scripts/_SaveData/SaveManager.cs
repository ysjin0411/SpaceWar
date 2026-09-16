using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Text.RegularExpressions;

/// <summary>
/// ÇÃ·¹ÀÌ¾îÀÇ ¼¼ÀÌºê µ¥ÀÌÅÍ¸¦ ¸Ş¸ğ¸®¿¡¼­ °ü¸®ÇÑ´Ù.
/// Ä³¸¯ÅÍº° ÀåÂø ¹«±â Á¤º¸¸¦ ÀúÀåÇÑ´Ù.
/// ½ÇÁ¦ ÆÄÀÏ ÀúÀå°ú ºÒ·¯¿À±â´Â ÀÌÈÄ ´Ü°è¿¡¼­ Ãß°¡ÇÑ´Ù.
/// </summary>
public class SaveManager : MonoBehaviour
{
    // ÀÎ½ºÆåÅÍ¿¡¼­ ÃÊ±âÈ­ÇÒ ½ºÅ×ÀÌÁö¸¦ ¼±ÅÃÇÏ±â À§ÇÑ Å×½ºÆ®¿ë ¸ñ·Ï
    private enum StageClearRecordResetTarget
    {
        Stage1 = 0,
        Stage2 = 1
    }

    [Header("Å×½ºÆ®¿ë Å¬¸®¾î ±â·Ï ÃÊ±âÈ­")]

    // ContextMenu ½ÇÇà ½Ã ±â·ÏÀ» ÃÊ±âÈ­ÇÒ ½ºÅ×ÀÌÁö
    [SerializeField]
    StageClearRecordResetTarget _resetTargetStage =
        StageClearRecordResetTarget.Stage1;

    // ´Ù¸¥ ¾À¿¡¼­µµ SaveManager¿¡ Á¢±ÙÇÏ±â À§ÇÑ ÇöÀç ÀÎ½ºÅÏ½º
    public static SaveManager Instance { get; private set; }

    // »ç¿ëÀÚº° JSON ÆÄÀÏÀ» ¸ğ¾Æ µÑ Æú´õ ÀÌ¸§.
    const string ProfileFolderName = "Profiles";

    // ÇöÀç ¼¼ÀÌºê°¡ ·Î±×ÀÎÀ¸·Î ¼±ÅÃµÈ »óÅÂÀÎÁö ³ªÅ¸³½´Ù.
    public bool IsLoggedIn { get; private set; }

    // Soldier ÀúÀå µ¥ÀÌÅÍ¸¦ Ã£À» ¶§ »ç¿ëÇÏ´Â °íÁ¤ Ä³¸¯ÅÍ ID
    const string SoldierCharacterId = "soldier";

    // Warrior ÀúÀå µ¥ÀÌÅÍ¸¦ Ã£À» ¶§ »ç¿ëÇÏ´Â °íÁ¤ Ä³¸¯ÅÍ ID
    const string WarriorCharacterId = "warrior";

    // ·Î±×ÀÎÇÑ »ç¿ëÀÚÀÇ ÀúÀå ÆÄÀÏ °æ·Î. ·Î±×ÀÎ Àü¿¡´Â ºñ¾î ÀÖ´Ù.
    string _savePath = string.Empty;

    // ·Î±×ÀÎ ¿Ï·á È­¸é¿¡ Ç¥½ÃÇÒ »ç¿ëÀÚ ID.
    public string CurrentUserId => _playerSaveData.userId;

    // ·Î±×ÀÎ ¿Ï·á È­¸éÀÇ ·¹º§ ¾ÆÀÌÄÜ¿¡ Ç¥½ÃÇÒ »ç¿ëÀÚ ·¹º§.
    public int CurrentUserLevel => _playerSaveData.userLevel;

    [Header("ÇöÀç ÇÃ·¹ÀÌ¾î ¼¼ÀÌºê µ¥ÀÌÅÍ")]
    [SerializeField] PlayerSaveData _playerSaveData = new();

    // ÀúÀå ½Ã ÁøÇà ÁßÀÎ Äù½ºÆ®ÀÇ ¸ñÇ¥¸¦ °Ë»çÇÒ ÀüÃ¼ Äù½ºÆ® ¸ñ·Ï.
    [SerializeField] private QuestDatabase _questDatabase;

    // User ·¹º§º° ÇÊ¿ä °æÇèÄ¡ ¼³Á¤.
    [SerializeField] private UserLevelData _userLevelData;

    // ½Å±Ô Ä³¸¯ÅÍ¿¡°Ô Áö±ŞÇÏ°í ÀåÂøÇÒ ¹«±â Á¤º¸¸¦ °¡Á®¿Â´Ù.
    [SerializeField] private CharacterDatabase _characterDatabase;

    // ÇöÀç User ·¹º§¿¡¼­ ¸ğÀº °æÇèÄ¡.
    public int CurrentUserExp => _playerSaveData.userExp;

    // ·Î±×ÀÎ ¶Ç´Â ÀúÀå ¿Ï·á ÈÄ User Ç¥½Ã Á¤º¸¸¦ °»½ÅÇÏµµ·Ï ¾Ë¸°´Ù.
    public event Action UserStatusChanged;

    /// <summary>
    /// ÇöÀç ÃÑ °ñµå°¡ º¯°æµÆÀ» ¶§ º¯°æµÈ °ñµå·®À» Àü´ŞÇÑ´Ù.
    /// </summary>
    public event Action<int> GoldChanged;

    /// <summary>
    /// JSON¿¡¼­ ºÒ·¯¿Â ÇöÀç ÇÃ·¹ÀÌ¾îÀÇ ÃÑ °ñµå.
    /// ¿ÜºÎ¿¡¼­´Â ÀĞ±â¸¸ ÇÒ ¼ö ÀÖ´Ù.
    /// </summary>
    public int CurrentGold => _playerSaveData.gold;

    // ´Ù¸¥ Å¬·¡½º¿¡´Â ÀĞ±â Àü¿ëÀ¸·Î ÇöÀç ¼¼ÀÌºê µ¥ÀÌÅÍ¸¦ °ø°³ÇÑ´Ù.
    public PlayerSaveData PlayerSaveData => _playerSaveData;

    private void Awake()
    {
        // ±âÁ¸ ¸Å´ÏÀú°¡ ÀÖÀ¸¸é »õ·Î »ı±ä Áßº¹ ¿ÀºêÁ§Æ®¸¦ Á¦°ÅÇÑ´Ù.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // ½ÃÀÛÇÒ ¶§´Â ¾î¶² »ç¿ëÀÚÀÇ ÆÄÀÏµµ ¼±ÅÃÇÏÁö ¾Ê´Â´Ù.
        _savePath = string.Empty;
        IsLoggedIn = false;

        // Inspector¿¡ ³²Àº °ªÀÌ ·Î±×ÀÎ Àü µ¥ÀÌÅÍ·Î »ç¿ëµÇÁö ¾Ê°Ô ÃÊ±âÈ­ÇÑ´Ù.
        _playerSaveData = new PlayerSaveData();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            Application.OpenURL(Application.persistentDataPath);
        }
    }

    private void OnDestroy()
    {
        // ÇöÀç µî·ÏµÈ ÀÎ½ºÅÏ½º ÀÚ½ÅÀÌ ÆÄ±«µÇ´Â °æ¿ì¿¡¸¸ Á¤Àû ÂüÁ¶¸¦ ºñ¿î´Ù.
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ÀÔ·ÂÇÑ ID¸¦ Á¤¸®ÇÏ°í ÆÄÀÏ¸íÀ¸·Î »ç¿ë °¡´ÉÇÑÁö °Ë»çÇÑ´Ù.
    public bool TryValidateUserId(
        string input,
        out string userId,
        out string message)
    {
        userId = string.Empty;
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            message = "ID¸¦ ÀÔ·ÂÇØÁÖ¼¼¿ä.";
            return false;
        }

        // ½Ç¼ö·Î ÀÔ·ÂÇÑ ¾ÕµÚ °ø¹éÀ» Á¦°ÅÇÑ´Ù.
        userId = input.Trim();

        // ¿µ¹® ¼Ò¹®ÀÚ, ¼ıÀÚ, ÇÑ±Û ¿Ï¼ºÇü, ¹ØÁÙ¸¸ 1~10ÀÚ Çã¿ëÇÑ´Ù.
        // ´ë¹®ÀÚ°¡ Æ÷ÇÔµÇ¸é ÀÚµ¿ º¯È¯ÇÏÁö ¾Ê°í ¿äÃ»À» °ÅºÎÇÑ´Ù.
        if (!Regex.IsMatch(userId, @"\A[a-z0-9°¡-ÆR_]{1,10}\z"))
        {
            message = "ID´Â ¿µ¹® ¼Ò¹®ÀÚ, ¼ıÀÚ, ÇÑ±Û, ¹ØÁÙ·Î 1~10ÀÚ ÀÔ·ÂÇØÁÖ¼¼¿ä.";
            return false;
        }

        // Windows¿¡¼­ ÀÏ¹İ ÆÄÀÏ¸íÀ¸·Î »ç¿ëÇÒ ¼ö ¾ø´Â ÀÌ¸§À» Â÷´ÜÇÑ´Ù.
        if (Regex.IsMatch(
            userId,
            @"\A(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            message = "ÀÌ ID´Â »ç¿ëÇÒ ¼ö ¾ø½À´Ï´Ù. ´Ù¸¥ ID¸¦ ÀÔ·ÂÇØÁÖ¼¼¿ä.";
            return false;
        }

        return true;
    }

    // °ËÁõÀ» Åë°úÇÑ ID·Î »ç¿ëÀÚ ÆÄÀÏ °æ·Î¸¦ ¸¸µç´Ù.
    private string GetProfilePath(string input, out string userId)
    {
        if (!TryValidateUserId(input, out userId, out string message))
        {
            throw new ArgumentException(message, nameof(input));
        }

        return Path.Combine(
            Application.persistentDataPath,
            ProfileFolderName,
            userId + ".json");
    }

    // ÇöÀç ·Î±×ÀÎ Áß¿¡´Â ´Ù¸¥ »ç¿ëÀÚ µ¥ÀÌÅÍ·Î ±³Ã¼ÇÏÁö ¸øÇÏ°Ô ÇÑ´Ù.
    private void EnsureLoggedOut()
    {
        if (IsLoggedIn)
        {
            throw new InvalidOperationException(
                "ÀÌ¹Ì ·Î±×ÀÎµÇ¾î ÀÖ½À´Ï´Ù.");
        }
    }

    // ±âÁ¸ ÆÄÀÏÀ» ºÒ·¯¿Â´Ù.
    // ÆÄÀÏÀÌ ¾øÀ» ¶§¸¸ false¸¦ ¹İÈ¯ÇÏ°í, ¼Õ»óÀÌ³ª Á¢±Ù ¿À·ù´Â ¿¹¿Ü·Î Àü´ŞÇÑ´Ù.
    public bool TryLoadUser(string input)
    {
        EnsureLoggedOut();

        string path = GetProfilePath(input, out string userId);
        string json;

        try
        {
            json = File.ReadAllText(path);
        }
        catch (FileNotFoundException)
        {
            // ÇØ´ç IDÀÇ ÆÄÀÏÀÌ ¾øÀ¸¹Ç·Î »ı¼º ¿©ºÎ¸¦ ¹°¾îº¼ ¼ö ÀÖ´Ù.
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            // ¾ÆÁ÷ »ç¿ëÀÚ ÀúÀå Æú´õ°¡ ¾ø´Â Ã¹ ½ÇÇà »óÅÂ´Ù.
            return false;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException(
                "¼¼ÀÌºê ÆÄÀÏÀÌ ºñ¾î ÀÖ½À´Ï´Ù.");
        }

        // ÀĞÀº ÆÄÀÏÀ» ¸ÕÀú º°µµ °´Ã¼·Î º¯È¯ÇÑ´Ù.
        PlayerSaveData loadedData =
            JsonUtility.FromJson<PlayerSaveData>(json);

        if (loadedData == null ||
            string.IsNullOrWhiteSpace(loadedData.userId) ||
            loadedData.userLevel < 1 ||
            loadedData.userExp < 0 ||
            loadedData.characterLoadouts == null)
        {
            throw new InvalidDataException(
                "¼¼ÀÌºê µ¥ÀÌÅÍÀÇ Çü½ÄÀÌ ¿Ã¹Ù¸£Áö ¾Ê½À´Ï´Ù.");
        }

        // ÆÄÀÏ ÀÌ¸§°ú ³»ºÎ »ç¿ëÀÚ Á¤º¸°¡ °°Àº »ç¿ëÀÚ¸¦ °¡¸®Å°´ÂÁö °Ë»çÇÑ´Ù.
        if (!string.Equals(
            loadedData.userId,
            userId,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "ÆÄÀÏ ÀÌ¸§°ú ¼¼ÀÌºêÀÇ »ç¿ëÀÚ ID°¡ ÀÏÄ¡ÇÏÁö ¾Ê½À´Ï´Ù.");
        }

        // ±âÁ¸ ÀúÀå ÄÚµåÀÇ µ¥ÀÌÅÍ º¸Á¤ ±â´ÉÀ» À¯ÁöÇÑ´Ù.
        NormalizeStageClearRecords(loadedData);
        NormalizeAchievementRecords(loadedData);

        foreach (CharacterLoadoutSaveData loadout
                 in loadedData.characterLoadouts)
        {
            if (loadout == null ||
                string.IsNullOrWhiteSpace(loadout.characterId))
            {
                throw new InvalidDataException(
                    "Ä³¸¯ÅÍ ÀúÀå Á¤º¸°¡ ¿Ã¹Ù¸£Áö ¾Ê½À´Ï´Ù.");
            }

            NormalizeLoadout(loadout);
        }

        // ±âÁ¸ ¼¼ÀÌºêÀÇ ÁøÇà Áß Äù½ºÆ®µµ ÇöÀç ¸ñÇ¥ ¼³Á¤À¸·Î ´Ù½Ã °Ë»çÇÑ´Ù.
        // ·Î±×ÀÎ¸¸À¸·Î ¾÷ÀûÃ¢ ¿­±â Á¶°ÇÀ» ´Ş¼º½ÃÅ°Áö´Â ¾Ê´Â´Ù.
        List<QuestSaveData> changedRecords =
            QuestService.FindSatisfiedQuests(
                _questDatabase, loadedData, false);

        if (changedRecords.Count > 0)
        {
            // °»½Å°ú ÀúÀåÀÌ ¼º°øÇÑ µÚ ±âÁ¸ ActivateUser°¡ »ç¿ëÀÚ¸¦ È°¼ºÈ­ÇÑ´Ù.
            WriteWithQuestProgress(path, loadedData, changedRecords);
        }

        // ¸ğµç ÀĞ±â¿Í °Ë»ç°¡ ¼º°øÇÑ µÚ ÇöÀç »ç¿ëÀÚ·Î Àû¿ëÇÑ´Ù.
        ActivateUser(path, loadedData);

        return true;
    }

    // »ı¼º È®ÀÎ Ã¢¿¡¼­ ¿¹¸¦ ¼±ÅÃÇßÀ» ¶§¸¸ È£ÃâÇÑ´Ù.
    public void CreateUser(string input)
    {
        EnsureLoggedOut();

        string path = GetProfilePath(input, out string userId);

        // ´Ù¸¥ »ç¿ëÀÚÀÇ ¸Ş¸ğ¸® µ¥ÀÌÅÍ¸¦ º¹»çÇÏÁö ¾Ê°í »õ °´Ã¼¸¦ ¸¸µç´Ù.
        PlayerSaveData newData = new PlayerSaveData
        {
            userId = userId,
            userLevel = 1,
            userExp = 0
        };

        // ½Å±Ô »ç¿ëÀÚ ÆÄÀÏÀ» ÀúÀåÇÏ±â Àü¿¡ ¸ğµç Ä³¸¯ÅÍÀÇ ½ÃÀÛ Àåºñ¸¦ ÁØºñÇÑ´Ù.
        if (_characterDatabase == null)
        {
            throw new InvalidOperationException(
                "SaveManagerÀÇ Character Database¸¦ ¿¬°áÇØÁÖ¼¼¿ä.");
        }

        HashSet<string> characterIds = new HashSet<string>();

        for (int i = 0; i < _characterDatabase.Count; i++)
        {
            CharacterData character = _characterDatabase.GetCharacterDataAt(i);

            // °°Àº Ä³¸¯ÅÍ ±â·ÏÀÌ Áßº¹ »ı¼ºµÇÁö ¾Êµµ·Ï ¼³Á¤À» °Ë»çÇÑ´Ù.
            if (character == null ||
                string.IsNullOrWhiteSpace(character.Id) ||
                !characterIds.Add(character.Id))
            {
                throw new InvalidOperationException(
                    "CharacterDatabaseÀÇ Ä³¸¯ÅÍ¿Í Áßº¹ ID¸¦ È®ÀÎÇØÁÖ¼¼¿ä.");
            }

            newData.characterLoadouts.Add(CreateInitialLoadout(character));
        }

        // ÀÌ¹Ì °°Àº ÀÌ¸§ÀÇ ÆÄÀÏÀÌ »ı°å´Ù¸é µ¤¾î¾²Áö ¾Ê°í ½ÇÆĞÇÑ´Ù.
        WriteDataFile(path, newData, true);

        // ½ÇÁ¦ ÆÄÀÏ »ı¼ºÀÌ ¼º°øÇÑ °æ¿ì¿¡¸¸ ·Î±×ÀÎ »óÅÂ·Î ¹Ù²Û´Ù.
        ActivateUser(path, newData);
    }

    // °ËÁõÇÏ°Å³ª »ı¼ºÇÑ µ¥ÀÌÅÍ¸¦ ÇöÀç ·Î±×ÀÎ »ç¿ëÀÚ·Î ÁöÁ¤ÇÑ´Ù.
    private void ActivateUser(string path, PlayerSaveData data)
    {
        _savePath = path;
        _playerSaveData = data;
        IsLoggedIn = true;
        // ·Î±×ÀÎÇÑ UserÀÇ Á¤º¸¸¦ È­¸é¿¡ ¹İ¿µÇÑ´Ù.
        NotifyUserStatusChanged();
    }

    // ÀÏ¹İ ÀúÀå¿¡¼­´Â ·¹º§¡¤¹«±â ¼ÒÀ¯¡¤½ºÅ×ÀÌÁö Å¬¸®¾î Á¶°ÇÀ» °Ë»çÇÑ´Ù.
    public void SaveToFile()
    {
        SaveWithQuestProgress(false);
    }

    // NPC¸¦ ÅëÇØ ¾÷ÀûÃ¢ÀÌ ½ÇÁ¦·Î ¿­·ÈÀ» ¶§ È£ÃâÇÑ´Ù.
    public void RecordAchievementWindowOpened()
    {
        SaveWithQuestProgress(true);
    }

    // ÇÃ·¹ÀÌ¾î µ¥ÀÌÅÍ¿Í »õ·Ó°Ô ´Ş¼ºÇÑ Äù½ºÆ® »óÅÂ¸¦ ÇÔ²² ÀúÀåÇÑ´Ù.
    private void SaveWithQuestProgress(bool achievementWindowOpened)
    {
        if (!IsLoggedIn)
        {
            throw new InvalidOperationException(
                "·Î±×ÀÎÇÑ »ç¿ëÀÚ°¡ ¾ø¾î ÀúÀåÇÒ ¼ö ¾ø½À´Ï´Ù.");
        }

        List<QuestSaveData> changedRecords =
            QuestService.FindSatisfiedQuests(
                _questDatabase,
                _playerSaveData,
                achievementWindowOpened);

        WriteWithQuestProgress(_savePath, _playerSaveData, changedRecords);

        // ÀúÀå ¼º°ø ÈÄ¿¡¸¸ Äù½ºÆ® È­¸éÀ» °»½ÅÇÑ´Ù.
        QuestService.NotifyProgressChanges(changedRecords);

        // ÀúÀåµÈ User ·¹º§°ú °æÇèÄ¡¸¦ È­¸é¿¡ ¹İ¿µÇÑ´Ù.
        NotifyUserStatusChanged();
    }

    // ¸ñÇ¥ ´Ş¼ºÀ» ¹İ¿µÇØ¼­ ÀúÀåÇÏ°í, ÀúÀå ½ÇÆĞ ½Ã Äù½ºÆ® »óÅÂ¸¦ º¹±¸ÇÑ´Ù.
    private void WriteWithQuestProgress(
        string path,
        PlayerSaveData saveData,
        List<QuestSaveData> changedRecords)
    {
        foreach (QuestSaveData record in changedRecords)
        {
            record.state = QuestState.ReadyToTurnIn;
        }

        try
        {
            // ±âÁ¸ ÀÓ½Ã ÆÄÀÏ ÀÛ¼º¡¤±³Ã¼ ¹æ½ÄÀ¸·Î ÀúÀåÇÑ´Ù.
            WriteDataFile(path, saveData, false);
        }
        catch
        {
            // °Ë»ç ´ë»óÀº ¸ğµÎ InProgress¿´À¸¹Ç·Î ¿ø·¡ »óÅÂ·Î µÇµ¹¸°´Ù.
            foreach (QuestSaveData record in changedRecords)
            {
                record.state = QuestState.InProgress;
            }

            // ±âÁ¸ È£ÃâÀÚ°¡ ÀúÀå ½ÇÆĞ¸¦ Ã³¸®ÇÏµµ·Ï Àü´ŞÇÑ´Ù.
            throw;
        }
    }

    // JSONÀ» º°µµ ÆÄÀÏ¿¡ ¿Ï¼ºÇÑ µÚ ÃÖÁ¾ °æ·Î¿¡ ¹İ¿µÇÑ´Ù.
    // ¾²´Â µµÁß ½ÇÆĞÇßÀ» ¶§ ±âÁ¸ Á¤»ó ÆÄÀÏÀÌ Àß¸®´Â °ÍÀ» ¹æÁöÇÑ´Ù.
    private void WriteDataFile(
        string path,
        PlayerSaveData data,
        bool createNew)
    {
        // Ã¹ »ç¿ëÀÚ »ı¼º ½Ã ÀúÀå Æú´õ¸¦ ¸¸µç´Ù.
        string directory = Path.Combine(
            Application.persistentDataPath,
            ProfileFolderName);

        Directory.CreateDirectory(directory);

        // ÀÛ¾÷¸¶´Ù ´Ù¸¥ ÀÓ½Ã ÆÄÀÏÀ» »ç¿ëÇÑ´Ù.
        string temporaryPath =
            path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(temporaryPath, json);

            if (createNew)
            {
                // ´ë»ó ÆÄÀÏÀÌ ÀÌ¹Ì ÀÖÀ¸¸é ½ÇÆĞÇÏ¸ç ±âÁ¸ ÆÄÀÏÀ» º¸Á¸ÇÑ´Ù.
                File.Move(temporaryPath, path);
            }
            else if (File.Exists(path))
            {
                // ¿Ï¼ºµÈ ³»¿ëÀ¸·Î ±âÁ¸ ÆÄÀÏÀ» ±³Ã¼ÇÑ´Ù.
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                // ·Î±×ÀÎ ÀÌÈÄ ÆÄÀÏÀÌ »ç¶óÁø °æ¿ì ÇöÀç µ¥ÀÌÅÍ·Î ´Ù½Ã ÀúÀåÇÑ´Ù.
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            // ÃÖÁ¾ °æ·Î¿¡ ¹İ¿µµÇÁö ¸øÇÑ ÀÛ¾÷ ÆÄÀÏ¸¸ Á¤¸®ÇÑ´Ù.
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }



    /// <summary>
    /// Ä³¸¯ÅÍ ID¿¡ ÇØ´çÇÏ´Â ÀåÂø Á¤º¸¸¦ Ã£¾Æ ¹İÈ¯ÇÑ´Ù. ¾ÆÁ÷ ÀåÂø Á¤º¸°¡ ¾ø´Ù¸é »õ·Î ¸¸µé¾î¼­ ¹İÈ¯ÇÑ´Ù.
    /// </summary>
    /// <param name="characterId">ÀåÂø Á¤º¸¸¦ Ã£À» Ä³¸¯ÅÍ ID</param>
    public CharacterLoadoutSaveData GetOrCreateCharacterLoadout(string characterId)
    {
        // ºñ¾î ÀÖ´Â Ä³¸¯ÅÍ ID´Â Á¤»óÀûÀÎ µ¥ÀÌÅÍ°¡ ¾Æ´Ï¹Ç·Î È£Ãâ À§Ä¡¸¦ È®ÀÎÇÒ ¼ö ÀÖµµ·Ï ¿¹¿Ü¸¦ ¹ß»ı½ÃÅ²´Ù.
        if (string.IsNullOrWhiteSpace(characterId))
        {
            throw new ArgumentException("Ä³¸¯ÅÍ ID°¡ ºñ¾î ÀÖ½À´Ï´Ù.", nameof(characterId));
        }

        // ±âÁ¸ Ä³¸¯ÅÍ ÀåÂø Á¤º¸ Áß °°Àº ID°¡ ÀÖ´ÂÁö È®ÀÎÇÑ´Ù.
        for (int i = 0; i < _playerSaveData.characterLoadouts.Count; i++)
        {
            // Ä³¸¯ÅÍº° ÀåÂø Á¤º¸ ¸ñ·Ï¿¡¼­ ÇÏ³ª¾¿ ²¨³½´Ù.
            CharacterLoadoutSaveData loadout = _playerSaveData.characterLoadouts[i];

            // °°Àº Ä³¸¯ÅÍ IDÀÇ ÀåÂø Á¤º¸¸¦ Ã£¾Ò´Ù¸é »õ·Î ¸¸µéÁö ¾Ê°í ±âÁ¸ µ¥ÀÌÅÍ¸¦ ¹İÈ¯ÇÑ´Ù.
            if (loadout.characterId == characterId)
            {
                // ÀÌÀü ¹öÀüÀÇ ÀúÀå µ¥ÀÌÅÍµµ ÇöÀç Àåºñ ÀúÀå Çü½Ä¿¡ ¸Â°Ô º¸Á¤ÇÑ´Ù.
                NormalizeLoadout(loadout);

                // º¸Á¤µÈ Ä³¸¯ÅÍ Àåºñ µ¥ÀÌÅÍ¸¦ ¹İÈ¯ÇÑ´Ù.
                return loadout;
            }
        }

        // ÀúÀå ±â·ÏÀÌ ¾ø´Â Ä³¸¯ÅÍ¸¸ ½ÃÀÛ Àåºñ¿Í ÇÔ²² »õ·Î »ı¼ºÇÑ´Ù.
        if (_characterDatabase == null)
        {
            throw new InvalidOperationException(
                "SaveManagerÀÇ Character Database¸¦ ¿¬°áÇØÁÖ¼¼¿ä.");
        }

        CharacterData character =
            _characterDatabase.GetCharacterData(characterId);

        CharacterLoadoutSaveData newLoadout =
            CreateInitialLoadout(character);

        _playerSaveData.characterLoadouts.Add(newLoadout);

        try
        {
            // ½ÃÀÛ Àåºñ Áö±Ş »óÅÂ¸¦ ÀúÀåÇÑ´Ù.
            SaveToFile();
        }
        catch
        {
            // ÀúÀå¿¡ ½ÇÆĞÇÏ¸é ¸Ş¸ğ¸®¿¡µµ »ı¼º ±â·ÏÀ» ³²±âÁö ¾Ê´Â´Ù.
            _playerSaveData.characterLoadouts.Remove(newLoadout);
            throw;
        }

        return newLoadout;
    }

    /// <summary>
    /// ÇØ´ç Ä³¸¯ÅÍ°¡ ÁöÁ¤ÇÑ ¹«±â¸¦ º¸À¯ÇÏ°í ÀÖ´ÂÁö È®ÀÎÇÑ´Ù.
    /// </summary>
    /// <param name="characterId">º¸À¯ ¿©ºÎ¸¦ È®ÀÎÇÒ Ä³¸¯ÅÍ ID</param>
    /// <param name="weaponId">º¸À¯ ¿©ºÎ¸¦ È®ÀÎÇÒ ¹«±â ID</param>
    /// <returns>º¸À¯ ÁßÀÌ¸é true, º¸À¯ÇÏÁö ¾Ê¾Ò´Ù¸é false</returns>
    public bool OwnsWeapon(string characterId, string weaponId)
    {
        // ºñ¾î ÀÖ´Â ¹«±â ID·Î´Â º¸À¯ ¿©ºÎ¸¦ È®ÀÎÇÒ ¼ö ¾ø´Ù.
        if (string.IsNullOrWhiteSpace(weaponId))
        {
            throw new ArgumentException(
                "º¸À¯ ¿©ºÎ¸¦ È®ÀÎÇÒ ¹«±â ID°¡ ºñ¾î ÀÖ½À´Ï´Ù.",
                nameof(weaponId));
        }

        // Ä³¸¯ÅÍÀÇ ÀúÀå µ¥ÀÌÅÍ¸¦ °¡Á®¿Â´Ù.
        // ±âÁ¸ ÀúÀå µ¥ÀÌÅÍ¶ó¸é ³»ºÎ¿¡¼­ NormalizeLoadoutµµ ½ÇÇàµÈ´Ù.
        CharacterLoadoutSaveData loadout =
            GetOrCreateCharacterLoadout(characterId);

        // º¸À¯ ¹«±â ID ¸ñ·Ï¿¡ ÇØ´ç ¹«±â°¡ ÀÖ´ÂÁö È®ÀÎÇÑ´Ù.
        return loadout.ownedWeaponIds.Contains(weaponId);
    }

    /// <summary>
    /// ÁöÁ¤ÇÑ Ä³¸¯ÅÍ°¡ ¹«±â¸¦ ±¸¸ÅÇÏµµ·Ï ½ÃµµÇÑ´Ù.
    /// ±¸¸Å¿¡ ¼º°øÇÏ¸é °ñµå¸¦ Â÷°¨ÇÏ°í º¸À¯ ¹«±â ¸ñ·Ï¿¡ Ãß°¡ÇÑ´Ù.
    /// </summary>
    /// <param name="characterId">¹«±â¸¦ ±¸¸ÅÇÒ Ä³¸¯ÅÍ ID</param>
    /// <param name="weaponId">±¸¸ÅÇÒ ¹«±â ID</param>
    /// <param name="price">±¸¸ÅÇÒ ¹«±âÀÇ °¡°İ</param>
    /// <returns>±¸¸Å¿¡ ¼º°øÇÏ¸é true, ±¸¸ÅÇÒ ¼ö ¾ø´Ù¸é false</returns>
    public bool TryPurchaseWeapon(string characterId, string weaponId, int price)
    {
        // ºñ¾î ÀÖ´Â ¹«±â ID·Î´Â ±¸¸ÅÇÒ ¼ö ¾ø´Ù.
        if (string.IsNullOrWhiteSpace(weaponId))
        {
            throw new ArgumentException(
                "±¸¸ÅÇÒ ¹«±â ID°¡ ºñ¾î ÀÖ½À´Ï´Ù.",
                nameof(weaponId));
        }

        // À½¼ö °¡°İÀº Àß¸ø ¼³Á¤µÈ ¹«±â µ¥ÀÌÅÍ´Ù.
        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(price),
                price,
                "¹«±â °¡°İÀº 0 ÀÌ»óÀÌ¾î¾ß ÇÕ´Ï´Ù.");
        }

        // ±¸¸ÅÇÒ Ä³¸¯ÅÍÀÇ ÀúÀå µ¥ÀÌÅÍ¸¦ °¡Á®¿Â´Ù.
        CharacterLoadoutSaveData loadout =
            GetOrCreateCharacterLoadout(characterId);

        // ÀÌ¹Ì º¸À¯ÇÑ ¹«±â´Â ´Ù½Ã ±¸¸ÅÇÏÁö ¾Ê´Â´Ù.
        if (loadout.ownedWeaponIds.Contains(weaponId))
        {
            return false;
        }

        // ÇöÀç °ñµå°¡ ¹«±â °¡°İº¸´Ù ÀûÀ¸¸é ±¸¸ÅÇÏÁö ¾Ê´Â´Ù.
        if (_playerSaveData.gold < price)
        {
            return false;
        }

        // ¹«±â °¡°İ¸¸Å­ ÇöÀç °ñµå¿¡¼­ Â÷°¨ÇÑ´Ù.
        _playerSaveData.gold -= price;

        // ±¸¸ÅÇÑ ¹«±â¸¦ Ä³¸¯ÅÍÀÇ º¸À¯ ¹«±â ¸ñ·Ï¿¡ Ãß°¡ÇÑ´Ù.
        loadout.ownedWeaponIds.Add(weaponId);

        // º¯°æµÈ °ñµå¿Í º¸À¯ ¹«±â ¸ñ·ÏÀ» JSON ÆÄÀÏ¿¡ ÀúÀåÇÑ´Ù.
        SaveToFile();

        // °ñµå°¡ º¯°æµÇ¾ú´Ù°í °ñµå UI¿¡ ¾Ë¸°´Ù.
        GoldChanged?.Invoke(_playerSaveData.gold);

        // ±¸¸Å°¡ Á¤»óÀûÀ¸·Î ¿Ï·áµÇ¾ú´Ù.
        return true;
    }

    /// <summary>
    /// ÁöÁ¤ÇÑ Ä³¸¯ÅÍÀÇ ÁöÁ¤ÇÑ ½½·Ô¿¡ ¹«±â¸¦ ÀåÂøÇÑ´Ù.
    /// </summary>
    /// <param name="characterId">¹«±â¸¦ ÀåÂøÇÒ Ä³¸¯ÅÍ ID</param>
    /// <param name="slotIndex">ÀåÂø ½½·Ô ¹øÈ£: 0, 1, 2</param>
    /// <param name="weaponId">ÀåÂøÇÒ ¹«±âÀÇ °íÀ¯ ID</param>
    public void EquipWeapon(string characterId, int slotIndex, string weaponId)
    {
        // ºñ¾î ÀÖ´Â ¹«±â ID´Â ÀåÂø ¿äÃ»À¸·Î ÀÎÁ¤ÇÏÁö ¾Ê´Â´Ù.
        if (string.IsNullOrWhiteSpace(weaponId))
        {
            throw new ArgumentException("ÀåÂøÇÒ ¹«±â ID°¡ ºñ¾î ÀÖ½À´Ï´Ù.", nameof(weaponId));
        }

        // Ä³¸¯ÅÍÀÇ ÀåÂø Á¤º¸¸¦ °¡Á®¿Â´Ù.
        CharacterLoadoutSaveData loadout = GetOrCreateCharacterLoadout(characterId);

        // ½½·Ô ¹øÈ£°¡ 0~2 ¹üÀ§¸¦ ¹ş¾î³ª¸é ÄÚµå ¿À·ù´Ù.
        if (slotIndex < 0 || slotIndex >= loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "¹«±â ½½·Ô ¹øÈ£°¡ ¹üÀ§¸¦ ¹ş¾î³µ½À´Ï´Ù.");
        }

        // ÁöÁ¤ÇÑ ½½·Ô¿¡ ¹«±â ID¸¦ ±â·ÏÇÑ´Ù.
        loadout.equippedWeaponIds[slotIndex] = weaponId;

        // ÀåÂø Á¤º¸°¡ º¯°æµÆÀ¸¹Ç·Î ·ÎÄÃ ÆÄÀÏ¿¡ Áï½Ã ÀúÀåÇÑ´Ù.
        SaveToFile();
    }

    /// <summary>
    /// ÁöÁ¤ÇÑ Ä³¸¯ÅÍÀÇ µÎ ÀåÂø ½½·Ô¿¡ µé¾î ÀÖ´Â ¹«±â ID¸¦ ¼­·Î ±³È¯ÇÏ°í ÀúÀåÇÑ´Ù.
    /// ºó ½½·ÔÀÌ Æ÷ÇÔµÇ¾î ÀÖ¾îµµ Á¤»óÀûÀ¸·Î ±³È¯ÇÒ ¼ö ÀÖ´Ù.
    /// </summary>
    /// <param name="characterId">ÀåÂø Á¤º¸¸¦ º¯°æÇÒ Ä³¸¯ÅÍ ID</param>
    /// <param name="sourceSlotIndex">µå·¡±×¸¦ ½ÃÀÛÇÑ ÀåÂø ½½·Ô ¹øÈ£</param>
    /// <param name="targetSlotIndex">¹«±â¸¦ µå·ÓÇÑ ÀåÂø ½½·Ô ¹øÈ£</param>
    public void SwapEquippedWeapons(
        string characterId,
        int sourceSlotIndex,
        int targetSlotIndex)
    {
        // Ä³¸¯ÅÍÀÇ ÀåÂø ¹«±â ÀúÀå µ¥ÀÌÅÍ¸¦ °¡Á®¿Â´Ù.
        CharacterLoadoutSaveData loadout =
            GetOrCreateCharacterLoadout(characterId);

        // Ãâ¹ß ½½·Ô ¹øÈ£°¡ ÀåÂø ½½·Ô ¹è¿­ÀÇ ¹üÀ§¸¦ ¹ş¾î³ª¸é ¼³Á¤ ¿À·ù´Ù.
        if (sourceSlotIndex < 0 ||
            sourceSlotIndex >= loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceSlotIndex),
                sourceSlotIndex,
                "Ãâ¹ß ÀåÂø ½½·Ô ¹øÈ£°¡ ¹üÀ§¸¦ ¹ş¾î³µ½À´Ï´Ù.");
        }

        // µµÂø ½½·Ô ¹øÈ£°¡ ÀåÂø ½½·Ô ¹è¿­ÀÇ ¹üÀ§¸¦ ¹ş¾î³ª¸é ¼³Á¤ ¿À·ù´Ù.
        if (targetSlotIndex < 0 ||
            targetSlotIndex >= loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetSlotIndex),
                targetSlotIndex,
                "µµÂø ÀåÂø ½½·Ô ¹øÈ£°¡ ¹üÀ§¸¦ ¹ş¾î³µ½À´Ï´Ù.");
        }

        // °°Àº ½½·Ô³¢¸®´Â ±³È¯ÇÒ ÇÊ¿ä°¡ ¾øÀ¸¹Ç·Î ÀúÀåÇÏÁö ¾Ê´Â´Ù.
        if (sourceSlotIndex == targetSlotIndex)
        {
            return;
        }

        // Ãâ¹ß ½½·Ô¿¡ µé¾î ÀÖ´ø ¹«±â ID¸¦ ÀÓ½Ã·Î º¸°üÇÑ´Ù.
        string sourceWeaponId =
            loadout.equippedWeaponIds[sourceSlotIndex];

        // µµÂø ½½·ÔÀÇ ¹«±â ID¸¦ Ãâ¹ß ½½·ÔÀ¸·Î ¿Å±ä´Ù.
        loadout.equippedWeaponIds[sourceSlotIndex] =
            loadout.equippedWeaponIds[targetSlotIndex];

        // ÀÓ½Ã·Î º¸°üÇÑ Ãâ¹ß ½½·ÔÀÇ ¹«±â ID¸¦ µµÂø ½½·ÔÀ¸·Î ¿Å±ä´Ù.
        loadout.equippedWeaponIds[targetSlotIndex] =
            sourceWeaponId;

        // ±³È¯µÈ ÀåÂø Á¤º¸¸¦ JSON ÆÄÀÏ¿¡ Áï½Ã ÀúÀåÇÑ´Ù.
        SaveToFile();
    }

    /// <summary>
    /// ÁöÁ¤ÇÑ Ä³¸¯ÅÍÀÇ ¹«±â ½½·ÔÀ» ºñ¿î´Ù.
    /// </summary>
    /// <param name="characterId">¹«±â¸¦ ÇØÁ¦ÇÒ Ä³¸¯ÅÍ ID</param>
    /// <param name="slotIndex">ºñ¿ï ½½·Ô ¹øÈ£: 0, 1, 2</param>
    public void UnequipWeapon(string characterId, int slotIndex)
    {
        // Ä³¸¯ÅÍÀÇ ÀåÂø Á¤º¸¸¦ °¡Á®¿Â´Ù.
        CharacterLoadoutSaveData loadout = GetOrCreateCharacterLoadout(characterId);

        // ½½·Ô ¹øÈ£°¡ ¹üÀ§¸¦ ¹ş¾î³ª¸é ÄÚµå ¿À·ù´Ù.
        if (slotIndex < 0 || slotIndex >= loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "¹«±â ½½·Ô ¹øÈ£°¡ ¹üÀ§¸¦ ¹ş¾î³µ½À´Ï´Ù.");
        }

        // nullÀ» ³Ö¾î ÇØ´ç ½½·ÔÀ» ÀåÂø ÇØÁ¦ »óÅÂ·Î ¸¸µç´Ù.
        loadout.equippedWeaponIds[slotIndex] = null;

        // ÀåÂø Á¤º¸°¡ º¯°æµÆÀ¸¹Ç·Î ·ÎÄÃ ÆÄÀÏ¿¡ Áï½Ã ÀúÀåÇÑ´Ù.
        SaveToFile();
    }

    /// <summary>
    /// ÀúÀåµÈ Ä³¸¯ÅÍÀÇ ÇöÀç ·¹º§À» ¹İÈ¯ÇÑ´Ù.
    /// ¾ÆÁ÷ ÀúÀå µ¥ÀÌÅÍ°¡ ¾ø´Â Ä³¸¯ÅÍ´Â ±âº» ·¹º§ÀÎ 1À» ¹İÈ¯ÇÑ´Ù.
    /// </summary>
    /// <param name="characterId">·¹º§À» È®ÀÎÇÒ Ä³¸¯ÅÍÀÇ °íÀ¯ ID</param>
    public int GetCharacterLevel(string characterId)
    {
        // ºó Ä³¸¯ÅÍ ID·Î´Â ÀúÀå µ¥ÀÌÅÍ¸¦ Ã£À» ¼ö ¾ø´Ù.
        if (string.IsNullOrWhiteSpace(characterId))
        {
            throw new ArgumentException(
                "·¹º§À» È®ÀÎÇÒ Ä³¸¯ÅÍ ID°¡ ºñ¾î ÀÖ½À´Ï´Ù.",
                nameof(characterId));
        }

        // ÀúÀåµÈ Ä³¸¯ÅÍ µ¥ÀÌÅÍ¸¦ Ã³À½ºÎÅÍ È®ÀÎÇÑ´Ù.
        for (int index = 0;
             index < _playerSaveData.characterLoadouts.Count;
             index++)
        {
            CharacterLoadoutSaveData loadout =
                _playerSaveData.characterLoadouts[index];

            // ºñ¾î ÀÖ´Â µ¥ÀÌÅÍ´Â °Ë»çÇÏÁö ¾Ê´Â´Ù.
            if (loadout == null)
            {
                continue;
            }

            // Ã£°í ÀÖ´Â Ä³¸¯ÅÍ°¡ ¾Æ´Ï¸é ´ÙÀ½ µ¥ÀÌÅÍ·Î ³Ñ¾î°£´Ù.
            if (loadout.characterId != characterId)
            {
                continue;
            }

            // ÀÌÀü ¼¼ÀÌºê¿¡¼­ Àß¸øµÈ °ªÀÌ µé¾î¿Ô´õ¶óµµ
            // Ä³¸¯ÅÍÀÇ ÃÖ¼Ò ·¹º§Àº 1·Î Ã³¸®ÇÑ´Ù.
            return Mathf.Max(1, loadout.level);
        }

        // ¾ÆÁ÷ ÀúÀå µ¥ÀÌÅÍ°¡ »ı¼ºµÇÁö ¾ÊÀº Ä³¸¯ÅÍ´Â
        // °ÔÀÓÀÇ ±âº» ½ÃÀÛ ·¹º§ÀÎ 1·Î Ã³¸®ÇÑ´Ù.
        return 1;
    }

    /// <summary>
    /// ¾÷Àû ID¿¡ ÇØ´çÇÏ´Â ÀúÀå ±â·ÏÀ» ¹İÈ¯ÇÑ´Ù.
    /// ¾ÆÁ÷ ±â·ÏÀÌ ¾ø´Ù¸é ¹Ì´Ş¼º »óÅÂÀÇ »õ ±â·ÏÀ» »ı¼ºÇÑ´Ù.
    /// </summary>
    /// <param name="achievementId">AchievementData¿¡ ¼³Á¤ÇÑ °íÀ¯ ID</param>
    public AchievementSaveData GetOrCreateAchievementRecord(
        string achievementId)
    {
        // ºó ID´Â Á¤»óÀûÀÎ ¾÷ÀûÀ» ³ªÅ¸³¾ ¼ö ¾ø´Ù.
        if (string.IsNullOrWhiteSpace(achievementId))
        {
            throw new ArgumentException(
                "¾÷Àû ID°¡ ºñ¾î ÀÖ½À´Ï´Ù.",
                nameof(achievementId));
        }

        // ÀÌÀü ¼¼ÀÌºê¿¡¼­µµ ¾÷Àû ¸ñ·ÏÀ» ¾ÈÀüÇÏ°Ô »ç¿ëÇÒ ¼ö ÀÖ°Ô ÇÑ´Ù.
        NormalizeAchievementRecords(_playerSaveData);

        // ±âÁ¸ ¾÷Àû ±â·Ï¿¡¼­ µ¿ÀÏÇÑ ID¸¦ Ã£´Â´Ù.
        for (int index = 0;
             index < _playerSaveData.achievementRecords.Count;
             index++)
        {
            AchievementSaveData achievementRecord =
                _playerSaveData.achievementRecords[index];

            // ºñ¾î ÀÖ´Â ±â·ÏÀº °Ë»çÇÏÁö ¾Ê´Â´Ù.
            if (achievementRecord == null)
            {
                continue;
            }

            // µ¿ÀÏÇÑ IDÀÇ ±â·ÏÀ» Ã£À¸¸é ±âÁ¸ µ¥ÀÌÅÍ¸¦ ¹İÈ¯ÇÑ´Ù.
            if (achievementRecord.achievementId == achievementId)
            {
                return achievementRecord;
            }
        }

        // ¾ÆÁ÷ ÀúÀå ±â·ÏÀÌ ¾ø´Â ¾÷ÀûÀº ¹Ì´Ş¼º »óÅÂ·Î »ı¼ºÇÑ´Ù.
        AchievementSaveData newRecord =
            new AchievementSaveData
            {
                achievementId = achievementId,
                isCompleted = false,
                isRewardClaimed = false
            };

        // »õ ¾÷Àû ±â·ÏÀ» ÇÃ·¹ÀÌ¾î ¼¼ÀÌºê µ¥ÀÌÅÍ¿¡ Ãß°¡ÇÑ´Ù.
        _playerSaveData.achievementRecords.Add(newRecord);

        // »õ ±â·ÏÀÌ »ı¼ºµÆÀ¸¹Ç·Î Áï½Ã ÆÄÀÏ¿¡ ÀúÀåÇÑ´Ù.
        SaveToFile();

        return newRecord;
    }

    /// <summary>
    /// ÁöÁ¤ÇÑ ¾÷ÀûÀ» ´Ş¼º »óÅÂ·Î º¯°æÇÏ°í Áï½Ã ÀúÀåÇÑ´Ù.
    /// ÀÌ¹Ì ´Ş¼ºÇÑ ¾÷ÀûÀº Áßº¹ Ã³¸®ÇÏÁö ¾Ê´Â´Ù.
    /// </summary>
    /// <param name="achievementId">´Ş¼º Ã³¸®ÇÒ ¾÷ÀûÀÇ °íÀ¯ ID</param>
    /// <returns>ÀÌ¹ø È£Ãâ·Î »õ·Ó°Ô ´Ş¼ºµÆ´Ù¸é true</returns>
    public bool TryCompleteAchievement(
        string achievementId)
    {
        // ±âÁ¸ ±â·ÏÀ» °¡Á®¿À°Å³ª ¹Ì´Ş¼º ±â·ÏÀ» »õ·Î ¸¸µç´Ù.
        AchievementSaveData achievementRecord =
            GetOrCreateAchievementRecord(achievementId);

        // ÀÌ¹Ì ´Ş¼ºÇÑ ¾÷ÀûÀº ´Ù½Ã ÀúÀåÇÏ°Å³ª Ã³¸®ÇÏÁö ¾Ê´Â´Ù.
        if (achievementRecord.isCompleted)
        {
            return false;
        }

        // ¾÷ÀûÀ» ´Ş¼º »óÅÂ·Î º¯°æÇÑ´Ù.
        achievementRecord.isCompleted = true;

        // ´Ş¼º¸¸ ÇßÀ» »Ó º¸»óÀº ¾ÆÁ÷ ¹ŞÁö ¾ÊÀº »óÅÂ´Ù.
        achievementRecord.isRewardClaimed = false;

        // ¾÷Àû ´Ş¼º »óÅÂ¸¦ Áï½Ã JSON ÆÄÀÏ¿¡ ÀúÀåÇÑ´Ù.
        SaveToFile();

        Debug.Log(
            $"¾÷Àû ´Ş¼º: {achievementId}");

        return true;
    }

    /// <summary>
    /// ´Ş¼ºÇÑ ¾÷ÀûÀÇ º¸»óÀ» Áö±ŞÇÏ°í ¼ö·É ¿Ï·á »óÅÂ·Î ÀúÀåÇÑ´Ù.
    /// Á¶°Ç ¹Ì´Ş¼º ¾÷ÀûÀÌ³ª ÀÌ¹Ì º¸»óÀ» ¹ŞÀº ¾÷ÀûÀº Ã³¸®ÇÏÁö ¾Ê´Â´Ù.
    /// </summary>
    /// <param name="achievementData">º¸»óÀ» Áö±ŞÇÒ ¾÷Àû Config</param>
    /// <returns>ÀÌ¹ø È£Ãâ¿¡¼­ º¸»ó Áö±Ş¿¡ ¼º°øÇß´Ù¸é true</returns>
    public bool TryClaimAchievementReward(
        AchievementData achievementData)
    {
        // Config°¡ ¾øÀ¸¸é º¸»ó Á¾·ù¿Í ¼ö·®À» È®ÀÎÇÒ ¼ö ¾ø´Ù.
        if (achievementData == null)
        {
            throw new ArgumentNullException(
                nameof(achievementData));
        }

        // ÇØ´ç ¾÷ÀûÀÇ ÇÃ·¹ÀÌ¾î ÀúÀå ±â·ÏÀ» °¡Á®¿Â´Ù.
        AchievementSaveData achievementRecord =
            GetOrCreateAchievementRecord(
                achievementData.Id);

        // ¾ÆÁ÷ Á¶°ÇÀ» ´Ş¼ºÇÏÁö ¾ÊÀº ¾÷ÀûÀº º¸»óÀ» ¹ŞÀ» ¼ö ¾ø´Ù.
        if (!achievementRecord.isCompleted)
        {
            return false;
        }

        // ÀÌ¹Ì º¸»óÀ» ¹ŞÀº ¾÷ÀûÀº ´Ù½Ã Áö±ŞÇÏÁö ¾Ê´Â´Ù.
        if (achievementRecord.isRewardClaimed)
        {
            return false;
        }

        // Config¿¡ ¼³Á¤µÈ º¸»ó ¼ö·®ÀÌ ¿Ã¹Ù¸¥Áö È®ÀÎÇÑ´Ù.
        if (achievementData.RewardValue <= 0)
        {
            throw new InvalidOperationException(
                $"¾÷Àû '{achievementData.Id}'ÀÇ º¸»ó ¼ö·®Àº " +
                "0º¸´Ù Ä¿¾ß ÇÕ´Ï´Ù.");
        }

        // º¸»ó Á¾·ù¿¡ ¸Â´Â ÇÃ·¹ÀÌ¾î µ¥ÀÌÅÍ¸¦ º¯°æÇÑ´Ù.
        switch (achievementData.RewardType)
        {
            case AchievementRewardType.Gold:

                // °ñµå º¸»ó¸¸Å­ ÇöÀç º¸À¯ °ñµå¸¦ Áõ°¡½ÃÅ²´Ù.
                _playerSaveData.gold +=
                    achievementData.RewardValue;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(achievementData.RewardType),
                    achievementData.RewardType,
                    "Áö¿øÇÏÁö ¾Ê´Â ¾÷Àû º¸»ó Á¾·ùÀÔ´Ï´Ù.");
        }

        // °°Àº º¸»óÀ» ´Ù½Ã ¹ŞÀ» ¼ö ¾øµµ·Ï ¼ö·É ¿Ï·á Ã³¸®ÇÑ´Ù.
        achievementRecord.isRewardClaimed = true;

        // °ñµå¿Í º¸»ó ¼ö·É »óÅÂ¸¦ ÇÑ ¹ø¿¡ ÀúÀåÇÑ´Ù.
        // µÑÀ» µû·Î ÀúÀåÇÏ¸é ÇÑÂÊ¸¸ ÀúÀåµÇ´Â ¹®Á¦°¡ »ı±æ ¼ö ÀÖ´Ù.
        SaveToFile();

        // º¸»ó Á¾·ù°¡ °ñµå¶ó¸é °ñµå UI¿¡ º¯°æµÈ ÃÑ·®À» ¾Ë¸°´Ù.
        if (achievementData.RewardType ==
            AchievementRewardType.Gold)
        {
            GoldChanged?.Invoke(
                _playerSaveData.gold);
        }

        Debug.Log(
            $"¾÷Àû º¸»ó ¼ö·É: {achievementData.Id}, " +
            $"{achievementData.RewardType} " +
            $"{achievementData.RewardValue}");

        return true;
    }

    /// <summary>
    /// ÇöÀç ÃÑ °ñµå¸¦ Áõ°¡½ÃÅ°°í JSON°ú UI¿¡ ¹İ¿µÇÑ´Ù.
    /// </summary>
    public void AddGold(int amount)
    {
        // 0 ÀÌÇÏÀÇ °ªÀº Á¤»óÀûÀÎ °ñµå È¹µæ·®ÀÌ ¾Æ´Ï´Ù.
        if (amount <= 0)
        {
            return;
        }

        // ¸Ş¸ğ¸®¿¡ ·ÎµåµÈ ÇÃ·¹ÀÌ¾î ÃÑ °ñµå¸¦ Áõ°¡½ÃÅ²´Ù.
        _playerSaveData.gold += amount;

        // º¯°æµÈ ÃÑ °ñµå¸¦ JSON ÆÄÀÏ¿¡ ÀúÀåÇÑ´Ù.
        SaveToFile();

        // ÇöÀç ÃÑ °ñµå°¡ º¯°æµÆ´Ù°í UI¿¡ ¾Ë¸°´Ù.
        GoldChanged?.Invoke(_playerSaveData.gold);
    }

    /// <summary>
    /// Å×½ºÆ®¿ë ContextMenu¸¦ ÇöÀç ¾ÈÀüÇÏ°Ô ½ÇÇàÇÒ ¼ö ÀÖ´ÂÁö È®ÀÎÇÑ´Ù.
    /// SaveManagerÀÇ ÀúÀå °æ·Î°¡ ÁØºñµÈ Play Mode¿¡¼­¸¸ ½ÇÇàÇÑ´Ù.
    /// </summary>
    private bool CanRunTestContextMenu()
    {
        // Edit Mode¿¡¼­´Â Awake°¡ ½ÇÇàµÇÁö ¾Ê¾Æ ÀúÀå °æ·Î¿Í µ¥ÀÌÅÍ°¡
        // ÁØºñµÇÁö ¾Ê¾ÒÀ» ¼ö ÀÖÀ¸¹Ç·Î ½ÇÇàÇÏÁö ¾Ê´Â´Ù.
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "SaveManager Å×½ºÆ® ¸Ş´º´Â Play Mode¿¡¼­ ½ÇÇàÇØÁÖ¼¼¿ä.");

            return false;
        }

        return true;
    }

    /// <summary>
    /// Å×½ºÆ®¸¦ À§ÇØ º¸À¯ °ñµå¸¦ 0À¸·Î ÃÊ±âÈ­ÇÑ´Ù.
    /// </summary>
    [ContextMenu("Å×½ºÆ® 1. º¸À¯ °ñµå ÃÊ±âÈ­")]
    private void ResetGoldForTest()
    {
        // Play Mode°¡ ¾Æ´Ï¶ó¸é ÀúÀå µ¥ÀÌÅÍ¸¦ º¯°æÇÏÁö ¾Ê´Â´Ù.
        if (!CanRunTestContextMenu())
        {
            return;
        }

        // ÇöÀç º¸À¯ °ñµå¸¦ ÃÊ±â°ªÀÎ 0À¸·Î º¯°æÇÑ´Ù.
        _playerSaveData.gold = 0;

        // º¯°æµÈ °ñµå¸¦ JSON ÀúÀå ÆÄÀÏ¿¡ ¹İ¿µÇÑ´Ù.
        SaveToFile();

        // °ñµå¸¦ ±¸µ¶ ÁßÀÎ UI°¡ ÀÖ´Ù¸é Áï½Ã º¯°æ »ç½ÇÀ» ¾Ë¸°´Ù.
        GoldChanged?.Invoke(_playerSaveData.gold);

        Debug.Log("Å×½ºÆ®: º¸À¯ °ñµå¸¦ 0À¸·Î ÃÊ±âÈ­Çß½À´Ï´Ù.");
    }

    /// <summary>
    /// Å×½ºÆ®¸¦ À§ÇØ SoldierÀÇ ·¹º§°ú °æÇèÄ¡¸¦ ÃÊ±âÈ­ÇÑ´Ù.
    /// </summary>
    [ContextMenu("Å×½ºÆ® 2. Soldier ·¹º§ 1 ÃÊ±âÈ­")]
    private void ResetSoldierLevelForTest()
    {
        // SoldierÀÇ ·¹º§À» 1, °æÇèÄ¡¸¦ 0À¸·Î ÃÊ±âÈ­ÇÑ´Ù.
        ResetCharacterLevelForTest(SoldierCharacterId);
    }

    /// <summary>
    /// Å×½ºÆ®¸¦ À§ÇØ WarriorÀÇ ·¹º§°ú °æÇèÄ¡¸¦ ÃÊ±âÈ­ÇÑ´Ù.
    /// </summary>
    [ContextMenu("Å×½ºÆ® 3. Warrior ·¹º§ 1 ÃÊ±âÈ­")]
    private void ResetWarriorLevelForTest()
    {
        // WarriorÀÇ ·¹º§À» 1, °æÇèÄ¡¸¦ 0À¸·Î ÃÊ±âÈ­ÇÑ´Ù.
        ResetCharacterLevelForTest(WarriorCharacterId);
    }

    /// <summary>
    /// Å×½ºÆ®¸¦ À§ÇØ SoldierÀÇ ÇöÀç ·¹º§À» 1 Áõ°¡½ÃÅ²´Ù.
    /// </summary>
    [ContextMenu("Å×½ºÆ® 4. Soldier ·¹º§ 1 Áõ°¡")]
    private void AddSoldierLevelForTest()
    {
        // SoldierÀÇ ÇöÀç °æÇèÄ¡´Â À¯ÁöÇÏ°í ·¹º§¸¸ 1 Áõ°¡½ÃÅ²´Ù.
        AddCharacterLevelForTest(SoldierCharacterId);
    }

    /// <summary>
    /// Å×½ºÆ®¸¦ À§ÇØ WarriorÀÇ ÇöÀç ·¹º§À» 1 Áõ°¡½ÃÅ²´Ù.
    /// </summary>
    [ContextMenu("Å×½ºÆ® 5. Warrior ·¹º§ 1 Áõ°¡")]
    private void AddWarriorLevelForTest()
    {
        // WarriorÀÇ ÇöÀç °æÇèÄ¡´Â À¯ÁöÇÏ°í ·¹º§¸¸ 1 Áõ°¡½ÃÅ²´Ù.
        AddCharacterLevelForTest(WarriorCharacterId);
    }

    /// <summary>
    /// Å×½ºÆ®¸¦ À§ÇØ º¸À¯ °ñµå¸¦ 1,000 Áõ°¡½ÃÅ²´Ù.
    /// </summary>
    [ContextMenu("Å×½ºÆ® 6. °ñµå 1000 Áõ°¡")]
    private void AddGoldForTest()
    {
        // Play Mode°¡ ¾Æ´Ï¶ó¸é ÀúÀå µ¥ÀÌÅÍ¸¦ º¯°æÇÏÁö ¾Ê´Â´Ù.
        if (!CanRunTestContextMenu())
        {
            return;
        }

        // ±âÁ¸ °ñµå Áõ°¡ ÇÔ¼ö¸¦ Àç»ç¿ëÇÑ´Ù.
        // ³»ºÎ¿¡¼­ ÀúÀå°ú GoldChanged ÀÌº¥Æ® ¹ß»ı±îÁö Ã³¸®ÇÑ´Ù.
        AddGold(1000);

        Debug.Log(
            $"Å×½ºÆ®: º¸À¯ °ñµå¸¦ 1,000 Ãß°¡Çß½À´Ï´Ù. ÇöÀç °ñµå: {_playerSaveData.gold}");
    }

    /// <summary>
    /// ÁöÁ¤ÇÑ Ä³¸¯ÅÍÀÇ ·¹º§À» 1, °æÇèÄ¡¸¦ 0À¸·Î ÃÊ±âÈ­ÇÑ´Ù.
    /// </summary>
    private void ResetCharacterLevelForTest(string characterId)
    {
        // Play Mode°¡ ¾Æ´Ï¶ó¸é ÀúÀå µ¥ÀÌÅÍ¸¦ º¯°æÇÏÁö ¾Ê´Â´Ù.
        if (!CanRunTestContextMenu())
        {
            return;
        }

        // Ä³¸¯ÅÍÀÇ ±âÁ¸ ÀúÀå µ¥ÀÌÅÍ¸¦ °¡Á®¿Â´Ù.
        // ¾ÆÁ÷ ¾ø´Ù¸é »õ·Î¿î ÀúÀå µ¥ÀÌÅÍ¸¦ »ı¼ºÇÑ´Ù.
        CharacterLoadoutSaveData loadout =
            GetOrCreateCharacterLoadout(characterId);

        // ¿ÏÀüÈ÷ µ¿ÀÏÇÑ ·¹º§ Å×½ºÆ® Á¶°ÇÀ» ¸¸µé±â À§ÇØ
        // ·¹º§»Ó ¾Æ´Ï¶ó ÇöÀç °æÇèÄ¡µµ ÃÊ±âÈ­ÇÑ´Ù.
        loadout.level = 1;
        loadout.currentExp = 0;

        // º¯°æµÈ Ä³¸¯ÅÍ µ¥ÀÌÅÍ¸¦ ÀúÀå ÆÄÀÏ¿¡ ¹İ¿µÇÑ´Ù.
        SaveToFile();

        Debug.Log(
            $"Å×½ºÆ®: {characterId}ÀÇ ·¹º§À» 1, °æÇèÄ¡¸¦ 0À¸·Î ÃÊ±âÈ­Çß½À´Ï´Ù.");
    }

    /// <summary>
    /// ÁöÁ¤ÇÑ Ä³¸¯ÅÍÀÇ ÇöÀç ·¹º§À» 1 Áõ°¡½ÃÅ²´Ù.
    /// </summary>
    private void AddCharacterLevelForTest(string characterId)
    {
        // Play Mode°¡ ¾Æ´Ï¶ó¸é ÀúÀå µ¥ÀÌÅÍ¸¦ º¯°æÇÏÁö ¾Ê´Â´Ù.
        if (!CanRunTestContextMenu())
        {
            return;
        }

        // Ä³¸¯ÅÍÀÇ ±âÁ¸ ÀúÀå µ¥ÀÌÅÍ¸¦ °¡Á®¿Â´Ù.
        // ¾ÆÁ÷ ¾ø´Ù¸é »õ·Î¿î ÀúÀå µ¥ÀÌÅÍ¸¦ »ı¼ºÇÑ´Ù.
        CharacterLoadoutSaveData loadout =
            GetOrCreateCharacterLoadout(characterId);

        // Àá±İ ÇØÁ¦ Å×½ºÆ®¸¦ À§ÇØ ÇöÀç ·¹º§À» 1 Áõ°¡½ÃÅ²´Ù.
        loadout.level++;

        // º¯°æµÈ Ä³¸¯ÅÍ µ¥ÀÌÅÍ¸¦ ÀúÀå ÆÄÀÏ¿¡ ¹İ¿µÇÑ´Ù.
        SaveToFile();

        Debug.Log(
            $"Å×½ºÆ®: {characterId}ÀÇ ·¹º§À» Áõ°¡½ÃÄ×½À´Ï´Ù. ÇöÀç ·¹º§: {loadout.level}");
    }

    /// <summary>
    /// ÀÎ½ºÆåÅÍ¿¡¼­ ¼±ÅÃÇÑ ½ºÅ×ÀÌÁöÀÇ ÃÖ´Ü Å¬¸®¾î ±â·Ï¸¸ ÃÊ±âÈ­ÇÑ´Ù.
    /// </summary>
    [ContextMenu("Å×½ºÆ® 7. ¼±ÅÃÇÑ ½ºÅ×ÀÌÁö Å¬¸®¾î ±â·Ï ÃÊ±âÈ­")]
    private void ResetSelectedStageBestClearTimeForTest()
    {
        // ¼¼ÀÌºê µ¥ÀÌÅÍ°¡ ÁØºñµÈ Play Mode¿¡¼­¸¸ ½ÇÇàÇÑ´Ù.
        if (!CanRunTestContextMenu())
        {
            return;
        }

        // Stage1Àº 0, Stage2´Â 1·Î ¼³Á¤µÇ¾î ÀÖÀ¸¹Ç·Î
        // ¼±ÅÃÇÑ ¿­°ÅÇü °ªÀ» ¼¼ÀÌºê µ¥ÀÌÅÍÀÇ ½ºÅ×ÀÌÁö ÀÎµ¦½º·Î »ç¿ëÇÑ´Ù.
        int stageIndex = (int)_resetTargetStage;

        // ¼±ÅÃÇÑ ½ºÅ×ÀÌÁöÀÇ ±â·Ï¸¸ Ã£¾Æ¼­ Á¦°ÅÇÏ°í ÀúÀåÇÑ´Ù.
        ResetStageBestClearTimeForTest(stageIndex);
    }

    /// <summary>
    /// ÁöÁ¤ÇÑ ½ºÅ×ÀÌÁöÀÇ ÃÖ´Ü Å¬¸®¾î ±â·ÏÀ» ¼¼ÀÌºê µ¥ÀÌÅÍ¿¡¼­ Á¦°ÅÇÑ´Ù.
    /// </summary>
    /// <param name="stageIndex">ÃÊ±âÈ­ÇÒ ½ºÅ×ÀÌÁöÀÇ 0ºÎÅÍ ½ÃÀÛÇÏ´Â ÀÎµ¦½º</param>
    private void ResetStageBestClearTimeForTest(int stageIndex)
    {
        // À½¼ö ÀÎµ¦½º´Â ½ÇÁ¦ ½ºÅ×ÀÌÁö¸¦ ³ªÅ¸³¾ ¼ö ¾ø´Ù.
        if (stageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stageIndex),
                stageIndex,
                "½ºÅ×ÀÌÁö ÀÎµ¦½º´Â 0 ÀÌ»óÀÌ¾î¾ß ÇÕ´Ï´Ù.");
        }

        // ÀÌÀü ¹öÀüÀÇ ¼¼ÀÌºê ÆÄÀÏ¿¡¼­µµ ¸ñ·ÏÀÌ nullÀÌ µÇÁö ¾Êµµ·Ï º¸Á¤ÇÑ´Ù.
        NormalizeStageClearRecords(_playerSaveData);

        bool removedRecord = false;

        // Á¦°ÅÇÏ¸é¼­ ¸ñ·Ï ¼ø¼­°¡ ´ç°ÜÁ®µµ ¾ÈÀüÇÏµµ·Ï µÚ¿¡¼­ºÎÅÍ È®ÀÎÇÑ´Ù.
        for (int index =
                 _playerSaveData.stageClearRecords.Count - 1;
             index >= 0;
             index--)
        {
            StageClearSaveData record =
                _playerSaveData.stageClearRecords[index];

            // ºñ¾î ÀÖ´Â ±â·ÏÀÌ°Å³ª ´Ù¸¥ ½ºÅ×ÀÌÁöÀÇ ±â·ÏÀÌ¸é °Ç³Ê¶Ú´Ù.
            if (record == null ||
                record.stageIndex != stageIndex)
            {
                continue;
            }

            // ÇØ´ç ½ºÅ×ÀÌÁöÀÇ ±â·ÏÀ» ¸ñ·Ï¿¡¼­ ¿ÏÀüÈ÷ Á¦°ÅÇÑ´Ù.
            _playerSaveData.stageClearRecords.RemoveAt(index);
            removedRecord = true;
        }

        // »èÁ¦µÈ ±â·ÏÀÌ ¾ø´Ù¸é ÆÄÀÏÀ» ´Ù½Ã ÀúÀåÇÒ ÇÊ¿ä°¡ ¾ø´Ù.
        if (!removedRecord)
        {
            Debug.Log(
                $"Stage {stageIndex + 1}¿¡ ÃÊ±âÈ­ÇÒ Å¬¸®¾î ±â·ÏÀÌ ¾ø½À´Ï´Ù.");

            return;
        }

        // ±â·ÏÀÌ Á¦°ÅµÈ ÇöÀç ¼¼ÀÌºê µ¥ÀÌÅÍ¸¦ ÆÄÀÏ¿¡ ¹İ¿µÇÑ´Ù.
        SaveToFile();

        Debug.Log(
            $"Stage {stageIndex + 1} Å¬¸®¾î ±â·ÏÀ» ÃÊ±âÈ­Çß½À´Ï´Ù.");
    }

    /// <summary>
    /// ÀÌÀü ¹öÀüÀÇ ÀúÀå µ¥ÀÌÅÍµµ ÇöÀç Àåºñ µ¥ÀÌÅÍ Çü½Ä¿¡ ¸Â°Ô º¸Á¤ÇÑ´Ù.
    /// </summary>
    private void NormalizeLoadout(CharacterLoadoutSaveData loadout)
    {
        // ÀÌÀü ÀúÀå ÆÄÀÏ¿¡´Â º¸À¯ ¹«±â ¸ñ·ÏÀÌ ¾øÀ» ¼ö ÀÖ´Ù.
        if (loadout.ownedWeaponIds == null)
        {
            loadout.ownedWeaponIds = new List<string>();
        }

        // ÀåÂø ¹è¿­ÀÌ ¾ø°Å³ª Å©±â°¡ ´Ù¸£¸é ±âÁ¸ °ªÀ» º¸Á¸ÇÏ¸ç 3Ä­À¸·Î ¸¸µç´Ù.
        if (loadout.equippedWeaponIds == null ||
            loadout.equippedWeaponIds.Length != 3)
        {
            string[] normalizedWeaponIds = new string[3];

            if (loadout.equippedWeaponIds != null)
            {
                int copyCount = Mathf.Min(
                    loadout.equippedWeaponIds.Length,
                    normalizedWeaponIds.Length);

                for (int i = 0; i < copyCount; i++)
                {
                    normalizedWeaponIds[i] =
                        loadout.equippedWeaponIds[i];
                }
            }

            loadout.equippedWeaponIds = normalizedWeaponIds;
        }

        // ÀÌ¹Ì ÀåÂøµÈ ¹«±â´Â ¹İµå½Ã º¸À¯ ÁßÀÎ ¹«±â·Î Ãë±ŞÇÑ´Ù.
        for (int i = 0; i < loadout.equippedWeaponIds.Length; i++)
        {
            string equippedWeaponId =
                loadout.equippedWeaponIds[i];

            if (string.IsNullOrWhiteSpace(equippedWeaponId))
            {
                continue;
            }

            if (!loadout.ownedWeaponIds.Contains(equippedWeaponId))
            {
                loadout.ownedWeaponIds.Add(equippedWeaponId);
            }
        }
    }

    /// <summary>
    /// ½ºÅ×ÀÌÁö Å¬¸®¾î ±â·Ï ¸ñ·ÏÀÌ Ç×»ó »ç¿ë °¡´ÉÇÑ »óÅÂÀÎÁö È®ÀÎÇÑ´Ù.
    /// </summary>
    private void NormalizeStageClearRecords(
        PlayerSaveData saveData)
    {
        if (saveData.stageClearRecords == null)
        {
            saveData.stageClearRecords =
                new List<StageClearSaveData>();
        }
    }

    /// <summary>
    /// ÇØ´ç ½ºÅ×ÀÌÁöÀÇ ÃÖ´Ü Å¬¸®¾î ½Ã°£À» ¹İÈ¯ÇÑ´Ù.
    /// Å¬¸®¾î ±â·ÏÀÌ ¾øÀ¸¸é -1À» ¹İÈ¯ÇÑ´Ù.
    /// </summary>
    public float GetBestClearTime(int stageIndex)
    {
        if (stageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stageIndex),
                stageIndex,
                "½ºÅ×ÀÌÁö ÀÎµ¦½º´Â 0 ÀÌ»óÀÌ¾î¾ß ÇÕ´Ï´Ù.");
        }

        NormalizeStageClearRecords(
            _playerSaveData);

        for (int index = 0;
             index < _playerSaveData.stageClearRecords.Count;
             index++)
        {
            StageClearSaveData record =
                _playerSaveData.stageClearRecords[index];

            // ºñ¾î ÀÖ´Â ±â·ÏÀº °Ç³Ê¶Ú´Ù.
            if (record == null)
            {
                continue;
            }

            if (record.stageIndex == stageIndex)
            {
                return record.bestClearTime;
            }
        }

        // ¾ÆÁ÷ ÇØ´ç ½ºÅ×ÀÌÁö¸¦ Å¬¸®¾îÇÑ ÀûÀÌ ¾ø´Ù.
        return -1f;
    }

    /// <summary>
    /// ±âÁ¸ ±â·Ïº¸´Ù ºü¸¥ °æ¿ì¿¡¸¸ ÃÖ´Ü Å¬¸®¾î ½Ã°£À» ÀúÀåÇÑ´Ù.
    /// ±â·ÏÀÌ °»½ÅµÆÀ¸¸é true¸¦ ¹İÈ¯ÇÑ´Ù.
    /// </summary>
    public bool TryUpdateBestClearTime(
        int stageIndex,
        float clearTime)
    {
        if (stageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stageIndex),
                stageIndex,
                "½ºÅ×ÀÌÁö ÀÎµ¦½º´Â 0 ÀÌ»óÀÌ¾î¾ß ÇÕ´Ï´Ù.");
        }

        if (clearTime <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(clearTime),
                clearTime,
                "Å¬¸®¾î ½Ã°£Àº 0º¸´Ù Ä¿¾ß ÇÕ´Ï´Ù.");
        }

        NormalizeStageClearRecords(
            _playerSaveData);

        for (int index = 0;
             index < _playerSaveData.stageClearRecords.Count;
             index++)
        {
            StageClearSaveData record =
                _playerSaveData.stageClearRecords[index];

            if (record == null ||
                record.stageIndex != stageIndex)
            {
                continue;
            }

            // ±âÁ¸ Á¤»ó ±â·Ïº¸´Ù ´À¸®°Å³ª °°À¸¸é °»½ÅÇÏÁö ¾Ê´Â´Ù.
            if (record.bestClearTime > 0f &&
                clearTime >= record.bestClearTime)
            {
                return false;
            }

            // ±âÁ¸ ±â·Ïº¸´Ù ºü¸£¹Ç·Î ÃÖ´Ü ½Ã°£À» ±³Ã¼ÇÑ´Ù.
            record.bestClearTime = clearTime;

            SaveToFile();

            Debug.Log(
                $"Stage {stageIndex + 1} ÃÖ´Ü ±â·Ï °»½Å: {clearTime:F2}ÃÊ");

            return true;
        }

        // Ã¹ Å¬¸®¾î¶ó¸é »õ·Î¿î ½ºÅ×ÀÌÁö ±â·ÏÀ» ¸¸µç´Ù.
        StageClearSaveData newRecord =
            new StageClearSaveData
            {
                stageIndex = stageIndex,
                bestClearTime = clearTime
            };

        _playerSaveData.stageClearRecords.Add(
            newRecord);

        SaveToFile();

        Debug.Log(
            $"Stage {stageIndex + 1} Ã¹ Å¬¸®¾î ±â·Ï ÀúÀå: {clearTime:F2}ÃÊ");

        return true;
    }

    /// <summary>
    /// ÀÌÀü ¹öÀü ¼¼ÀÌºê ÆÄÀÏ¿¡¼­µµ ¾÷Àû ±â·Ï ¸ñ·ÏÀ»
    /// ¾ÈÀüÇÏ°Ô »ç¿ëÇÒ ¼ö ÀÖµµ·Ï ºó ¸ñ·ÏÀ» »ı¼ºÇÑ´Ù.
    /// </summary>
    private void NormalizeAchievementRecords(
        PlayerSaveData saveData)
    {
        // ±âÁ¸ ¼¼ÀÌºê ÆÄÀÏ¿¡´Â ¾÷Àû ¸ñ·ÏÀÌ ¾øÀ» ¼ö ÀÖ´Ù.
        if (saveData.achievementRecords == null)
        {
            saveData.achievementRecords =
                new List<AchievementSaveData>();
        }
    }

    // ÇöÀç User ·¹º§¿¡¼­ ÇÊ¿äÇÑ °æÇèÄ¡¸¦ °¡Á®¿Â´Ù.
    public bool TryGetCurrentUserRequiredExp(out int requiredExp)
    {
        requiredExp = 0;

        if (!IsLoggedIn || _userLevelData == null)
        {
            return false;
        }

        return _userLevelData.TryGetRequiredExp(
            CurrentUserLevel, out requiredExp);
    }

    // È­¸é °»½Å ¿À·ù°¡ ·Î±×ÀÎÀÌ³ª ÀúÀå ¼º°øÀ» µÚÁıÁö ¾Êµµ·Ï °³º° ÅëÁöÇÑ´Ù.
    private void NotifyUserStatusChanged()
    {
        Action handlers = UserStatusChanged;

        if (handlers == null)
        {
            return;
        }

        foreach (Action handler in handlers.GetInvocationList())
        {
            try
            {
                handler.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    // User °æÇèÄ¡¸¦ 50 Ãß°¡ÇÑ´Ù.
    [ContextMenu("User Å×½ºÆ®/°æÇèÄ¡ 50 Ãß°¡")]
    private void AddUserExpForTest()
    {
        ChangeUserProgressForTest(50, 0);
    }

    // ÇöÀç ·¹º§ÀÇ User °æÇèÄ¡¸¦ 50 °¨¼Ò½ÃÅ²´Ù.
    [ContextMenu("User Å×½ºÆ®/°æÇèÄ¡ 50 °¨¼Ò")]
    private void SubtractUserExpForTest()
    {
        ChangeUserProgressForTest(-50, 0);
    }

    // User ·¹º§À» 1 ¿Ã¸®°í °æÇèÄ¡¸¦ ÃÊ±âÈ­ÇÑ´Ù.
    [ContextMenu("User Å×½ºÆ®/·¹º§ 1 Áõ°¡")]
    private void IncreaseUserLevelForTest()
    {
        ChangeUserProgressForTest(0, 1);
    }

    // User ·¹º§À» 1 ³»¸®°í °æÇèÄ¡¸¦ ÃÊ±âÈ­ÇÑ´Ù.
    [ContextMenu("User Å×½ºÆ®/·¹º§ 1 °¨¼Ò")]
    private void DecreaseUserLevelForTest()
    {
        ChangeUserProgressForTest(0, -1);
    }

    // ³× Å×½ºÆ® ¸Ş´ºÀÇ °è»ê¡¤°ËÁõ¡¤ÀúÀåÀ» °øÅëÀ¸·Î Ã³¸®ÇÑ´Ù.
    private void ChangeUserProgressForTest(int expChange, int levelChange)
    {
        // ½ÇÇà ÁßÀÎ ½ÇÁ¦ ÀúÀå °ü¸®ÀÚ¿¡¼­¸¸ Å×½ºÆ®ÇÑ´Ù.
        if (!CanRunTestContextMenu())
        {
            return;
        }

        if (Instance != this || !IsLoggedIn)
        {
            Debug.LogWarning(
                "·Î±×ÀÎ ÈÄ ÇöÀç »ç¿ë ÁßÀÎ SaveManager¿¡¼­ ½ÇÇàÇØÁÖ¼¼¿ä.",
                this);
            return;
        }

        if (_userLevelData == null)
        {
            Debug.LogWarning("UserLevelData¸¦ ¿¬°áÇØÁÖ¼¼¿ä.", this);
            return;
        }

        // °è»êÀÌ ³¡³ª±â Àü¿¡´Â ½ÇÁ¦ ÀúÀå µ¥ÀÌÅÍ¸¦ º¯°æÇÏÁö ¾Ê´Â´Ù.
        int nextLevel = _playerSaveData.userLevel;
        long nextExp = _playerSaveData.userExp;

        if (levelChange != 0)
        {
            // ·¹º§ 1¿¡¼­ °¨¼Ò¸¦ ´©¸£¸é ±âÁ¸ °æÇèÄ¡µµ ±×´ë·Î À¯ÁöÇÑ´Ù.
            if (levelChange < 0 && nextLevel <= 1)
            {
                return;
            }

            if (levelChange > 0 && nextLevel == int.MaxValue)
            {
                Debug.LogWarning("´õ ÀÌ»ó ·¹º§À» Áõ°¡½ÃÅ³ ¼ö ¾ø½À´Ï´Ù.", this);
                return;
            }

            nextLevel += levelChange;

            // ·¹º§À» Á÷Á¢ º¯°æÇÏ´Â Å×½ºÆ®´Â ÇØ´ç ·¹º§ÀÇ 0%ºÎÅÍ ½ÃÀÛÇÑ´Ù.
            nextExp = 0;
        }
        else
        {
            // µ¡¼À Áß Á¤¼ö ¹üÀ§¸¦ ³ÑÁö ¾Êµµ·Ï longÀ¸·Î °è»êÇÑ´Ù.
            nextExp = Math.Max(0L, nextExp + expChange);
        }

        while (true)
        {
            // °è»ê Áß µµ´ŞÇÏ´Â °¢ ·¹º§ÀÇ ÇÊ¿ä °æÇèÄ¡¸¦ È®ÀÎÇÑ´Ù.
            if (!_userLevelData.TryGetRequiredExp(
                    nextLevel, out int requiredExp))
            {
                Debug.LogWarning(
                    $"User {nextLevel}·¹º§ÀÇ ÇÊ¿ä °æÇèÄ¡°¡ ¾ø½À´Ï´Ù. " +
                    "UserLevelData¸¦ ¼³Á¤ÇØÁÖ¼¼¿ä. º¯°æÀº Ãë¼ÒÇß½À´Ï´Ù.",
                    this);
                return;
            }

            if (nextExp < requiredExp)
            {
                break;
            }

            if (nextLevel == int.MaxValue)
            {
                Debug.LogWarning("´õ ÀÌ»ó ·¹º§¾÷ÇÒ ¼ö ¾ø½À´Ï´Ù.", this);
                return;
            }

            // ÇÊ¿äÇÑ °æÇèÄ¡¸¸ ¼ÒºñÇÏ°í ³²Àº °æÇèÄ¡´Â ´ÙÀ½ ·¹º§·Î ³Ñ±ä´Ù.
            nextExp -= requiredExp;
            nextLevel++;
        }

        // ½ÇÁ¦ º¯°æÀÌ ¾øÀ¸¸é ÆÄÀÏÀ» ´Ù½Ã ÀúÀåÇÏÁö ¾Ê´Â´Ù.
        if (nextLevel == _playerSaveData.userLevel &&
            nextExp == _playerSaveData.userExp)
        {
            return;
        }

        // ÀúÀå ½ÇÆĞ ½Ã º¹±¸ÇÒ ÀÌÀü °ªÀ» º¸°üÇÑ´Ù.
        int previousLevel = _playerSaveData.userLevel;
        int previousExp = _playerSaveData.userExp;

        _playerSaveData.userLevel = nextLevel;
        _playerSaveData.userExp = (int)nextExp;

        try
        {
            // ±âÁ¸ ÀúÀå Èå¸§¿¡¼­ Äù½ºÆ® °Ë»ç¿Í User UI °»½Åµµ Ã³¸®ÇÑ´Ù.
            SaveToFile();
        }
        catch (Exception exception)
        {
            // ÀúÀå¿¡ ½ÇÆĞÇÏ¸é ¸Ş¸ğ¸® °ªµµ º¯°æ ÀüÀ¸·Î µÇµ¹¸°´Ù.
            _playerSaveData.userLevel = previousLevel;
            _playerSaveData.userExp = previousExp;

            Debug.LogException(exception, this);
            return;
        }

        Debug.Log(
            $"User Å×½ºÆ® ¿Ï·á: ·¹º§ {nextLevel}, °æÇèÄ¡ {nextExp}",
            this);
    }

    // »õ Ä³¸¯ÅÍÀÇ º¸À¯¡¤ÀåÂø µ¥ÀÌÅÍ¸¦ ¸¸µç´Ù.
    // ±âÁ¸ Ä³¸¯ÅÍÀÇ ÀåÂø »óÅÂ¸¦ ÃÊ±âÈ­ÇÒ ¶§´Â »ç¿ëÇÏÁö ¾Ê´Â´Ù.
    private CharacterLoadoutSaveData CreateInitialLoadout(
        CharacterData character)
    {
        if (character == null || string.IsNullOrWhiteSpace(character.Id))
        {
            throw new InvalidOperationException(
                "ÃÊ±â Àåºñ¸¦ Áö±ŞÇÒ CharacterData¿Í ID¸¦ È®ÀÎÇØÁÖ¼¼¿ä.");
        }

        WeaponData[] weapons = character.InitialEquippedWeapons;

        // ½ÃÀÛ Àåºñ´Â Á¤È®È÷ 3Á¾À» ÁöÁ¤ÇÑ´Ù.
        if (weapons == null || weapons.Length != 3)
        {
            throw new InvalidOperationException(
                $"{character.CharacterName}ÀÇ Initial Equipped Weapons¸¦ 3°³ ÁöÁ¤ÇØÁÖ¼¼¿ä.");
        }

        CharacterLoadoutSaveData loadout = new CharacterLoadoutSaveData
        {
            characterId = character.Id
        };

        HashSet<string> weaponIds = new HashSet<string>();

        for (int i = 0; i < weapons.Length; i++)
        {
            WeaponData weapon = weapons[i];

            // Àß¸øµÈ ¼³Á¤ÀÌ³ª °°Àº ¹«±âÀÇ Áßº¹ Áö±ŞÀ» ¸·´Â´Ù.
            if (weapon == null ||
                string.IsNullOrWhiteSpace(weapon.Id) ||
                !weaponIds.Add(weapon.Id))
            {
                throw new InvalidOperationException(
                    $"{character.CharacterName}ÀÇ ½ÃÀÛ ¹«±â {i + 1}¹øÀ» È®ÀÎÇØÁÖ¼¼¿ä. " +
                    "¼­·Î ´Ù¸¥ ¹«±â 3Á¾ÀÌ ÇÊ¿äÇÕ´Ï´Ù.");
            }

            // Áö±ŞÇÑ ¹«±â¸¦ Owned »óÅÂ·Î ±â·ÏÇÑ´Ù.
            loadout.ownedWeaponIds.Add(weapon.Id);

            // Áö±Ş°ú µ¿½Ã¿¡ °°Àº ¼ø¼­ÀÇ ½½·Ô¿¡ ÀåÂøÇÑ´Ù.
            loadout.equippedWeaponIds[i] = weapon.Id;
        }

        return loadout;
    }

    // Äù½ºÆ® º¸»ó ÀüÃ¼¿Í ¼ö·É ¿Ï·á »óÅÂ¸¦ ÇÑ ¹ø¿¡ ÀúÀåÇÑ´Ù.
    public bool TryClaimQuestReward(QuestData quest, out string error)
    {
        error = string.Empty;

        // ¸ñÇ¥¸¦ ´Ş¼ºÇß°í ¾ÆÁ÷ º¸»óÀ» ¹ŞÁö ¾ÊÀº °æ¿ì¸¸ Çã¿ëÇÑ´Ù.
        if (Instance != this || !IsLoggedIn ||
            QuestService.GetState(quest) != QuestState.ReadyToTurnIn)
        {
            error = "Áö±İÀº º¸»óÀ» ¹ŞÀ» ¼ö ¾ø´Â Äù½ºÆ®ÀÔ´Ï´Ù.";
            return false;
        }

        // ½ÇÁ¦ ÀúÀåÇÒ Äù½ºÆ® ±â·ÏÀ» Ã£´Â´Ù.
        QuestSaveData record = _playerSaveData.questRecords?.Find(
            item => item != null && item.questId == quest.Id);

        if (record == null)
        {
            error = "Äù½ºÆ® ÁøÇà ±â·ÏÀ» Ã£À» ¼ö ¾ø½À´Ï´Ù.";
            return false;
        }

        // ¿©·¯ Ç×¸ñÀ» ´õÇÒ ¶§ int ¹üÀ§¸¦ ³ÑÁö ¾Êµµ·Ï longÀ¸·Î °è»êÇÑ´Ù.
        long goldReward = 0;
        long expReward = 0;

        if (quest.Rewards != null)
        {
            foreach (QuestRewardData reward in quest.Rewards)
            {
                // Àß¸øµÈ Ç×¸ñÀº ÀÏºÎ¸¸ Áö±ŞÇÏÁö ¾Ê°í ÀüÃ¼ Áö±ŞÀ» Áß´ÜÇÑ´Ù.
                if (reward == null || reward.Amount <= 0)
                {
                    error = "Äù½ºÆ® º¸»ó ¼³Á¤À» È®ÀÎÇØÁÖ¼¼¿ä.";
                    return false;
                }

                // °°Àº Á¾·ù°¡ ¿©·¯ ¹ø µé¾î ÀÖ¾îµµ ÇÕ»êÇØ¼­ Áö±ŞÇÑ´Ù.
                switch (reward.Type)
                {
                    case QuestRewardType.UserExp:
                        expReward += reward.Amount;
                        break;

                    case QuestRewardType.Gold:
                        goldReward += reward.Amount;
                        break;

                    default:
                        error = "¾ÆÁ÷ Áö¿øÇÏÁö ¾Ê´Â º¸»ó Á¾·ùÀÔ´Ï´Ù.";
                        return false;
                }
            }
        }

        // ÀúÀå µ¥ÀÌÅÍ¸¦ º¯°æÇÏ±â Àü¿¡ ÃÖÁ¾ °ñµå¸¦ °è»êÇÑ´Ù.
        long nextGold = (long)_playerSaveData.gold + goldReward;

        if (nextGold > int.MaxValue)
        {
            error = "º¸À¯ °¡´ÉÇÑ °ñµå ¹üÀ§¸¦ ÃÊ°úÇÕ´Ï´Ù.";
            return false;
        }

        // °æÇèÄ¡ º¸»óÀ¸·Î ¿©·¯ ·¹º§À» ¿À¸¦ ¼ö ÀÖµµ·Ï °è»êÇÑ´Ù.
        int nextLevel = _playerSaveData.userLevel;
        long nextExp = (long)_playerSaveData.userExp + expReward;

        if (expReward > 0)
        {
            while (true)
            {
                // ÇöÀç ÇÁ·ÎÁ§Æ®ÀÇ °æÇèÄ¡ Å×½ºÆ®¿Í °°Àº Á¤Ã¥:
                // ÇÊ¿äÇÑ ·¹º§ µ¥ÀÌÅÍ°¡ ¾øÀ¸¸é Áö±ŞÀ» Áß´ÜÇÑ´Ù.
                if (_userLevelData == null ||
                    !_userLevelData.TryGetRequiredExp(
                        nextLevel, out int requiredExp) ||
                    requiredExp <= 0)
                {
                    error = "ÇØ´ç À¯Àú ·¹º§ÀÇ ÇÊ¿ä °æÇèÄ¡¸¦ ¼³Á¤ÇØÁÖ¼¼¿ä.";
                    return false;
                }

                // ´ÙÀ½ ·¹º§¿¡ µµ´ŞÇÏÁö ¾Ê¾Ò´Ù¸é ³²Àº °æÇèÄ¡¸¦ À¯ÁöÇÑ´Ù.
                if (nextExp < requiredExp)
                {
                    break;
                }

                if (nextLevel == int.MaxValue)
                {
                    error = "´õ ÀÌ»ó À¯Àú ·¹º§À» ¿Ã¸± ¼ö ¾ø½À´Ï´Ù.";
                    return false;
                }

                // ·¹º§¾÷¿¡ »ç¿ëÇÑ °æÇèÄ¡¸¦ »©°í ´ÙÀ½ ·¹º§À» °è»êÇÑ´Ù.
                nextExp -= requiredExp;
                nextLevel++;
            }
        }

        // ÀúÀå ½ÇÆĞ ½Ã µÇµ¹¸± °ªÀ» º¸°üÇÑ´Ù.
        int previousGold = _playerSaveData.gold;
        int previousLevel = _playerSaveData.userLevel;
        int previousExp = _playerSaveData.userExp;
        QuestState previousState = record.state;

        // º¸»ó°ú ¼ö·É ¿Ï·á »óÅÂ¸¦ °°Àº ÀúÀå¿¡ Æ÷ÇÔÇÑ´Ù.
        _playerSaveData.gold = (int)nextGold;
        _playerSaveData.userLevel = nextLevel;
        _playerSaveData.userExp = (int)nextExp;
        record.state = QuestState.RewardClaimed;

        try
        {
            // ±âÁ¸ ÀúÀå °æ·Î¸¦ »ç¿ëÇÑ´Ù.
            // À¯Àú »óÅÂ UI °»½Å°ú ´Ù¸¥ Äù½ºÆ®ÀÇ ¸ñÇ¥ °Ë»çµµ ¼öÇàµÈ´Ù.
            SaveToFile();
        }
        catch (Exception exception)
        {
            // ÀúÀå¿¡ ½ÇÆĞÇÏ¸é ¸ğµç º¯°æÀ» µÇµ¹·Á Àç½Ãµµ¸¦ Çã¿ëÇÑ´Ù.
            _playerSaveData.gold = previousGold;
            _playerSaveData.userLevel = previousLevel;
            _playerSaveData.userExp = previousExp;
            record.state = previousState;

            Debug.LogException(exception, this);
            error = "º¸»óÀ» ÀúÀåÇÏÁö ¸øÇß½À´Ï´Ù. ´Ù½Ã ½ÃµµÇØÁÖ¼¼¿ä.";
            return false;
        }

        // ÀÌ¹Ì ÀúÀåµÈ °á°ú¸¦ ¾Ë¸®´Â ´Ü°èÀÌ¹Ç·Î,
        // UI ¿À·ù°¡ º¸»ó Áö±Ş ½ÇÆĞ·Î Ã³¸®µÇÁö ¾Êµµ·Ï °³º° º¸È£ÇÑ´Ù.
        if (previousGold != _playerSaveData.gold)
        {
            Action<int> handlers = GoldChanged;

            if (handlers != null)
            {
                foreach (Action<int> handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler.Invoke(_playerSaveData.gold);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);
                    }
                }
            }
        }

        // ±âÁ¸ ¾Ë¸² ¸Ş¼­µå¸¦ Àç»ç¿ëÇØ Äù½ºÆ® ¸ñ·Ïµµ °»½ÅÇÑ´Ù.
        QuestService.NotifyProgressChanges(
            new List<QuestSaveData> { record });

        return true;
    }

    // User °æÇèÄ¡¸¦ Áö±ŞÇÏ°í ·¹º§¾÷ °á°ú¸¦ Áï½Ã ÀúÀåÇÑ´Ù.
    public void AddUserExperience(int amount)
    {
        // Áö±Ş·®ÀÌ ¾ø´Â °æ¿ì ¾Æ¹«°Íµµ º¯°æÇÏÁö ¾Ê´Â´Ù.
        if (amount <= 0)
        {
            return;
        }

        // ½ÇÁ¦ ·Î±×ÀÎÇÑ ÀúÀå °ü¸®ÀÚ¿¡¼­¸¸ º¸»óÀ» Áö±ŞÇÑ´Ù.
        if (Instance != this || !IsLoggedIn)
        {
            throw new InvalidOperationException(
                "·Î±×ÀÎÇÑ »ç¿ëÀÚ¿¡°Ô¸¸ User °æÇèÄ¡¸¦ Áö±ŞÇÒ ¼ö ÀÖ½À´Ï´Ù.");
        }

        if (_userLevelData == null)
        {
            throw new InvalidOperationException(
                "SaveManagerÀÇ User Level Data¸¦ ¿¬°áÇØÁÖ¼¼¿ä.");
        }

        // °è»êÀÌ ³¡³ª±â Àü¿¡´Â ½ÇÁ¦ ÀúÀå µ¥ÀÌÅÍ¸¦ º¯°æÇÏÁö ¾Ê´Â´Ù.
        int nextLevel = _playerSaveData.userLevel;
        long nextExp = (long)_playerSaveData.userExp + amount;

        // ÇÑ ¹øÀÇ º¸»óÀ¸·Î ¿©·¯ ·¹º§À» ¿Ã¸± ¼ö ÀÖµµ·Ï ¹İº¹ °è»êÇÑ´Ù.
        while (true)
        {
            // ±âÁ¸ Äù½ºÆ® º¸»ó°ú µ¿ÀÏÇÏ°Ô ÇÊ¿äÇÑ ·¹º§ µ¥ÀÌÅÍ°¡ ÀÖ¾î¾ß ÇÑ´Ù.
            if (!_userLevelData.TryGetRequiredExp(
                    nextLevel, out int requiredExp) ||
                requiredExp <= 0)
            {
                throw new InvalidOperationException(
                    $"User {nextLevel}·¹º§ÀÇ ÇÊ¿ä °æÇèÄ¡¸¦ ¼³Á¤ÇØÁÖ¼¼¿ä.");
            }

            // ·¹º§¾÷¿¡ ºÎÁ·ÇÑ °æÇèÄ¡´Â ÇöÀç ·¹º§¿¡ ³²±ä´Ù.
            if (nextExp < requiredExp)
            {
                break;
            }

            if (nextLevel == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "´õ ÀÌ»ó User ·¹º§À» ¿Ã¸± ¼ö ¾ø½À´Ï´Ù.");
            }

            // ÇÊ¿äÇÑ °æÇèÄ¡¸¦ ¼ÒºñÇÏ°í ´ÙÀ½ ·¹º§·Î ³Ñ±ä´Ù.
            nextExp -= requiredExp;
            nextLevel++;
        }

        // ÀúÀå ½ÇÆĞ ½Ã µÇµ¹¸± ÀÌÀü °ªÀ» º¸°üÇÑ´Ù.
        int previousLevel = _playerSaveData.userLevel;
        int previousExp = _playerSaveData.userExp;

        _playerSaveData.userLevel = nextLevel;
        _playerSaveData.userExp = (int)nextExp;

        try
        {
            // ±âÁ¸ ÀúÀå Èå¸§À» ÅëÇØ ÆÄÀÏ ÀúÀå°ú User UI °»½ÅÀ» Ã³¸®ÇÑ´Ù.
            SaveToFile();
        }
        catch
        {
            // ÆÄÀÏ ÀúÀå¿¡ ½ÇÆĞÇÏ¸é ¸Ş¸ğ¸®ÀÇ º¸»óµµ Ãë¼ÒÇÑ´Ù.
            _playerSaveData.userLevel = previousLevel;
            _playerSaveData.userExp = previousExp;
            throw;
        }
    }

    // ÇöÀç Äù½ºÆ® »óÅÂ¿Í º¸À¯ ¼ö·®À¸·Î µå¶ø Çã¿ë ¿©ºÎ¸¦ °è»êÇÑ´Ù.
    public bool CanDropItem(Item item)
    {
        if (!IsLoggedIn ||
            item == null ||
            string.IsNullOrWhiteSpace(item.Id))
        {
            return false;
        }

        // ÀÏ¹İ ¾ÆÀÌÅÛÀº Äù½ºÆ® Á¶°ÇÀ¸·Î Á¦ÇÑÇÏÁö ¾Ê´Â´Ù.
        if (item.Type != ItemType.Quest)
        {
            return true;
        }

        // Äù½ºÆ® ¾ÆÀÌÅÛÀº ¾ÆÁ÷ ÇÊ¿äÇÑ °æ¿ì¿¡¸¸ Çã¿ëÇÑ´Ù.
        return QuestService.IsItemNeeded(
            _questDatabase,
            _playerSaveData,
            item.Id);
    }

    // ¾ÆÀÌÅÛ ¼ö·®À» Ãß°¡ÇÏ°í Äù½ºÆ® ÁøÇà »óÅÂ¿Í ÇÔ²² ÀúÀåÇÑ´Ù.
    public bool TryAddItem(Item item, int amount, out string error)
    {
        error = string.Empty;

        if (!IsLoggedIn ||
            item == null ||
            string.IsNullOrWhiteSpace(item.Id) ||
            amount <= 0)
        {
            error = "¾ÆÀÌÅÛ È¹µæ Á¤º¸°¡ ¿Ã¹Ù¸£Áö ¾Ê½À´Ï´Ù.";
            return false;
        }

        // ÀÌÀü ÀúÀå ÆÄÀÏ¿¡´Â ÀÎº¥Åä¸® ¸ñ·ÏÀÌ ¾øÀ» ¼ö ÀÖ´Ù.
        bool createdList = _playerSaveData.inventoryItems == null;

        if (createdList)
        {
            _playerSaveData.inventoryItems =
                new List<InventoryItemSaveData>();
        }

        List<InventoryItemSaveData> entries =
            _playerSaveData.inventoryItems;

        InventoryItemSaveData entry = entries.Find(
            saved => saved != null && saved.itemId == item.Id);

        bool createdEntry = entry == null;

        // Ã³À½ È¹µæÇÑ ¾ÆÀÌÅÛÀÌ¸é ÀúÀå Ç×¸ñÀ» ¸¸µç´Ù.
        if (createdEntry)
        {
            entry = new InventoryItemSaveData
            {
                itemId = item.Id,
                count = 0
            };

            entries.Add(entry);
        }

        // ÀúÀå ½ÇÆĞ ½Ã º¹¿øÇÒ ±âÁ¸ ¼ö·®ÀÌ´Ù.
        int previousCount = entry.count;

        try
        {
            // Á¤¼ö ¹üÀ§¸¦ ÃÊ°úÇÏ¸é Àß¸øµÈ ¼ö·®À» ÀúÀåÇÏÁö ¾Ê´Â´Ù.
            entry.count = checked(previousCount + amount);

            // ¾ÆÀÌÅÛ ¼ö·®°ú ¸ñÇ¥¸¦ ´Ş¼ºÇÑ Äù½ºÆ® »óÅÂ¸¦ ÇÔ²² ÀúÀåÇÑ´Ù.
            SaveToFile();
        }
        catch (Exception exception)
        {
            // ÀúÀå ½ÇÆĞ ½Ã ÀÎº¥Åä¸®µµ ÀÌÀü »óÅÂ·Î º¹¿øÇÑ´Ù.
            entry.count = previousCount;

            if (createdEntry)
            {
                entries.Remove(entry);
            }

            if (createdList)
            {
                _playerSaveData.inventoryItems = null;
            }

            Debug.LogException(exception, this);
            error = "¾ÆÀÌÅÛ È¹µæÀ» ÀúÀåÇÏÁö ¸øÇß½À´Ï´Ù.";
            return false;
        }

        return true;
    }

}