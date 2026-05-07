using System;
using System.Collections.Generic;
using UnityEngine;

namespace BossRaid
{
    [Serializable]
    public class BossRaidState
    {
        public int version = 1;
        public int animationNonce = 0;
        public string eventTitle = "BOSS RAID";
        public string screen = BossRaidScreens.Standby;
        public string currentTeamId = "team-1";
        public string difficulty = BossRaidDifficulties.Normal;
        public int bossHp = 1400000;
        public int totalScore = 0;
        public int prizePool = 0;
        public int burgerCount = 0;
        public int burgerMissCount = 0;
        public int clearCount = 0;
        public int failCount = 0;
        public int roundIndex = 0;
        public string selectedMode = "";
        public string selectedMapId = "";
        public string lastResult = BossRaidResults.None;
        public string resultMessage = "";
        public string connectionLabel = "LOCAL PREVIEW";
        public string chatStatus = "LOCAL CHAT";
        public List<BossRaidTeam> teams = new List<BossRaidTeam>();
        public List<BossRaidMap> mapPool = new List<BossRaidMap>();
        public List<string> selectedRoundMapIds = new List<string>();
        public List<BossRaidDifficultyConfig> difficulties = new List<BossRaidDifficultyConfig>();
        public List<BossRaidChatMessage> chatMessages = new List<BossRaidChatMessage>();

        public BossRaidTeam CurrentTeam
        {
            get
            {
                for (var i = 0; i < teams.Count; i++)
                {
                    if (teams[i].id == currentTeamId)
                    {
                        return teams[i];
                    }
                }

                return teams.Count > 0 ? teams[0] : null;
            }
        }

        public BossRaidMap SelectedMap
        {
            get
            {
                for (var i = 0; i < mapPool.Count; i++)
                {
                    if (mapPool[i].id == selectedMapId)
                    {
                        return mapPool[i];
                    }
                }

                return null;
            }
        }

        public BossRaidDifficultyConfig CurrentDifficulty
        {
            get
            {
                for (var i = 0; i < difficulties.Count; i++)
                {
                    if (difficulties[i].id == difficulty)
                    {
                        return difficulties[i];
                    }
                }

                return difficulties.Count > 0 ? difficulties[0] : null;
            }
        }
    }

    [Serializable]
    public class BossRaidTeam
    {
        public string id;
        public string name;
        public int score;
        public Color color = Color.white;
    }

    [Serializable]
    public class BossRaidMap
    {
        public string id;
        public string mode;
        public string title;
        public string artist;
        public string mapper;
        public string difficultyName;
        public string link;
        public bool isBurger;
        public bool played;
    }

    [Serializable]
    public class BossRaidDifficultyConfig
    {
        public string id;
        public string label;
        public int bossHp;
        public int prize;
    }

    [Serializable]
    public class BossRaidChatMessage
    {
        public string time;
        public string sender;
        public string message;
        public string kind;
    }

    public static class BossRaidScreens
    {
        public const string Standby = "standby";
        public const string BurgerReveal = "burgerReveal";
        public const string RouletteMode = "rouletteMode";
        public const string RouletteMap = "rouletteMap";
        public const string DifficultySelect = "difficultySelect";
        public const string MapReady = "mapReady";
        public const string InGame = "inGame";
        public const string Result = "result";
    }

    public static class BossRaidDifficulties
    {
        public const string Easy = "easy";
        public const string Normal = "normal";
        public const string Hard = "hard";
    }

    public static class BossRaidResults
    {
        public const string None = "none";
        public const string Clear = "clear";
        public const string Fail = "fail";
    }
}
