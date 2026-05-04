using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BossRaid
{
    [RequireComponent(typeof(BossRaidStateStore))]
    [RequireComponent(typeof(BossRaidWebSocketClient))]
    [RequireComponent(typeof(BossRaidOverlayView))]
    public sealed class BossRaidOverlayApp : MonoBehaviour
    {
        [SerializeField] private bool enableKeyboardPreview = true;
        [SerializeField] private float rouletteMinStepSeconds = 0.045f;
        [SerializeField] private float rouletteMaxStepSeconds = 0.42f;
        [SerializeField] private float rouletteAutoAdvanceDelaySeconds = 3f;
        [SerializeField] private float burgerRouletteMinStepSeconds = 0.025f;
        [SerializeField] private float burgerRouletteMaxStepSeconds = 0.22f;
        [SerializeField] private float burgerRouletteMinDurationSeconds = 4.2f;
        [SerializeField] private float burgerRouletteMaxDurationSeconds = 7.4f;
        [SerializeField] private int burgerPickCount = 8;

        private BossRaidStateStore stateStore;
        private BossRaidWebSocketClient bridgeClient;
        private Coroutine rouletteRoutine;

        private sealed class BurgerCursor
        {
            public string targetId;
            public string currentId;
            public float stopAt;
            public float nextStepAt;
            public bool locked;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<BossRaidOverlayApp>() != null)
            {
                return;
            }

            var app = new GameObject("BossRaidOverlayApp");
            DontDestroyOnLoad(app);
            app.AddComponent<BossRaidOverlayApp>();
        }

        private void Awake()
        {
            stateStore = GetComponent<BossRaidStateStore>();
            bridgeClient = GetComponent<BossRaidWebSocketClient>();
        }

        private void Update()
        {
            if (!enableKeyboardPreview || stateStore == null || stateStore.Current == null)
            {
                return;
            }

            if (IsControlPressed())
            {
                if (GetNumberDown(KeyCode.Alpha1, KeyCode.Keypad1))
                {
                    StopRoulette();
                    SetScreen(BossRaidScreens.Standby);
                }
                else if (GetNumberDown(KeyCode.Alpha9, KeyCode.Keypad9))
                {
                    StopRoulette();
                    ShowBurgerReveal();
                }
                else if (GetNumberDown(KeyCode.Alpha2, KeyCode.Keypad2))
                {
                    StopRoulette();
                    EnterDifficultySelect();
                }
                else if (GetNumberDown(KeyCode.Alpha3, KeyCode.Keypad3))
                {
                    StopRoulette();
                    ToggleMapReadyAndInGame();
                }
                else if (GetNumberDown(KeyCode.Alpha4, KeyCode.Keypad4))
                {
                    StopRoulette();
                    EnterDifficultySelect();
                }

                return;
            }

            if (stateStore.Current.screen == BossRaidScreens.DifficultySelect && HandleDifficultyInput())
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F1))
            {
                SetScreen(BossRaidScreens.Standby);
            }
            else if (Input.GetKeyDown(KeyCode.F2))
            {
                AssignPreviewBurgers();
            }
            else if (Input.GetKeyDown(KeyCode.F3))
            {
                SpinPreviewMode();
            }
            else if (Input.GetKeyDown(KeyCode.F4))
            {
                SpinPreviewMap();
            }
            else if (Input.GetKeyDown(KeyCode.F5))
            {
                SetScreen(BossRaidScreens.MapReady);
            }
            else if (Input.GetKeyDown(KeyCode.F9))
            {
                EnterDifficultySelect();
            }
            else if (Input.GetKeyDown(KeyCode.F6))
            {
                StartPreviewMap();
            }
            else if (Input.GetKeyDown(KeyCode.F7))
            {
                FinishPreviewMap(true);
            }
            else if (Input.GetKeyDown(KeyCode.F8))
            {
                FinishPreviewMap(false);
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                HandleSpace();
            }
        }

        private void SetScreen(string screen)
        {
            var state = stateStore.Current;
            state.screen = screen;
            Bump(state);
            ApplyLocalState(state);
            SendCommand("set_screen", JsonStringField("screen", screen));
        }

        private void AssignPreviewBurgers()
        {
            var state = stateStore.Current;
            var burgerIds = PickBurgerMapIds(state, Mathf.Min(burgerPickCount, state.mapPool.Count));
            ApplyBurgerSelection(state, burgerIds, new List<string>());

            state.screen = BossRaidScreens.BurgerReveal;
            state.selectedMapId = "";
            Bump(state);
            ApplyLocalState(state, 0.8f);
            SendCommand("set_burger_maps", JsonStringArrayField("mapIds", burgerIds), 0.8f);
        }

        private void ShowBurgerReveal()
        {
            SetScreen(BossRaidScreens.BurgerReveal);
        }

        private void SpinPreviewMode()
        {
            var state = stateStore.Current;
            var modes = GetModes(state);

            if (modes.Count > 0)
            {
                state.selectedMode = modes[Random.Range(0, modes.Count)];
            }

            state.screen = BossRaidScreens.RouletteMode;
            Bump(state);
            ApplyLocalState(state);
            SendCommand("spin_mode");
        }

        private void EnterModeRoulette()
        {
            var state = stateStore.Current;
            state.screen = BossRaidScreens.RouletteMode;
            state.selectedMode = "";
            state.selectedMapId = "";
            state.lastResult = BossRaidResults.None;
            state.resultMessage = "";
            Bump(state);
            ApplyLocalState(state);
            SendCommand("enter_mode_roulette");
        }

        private void SpinPreviewMap()
        {
            var state = stateStore.Current;
            var candidates = GetMapCandidates(state, true);

            if (candidates.Count > 0)
            {
                var selected = candidates[Random.Range(0, candidates.Count)];
                state.selectedMapId = selected.id;
                state.selectedMode = selected.mode;
            }

            state.screen = BossRaidScreens.RouletteMap;
            Bump(state);
            ApplyLocalState(state);
            SendCommand("spin_map");
        }

        private void ToggleMapReadyAndInGame()
        {
            var state = stateStore.Current;
            var nextScreen = state.screen == BossRaidScreens.MapReady ? BossRaidScreens.InGame : BossRaidScreens.MapReady;
            state.screen = nextScreen;
            state.lastResult = BossRaidResults.None;
            state.resultMessage = "";
            Bump(state);
            ApplyLocalState(state);
            SendCommand(nextScreen == BossRaidScreens.InGame ? "start_map" : "ready_map");
        }

        private void EnterDifficultySelect()
        {
            var state = stateStore.Current;
            state.screen = BossRaidScreens.DifficultySelect;
            state.selectedMode = "";
            state.selectedMapId = "";
            state.lastResult = BossRaidResults.None;
            state.resultMessage = "";
            Bump(state);
            ApplyLocalState(state);
            SendCommand("enter_difficulty_select");
        }

        private bool HandleDifficultyInput()
        {
            if (GetNumberDown(KeyCode.Alpha1, KeyCode.Keypad1))
            {
                SelectDifficultyByIndex(0);
                return true;
            }

            if (GetNumberDown(KeyCode.Alpha2, KeyCode.Keypad2))
            {
                SelectDifficultyByIndex(1);
                return true;
            }

            if (GetNumberDown(KeyCode.Alpha3, KeyCode.Keypad3))
            {
                SelectDifficultyByIndex(2);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                SelectDifficultyById(BossRaidDifficulties.Easy);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.N))
            {
                SelectDifficultyById(BossRaidDifficulties.Normal);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                SelectDifficultyById(BossRaidDifficulties.Hard);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                CycleDifficulty(-1);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                CycleDifficulty(1);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                ConfirmDifficulty();
                return true;
            }

            return false;
        }

        private void SelectDifficultyByIndex(int index)
        {
            var state = stateStore.Current;
            if (state.difficulties == null || index < 0 || index >= state.difficulties.Count)
            {
                return;
            }

            SelectDifficultyById(state.difficulties[index].id);
        }

        private void SelectDifficultyById(string difficultyId)
        {
            var state = stateStore.Current;
            if (state.difficulties == null || state.difficulties.Count == 0)
            {
                return;
            }

            var found = false;
            for (var i = 0; i < state.difficulties.Count; i++)
            {
                if (state.difficulties[i].id == difficultyId)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return;
            }

            state.difficulty = difficultyId;
            state.screen = BossRaidScreens.DifficultySelect;
            Bump(state);
            ApplyLocalState(state);
            SendCommand("set_difficulty", JsonStringField("difficulty", difficultyId));
        }

        private void CycleDifficulty(int direction)
        {
            var state = stateStore.Current;
            if (state.difficulties == null || state.difficulties.Count == 0)
            {
                return;
            }

            var index = 0;
            for (var i = 0; i < state.difficulties.Count; i++)
            {
                if (state.difficulties[i].id == state.difficulty)
                {
                    index = i;
                    break;
                }
            }

            index = (index + direction + state.difficulties.Count) % state.difficulties.Count;
            SelectDifficultyById(state.difficulties[index].id);
        }

        private void ConfirmDifficulty()
        {
            var state = stateStore.Current;
            state.screen = BossRaidScreens.RouletteMode;
            state.selectedMode = "";
            state.selectedMapId = "";
            state.lastResult = BossRaidResults.None;
            state.resultMessage = "";
            Bump(state);
            ApplyLocalState(state);
            SendCommand("confirm_difficulty", JsonStringField("difficulty", state.difficulty));
        }

        private void HandleSpace()
        {
            if (rouletteRoutine != null)
            {
                return;
            }

            var screen = stateStore.Current.screen;
            if (screen == BossRaidScreens.RouletteMode)
            {
                rouletteRoutine = StartCoroutine(RollModeRoulette());
            }
            else if (screen == BossRaidScreens.RouletteMap)
            {
                rouletteRoutine = StartCoroutine(RollMapRoulette());
            }
            else if (screen == BossRaidScreens.BurgerReveal)
            {
                rouletteRoutine = StartCoroutine(RollBurgerReveal());
            }
            else if (screen == BossRaidScreens.DifficultySelect)
            {
                ConfirmDifficulty();
            }
            else if (screen == BossRaidScreens.Result)
            {
                NextPreviewRound();
            }
        }

        private IEnumerator RollModeRoulette()
        {
            var modes = GetModes(stateStore.Current);
            if (modes.Count == 0)
            {
                rouletteRoutine = null;
                yield break;
            }

            var ticks = Random.Range(26, 38);
            var index = Random.Range(0, modes.Count);
            for (var tick = 0; tick < ticks; tick++)
            {
                var state = stateStore.Current;
                state.screen = BossRaidScreens.RouletteMode;
                state.selectedMode = modes[index % modes.Count];
                state.selectedMapId = "";
                Bump(state);
                ApplyLocalState(state, 0.9f);

                index += 1;
                yield return new WaitForSeconds(GetRouletteDelay(tick, ticks));
            }

            var finalMode = stateStore.Current.selectedMode;
            bridgeClient?.HoldIncomingState(rouletteAutoAdvanceDelaySeconds + 1.2f);
            yield return new WaitForSeconds(rouletteAutoAdvanceDelaySeconds);

            var finalState = stateStore.Current;
            finalState.screen = BossRaidScreens.RouletteMap;
            finalState.selectedMode = finalMode;
            finalState.selectedMapId = "";
            Bump(finalState);
            ApplyLocalState(finalState);
            SendCommand("complete_mode_roulette", JsonStringField("mode", finalMode));
            rouletteRoutine = null;
        }

        private IEnumerator RollBurgerReveal()
        {
            var initialState = stateStore.Current;
            var targetCount = Mathf.Min(burgerPickCount, initialState.mapPool.Count);
            if (targetCount <= 0)
            {
                rouletteRoutine = null;
                yield break;
            }

            var targetIds = PickBurgerMapIds(initialState, targetCount);
            var cursors = new List<BurgerCursor>();
            var lockedIds = new List<string>();
            var elapsed = 0f;

            initialState.screen = BossRaidScreens.BurgerReveal;
            initialState.selectedMapId = "";
            ApplyBurgerSelection(initialState, lockedIds, new List<string>());
            Bump(initialState);
            ApplyLocalState(initialState, 1.2f);

            for (var i = 0; i < targetIds.Count; i++)
            {
                cursors.Add(new BurgerCursor
                {
                    targetId = targetIds[i],
                    currentId = "",
                    stopAt = Random.Range(burgerRouletteMinDurationSeconds, burgerRouletteMaxDurationSeconds),
                    nextStepAt = 0f,
                    locked = false
                });
            }

            var guardSeconds = Mathf.Max(burgerRouletteMinDurationSeconds, burgerRouletteMaxDurationSeconds) + 1.0f;
            while (lockedIds.Count < targetIds.Count && elapsed < guardSeconds)
            {
                var state = stateStore.Current;
                var activeIds = new List<string>();
                var anyChanged = false;

                for (var i = 0; i < cursors.Count; i++)
                {
                    var cursor = cursors[i];
                    if (cursor.locked)
                    {
                        activeIds.Add(cursor.targetId);
                        continue;
                    }

                    if (elapsed >= cursor.stopAt)
                    {
                        cursor.locked = true;
                        cursor.currentId = cursor.targetId;
                        if (!lockedIds.Contains(cursor.targetId))
                        {
                            lockedIds.Add(cursor.targetId);
                        }

                        activeIds.Add(cursor.targetId);
                        anyChanged = true;
                        continue;
                    }

                    if (elapsed >= cursor.nextStepAt || string.IsNullOrEmpty(cursor.currentId))
                    {
                        cursor.currentId = PickBurgerCursorId(state, lockedIds, activeIds);
                        cursor.nextStepAt = elapsed + Random.Range(burgerRouletteMinStepSeconds, burgerRouletteMaxStepSeconds);
                        anyChanged = true;
                    }

                    activeIds.Add(cursor.currentId);
                }

                if (anyChanged)
                {
                    state.screen = BossRaidScreens.BurgerReveal;
                    state.selectedMapId = "";
                    ApplyBurgerSelection(state, lockedIds, activeIds);
                    Bump(state);
                    ApplyLocalState(state, 1.0f);
                }

                yield return null;
                elapsed += Time.deltaTime;
            }

            var lockedState = stateStore.Current;
            lockedState.screen = BossRaidScreens.BurgerReveal;
            lockedState.selectedMapId = "";
            ApplyBurgerSelection(lockedState, targetIds, new List<string>());
            Bump(lockedState);
            ApplyLocalState(lockedState);
            SendCommand("set_burger_maps", JsonStringArrayField("mapIds", targetIds));
            rouletteRoutine = null;
        }

        private IEnumerator RollMapRoulette()
        {
            var candidates = GetMapCandidates(stateStore.Current, true);
            if (candidates.Count == 0)
            {
                rouletteRoutine = null;
                yield break;
            }

            var ticks = Random.Range(32, 46);
            var index = Random.Range(0, candidates.Count);
            for (var tick = 0; tick < ticks; tick++)
            {
                var selected = candidates[index % candidates.Count];
                var state = stateStore.Current;
                state.screen = BossRaidScreens.RouletteMap;
                state.selectedMode = selected.mode;
                state.selectedMapId = selected.id;
                Bump(state);
                ApplyLocalState(state, 0.9f);

                index += 1;
                yield return new WaitForSeconds(GetRouletteDelay(tick, ticks));
            }

            var finalMapId = stateStore.Current.selectedMapId;
            var finalMode = stateStore.Current.selectedMode;
            bridgeClient?.HoldIncomingState(rouletteAutoAdvanceDelaySeconds + 1.2f);
            yield return new WaitForSeconds(rouletteAutoAdvanceDelaySeconds);

            var finalState = stateStore.Current;
            finalState.screen = BossRaidScreens.MapReady;
            finalState.selectedMode = finalMode;
            finalState.selectedMapId = finalMapId;
            Bump(finalState);
            ApplyLocalState(finalState);
            SendCommand("complete_map_roulette", JsonStringField("mapId", finalMapId));
            rouletteRoutine = null;
        }

        private void StartPreviewMap()
        {
            var state = stateStore.Current;
            for (var i = 0; i < state.teams.Count; i++)
            {
                state.teams[i].score = Random.Range(180000, 390000);
            }

            state.lastResult = BossRaidResults.None;
            state.resultMessage = "";
            state.screen = BossRaidScreens.InGame;
            Bump(state);
            ApplyLocalState(state);
            SendCommand("start_map");
        }

        private void FinishPreviewMap(bool clear)
        {
            var state = stateStore.Current;
            var target = state.CurrentDifficulty != null ? state.CurrentDifficulty.bossHp : state.bossHp;
            var perTeam = clear ? Mathf.CeilToInt(target / Mathf.Max(1f, state.teams.Count)) : Mathf.FloorToInt(target * 0.72f / Mathf.Max(1f, state.teams.Count));
            for (var i = 0; i < state.teams.Count; i++)
            {
                state.teams[i].score = perTeam + Random.Range(0, clear ? 90000 : 45000);
            }

            BossRaidStateStore.Normalize(state);
            clear = state.totalScore >= state.bossHp;
            state.lastResult = clear ? BossRaidResults.Clear : BossRaidResults.Fail;
            state.screen = BossRaidScreens.Result;

            var map = state.SelectedMap;
            if (map != null)
            {
                map.played = true;
            }

            if (clear)
            {
                state.clearCount += 1;
                if (state.CurrentDifficulty != null)
                {
                    state.prizePool += state.CurrentDifficulty.prize;
                }

                if (map != null && map.isBurger && state.difficulty == BossRaidDifficulties.Hard)
                {
                    state.burgerCount += 1;
                    state.resultMessage = "Boss defeated. Viewer burger added.";
                }
                else if (map != null && map.isBurger)
                {
                    state.burgerMissCount += 1;
                    state.resultMessage = "Boss defeated, but burger condition missed.";
                }
                else
                {
                    state.resultMessage = "Boss defeated. Prize pool increased.";
                }
            }
            else
            {
                state.failCount += 1;
                if (map != null && map.isBurger)
                {
                    state.burgerMissCount += 1;
                }

                state.resultMessage = "Boss survived. Failure count increased.";
            }

            Bump(state);
            ApplyLocalState(state);
            SendCommand("finish_map");
        }

        private void NextPreviewRound()
        {
            var state = stateStore.Current;
            state.roundIndex = Mathf.Clamp(state.roundIndex + 1, 0, 7);
            state.lastResult = BossRaidResults.None;
            state.resultMessage = "";
            for (var i = 0; i < state.teams.Count; i++)
            {
                state.teams[i].score = 0;
            }

            Bump(state);
            ApplyLocalState(state);
            SendCommand("next_round");
            SpinPreviewMode();
        }

        private void ApplyLocalState(BossRaidState state, float holdIncomingSeconds = 0.45f)
        {
            bridgeClient?.HoldIncomingState(holdIncomingSeconds);
            stateStore.ApplyPreviewState(state);
        }

        private void SendCommand(string type, string fields = "", float holdIncomingSeconds = 0.45f)
        {
            if (bridgeClient == null)
            {
                return;
            }

            bridgeClient.HoldIncomingState(holdIncomingSeconds);
            bridgeClient.SendCommandJson($"{{\"type\":\"{EscapeJson(type)}\"{fields}}}");
        }

        private static string JsonStringField(string key, string value)
        {
            return $",\"{EscapeJson(key)}\":\"{EscapeJson(value)}\"";
        }

        private static string JsonStringArrayField(string key, List<string> values)
        {
            var items = new List<string>();
            if (values != null)
            {
                for (var i = 0; i < values.Count; i++)
                {
                    items.Add($"\"{EscapeJson(values[i])}\"");
                }
            }

            return $",\"{EscapeJson(key)}\":[{string.Join(",", items)}]";
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value)
                ? ""
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static List<string> PickBurgerMapIds(BossRaidState state, int count)
        {
            var selectedIds = new List<string>();
            var remaining = new List<int>();
            for (var i = 0; i < state.mapPool.Count; i++)
            {
                remaining.Add(i);
            }

            for (var i = 0; i < count && remaining.Count > 0; i++)
            {
                var pick = Random.Range(0, remaining.Count);
                selectedIds.Add(state.mapPool[remaining[pick]].id);
                remaining.RemoveAt(pick);
            }

            return selectedIds;
        }

        private static void ApplyBurgerSelection(BossRaidState state, List<string> lockedIds, List<string> activeIds)
        {
            for (var i = 0; i < state.mapPool.Count; i++)
            {
                var map = state.mapPool[i];
                map.isBurger = lockedIds.Contains(map.id) || (activeIds != null && activeIds.Contains(map.id));
            }
        }

        private static string PickBurgerCursorId(BossRaidState state, List<string> lockedIds, List<string> activeIds)
        {
            var candidates = new List<BossRaidMap>();
            for (var i = 0; i < state.mapPool.Count; i++)
            {
                var map = state.mapPool[i];
                if (!lockedIds.Contains(map.id) && !activeIds.Contains(map.id))
                {
                    candidates.Add(map);
                }
            }

            if (candidates.Count == 0)
            {
                for (var i = 0; i < state.mapPool.Count; i++)
                {
                    var map = state.mapPool[i];
                    if (!lockedIds.Contains(map.id))
                    {
                        candidates.Add(map);
                    }
                }
            }

            return candidates.Count == 0 ? "" : candidates[Random.Range(0, candidates.Count)].id;
        }

        private float GetRouletteDelay(int tick, int totalTicks)
        {
            if (totalTicks <= 1)
            {
                return rouletteMaxStepSeconds;
            }

            var progress = Mathf.Clamp01((float)tick / (totalTicks - 1));
            return Mathf.Lerp(rouletteMinStepSeconds, rouletteMaxStepSeconds, progress * progress);
        }

        private static List<string> GetModes(BossRaidState state)
        {
            var modes = new List<string>();
            for (var i = 0; i < state.mapPool.Count; i++)
            {
                var mode = state.mapPool[i].mode;
                if (!string.IsNullOrEmpty(mode) && !modes.Contains(mode))
                {
                    modes.Add(mode);
                }
            }

            return modes;
        }

        private static List<BossRaidMap> GetMapCandidates(BossRaidState state, bool includePlayedFallback)
        {
            var candidates = new List<BossRaidMap>();
            var roundIds = state.selectedRoundMapIds ?? new List<string>();
            for (var i = 0; i < state.mapPool.Count; i++)
            {
                var map = state.mapPool[i];
                if (map.mode != state.selectedMode)
                {
                    continue;
                }

                if (roundIds.Count > 0 && !roundIds.Contains(map.id))
                {
                    continue;
                }

                if (!map.played)
                {
                    candidates.Add(map);
                }
            }

            if (candidates.Count > 0 || !includePlayedFallback)
            {
                return candidates;
            }

            for (var i = 0; i < state.mapPool.Count; i++)
            {
                var map = state.mapPool[i];
                if (map.mode == state.selectedMode && (roundIds.Count == 0 || roundIds.Contains(map.id)))
                {
                    candidates.Add(map);
                }
            }

            return candidates;
        }

        private static bool IsControlPressed()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        private static bool GetNumberDown(KeyCode alphaKey, KeyCode keypadKey)
        {
            return Input.GetKeyDown(alphaKey) || Input.GetKeyDown(keypadKey);
        }

        private void StopRoulette()
        {
            if (rouletteRoutine == null)
            {
                return;
            }

            StopCoroutine(rouletteRoutine);
            rouletteRoutine = null;
        }

        private static void Bump(BossRaidState state)
        {
            state.animationNonce += 1;
        }
    }
}
