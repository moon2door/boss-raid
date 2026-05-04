using System;
using System.Collections.Generic;
using UnityEngine;

namespace BossRaid
{
    public sealed class BossRaidStateStore : MonoBehaviour
    {
        public event Action<BossRaidState> StateChanged;

        public BossRaidState Current { get; private set; }

        private void Awake()
        {
            Current = CreatePreviewState();
        }

        public void ApplyJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                var next = JsonUtility.FromJson<BossRaidState>(json);
                if (next == null)
                {
                    Debug.LogWarning("BossRaidStateStore received empty state json.");
                    return;
                }

                Normalize(next);
                Current = next;
                StateChanged?.Invoke(Current);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"BossRaidStateStore failed to parse state json: {exception.Message}");
            }
        }

        public void ApplyPreviewState(BossRaidState state)
        {
            Normalize(state);
            Current = state;
            StateChanged?.Invoke(Current);
        }

        public void Notify()
        {
            StateChanged?.Invoke(Current);
        }

        public static BossRaidState CreatePreviewState()
        {
            var state = new BossRaidState
            {
                eventTitle = "BOSS RAID",
                screen = BossRaidScreens.Standby,
                difficulty = BossRaidDifficulties.Normal,
                bossHp = 1400000,
                prizePool = 0,
                burgerCount = 0,
                burgerMissCount = 0,
                clearCount = 0,
                failCount = 0,
                roundIndex = 0,
                selectedMode = "Aim",
                selectedMapId = "aim-1",
                connectionLabel = "LOCAL PREVIEW",
                teams = new List<BossRaidTeam>
                {
                    new BossRaidTeam { id = "team-1", name = "Team A", score = 0, color = new Color(0.95f, 0.25f, 0.2f) },
                    new BossRaidTeam { id = "team-2", name = "Team B", score = 0, color = new Color(0.2f, 0.55f, 0.95f) },
                    new BossRaidTeam { id = "team-3", name = "Team C", score = 0, color = new Color(0.25f, 0.85f, 0.45f) },
                    new BossRaidTeam { id = "team-4", name = "Team D", score = 0, color = new Color(0.95f, 0.75f, 0.2f) }
                },
                difficulties = new List<BossRaidDifficultyConfig>
                {
                    new BossRaidDifficultyConfig { id = BossRaidDifficulties.Easy, label = "Easy", bossHp = 1000000, prize = 3000 },
                    new BossRaidDifficultyConfig { id = BossRaidDifficulties.Normal, label = "Normal", bossHp = 1400000, prize = 5000 },
                    new BossRaidDifficultyConfig { id = BossRaidDifficulties.Hard, label = "Hard", bossHp = 2000000, prize = 15000 }
                }
            };

            var modes = new[] { "Aim", "Speed", "Tech", "Stamina" };
            foreach (var mode in modes)
            {
                for (var i = 1; i <= 6; i++)
                {
                    var id = $"{mode.ToLowerInvariant()}-{i}";
                    state.mapPool.Add(new BossRaidMap
                    {
                        id = id,
                        mode = mode,
                        title = $"{mode} Raid #{i}",
                        artist = "Map Pool",
                        mapper = "Staff",
                        difficultyName = $"Set {i}",
                        link = "",
                        isBurger = i == 2 || i == 5,
                        played = false
                    });

                    state.selectedRoundMapIds.Add(id);
                }
            }

            Normalize(state);
            return state;
        }

        public static void Normalize(BossRaidState state)
        {
            if (state.teams == null)
            {
                state.teams = new List<BossRaidTeam>();
            }

            if (state.mapPool == null)
            {
                state.mapPool = new List<BossRaidMap>();
            }

            if (state.selectedRoundMapIds == null)
            {
                state.selectedRoundMapIds = new List<string>();
            }

            if (state.difficulties == null || state.difficulties.Count == 0)
            {
                state.difficulties = new List<BossRaidDifficultyConfig>
                {
                    new BossRaidDifficultyConfig { id = BossRaidDifficulties.Easy, label = "Easy", bossHp = 1000000, prize = 3000 },
                    new BossRaidDifficultyConfig { id = BossRaidDifficulties.Normal, label = "Normal", bossHp = 1400000, prize = 5000 },
                    new BossRaidDifficultyConfig { id = BossRaidDifficulties.Hard, label = "Hard", bossHp = 2000000, prize = 15000 }
                };
            }

            var difficulty = state.CurrentDifficulty;
            if (difficulty != null)
            {
                state.bossHp = difficulty.bossHp;
            }

            state.totalScore = 0;
            for (var i = 0; i < state.teams.Count; i++)
            {
                state.totalScore += Mathf.Max(0, state.teams[i].score);
            }
        }
    }
}
