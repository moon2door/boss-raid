using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BossRaid
{
    public sealed class BossRaidOverlayView : MonoBehaviour
    {
        private static readonly Color Background = new Color(0.018f, 0.022f, 0.032f, 1f);
        private static readonly Color Panel = new Color(0.055f, 0.067f, 0.09f, 0.95f);
        private static readonly Color PanelAlt = new Color(0.085f, 0.095f, 0.125f, 0.96f);
        private static readonly Color Line = new Color(0.42f, 0.48f, 0.55f, 0.35f);
        private static readonly Color White = new Color(0.94f, 0.96f, 0.98f, 1f);
        private static readonly Color Muted = new Color(0.62f, 0.68f, 0.75f, 1f);
        private static readonly Color Red = new Color(0.95f, 0.18f, 0.22f, 1f);
        private static readonly Color Green = new Color(0.22f, 0.85f, 0.48f, 1f);
        private static readonly Color Cyan = new Color(0.25f, 0.78f, 1f, 1f);
        private static readonly Color Gold = new Color(1f, 0.72f, 0.22f, 1f);
        private const string PrefabResourcePath = "BossRaidUi/";

        [SerializeField] private bool usePrefabUi = true;

        private BossRaidStateStore stateStore;
        private BossRaidWebSocketClient socketClient;
        private BossRaidState state;
        private RectTransform root;
        private RectTransform header;
        private RectTransform content;
        private RectTransform prefabLayer;
        private Image backgroundImage;
        private Text connectionText;
        private float pulse;

        private sealed class PrefabContext
        {
            public BossRaidTeam team;
            public BossRaidMap map;
            public BossRaidDifficultyConfig difficulty;
            public string mode;
            public string statLabel;
            public string statValue;
            public Color statColor;
            public int index;
            public bool selected;
            public bool roundMap;
        }

        private void Awake()
        {
            stateStore = GetComponent<BossRaidStateStore>();
            socketClient = GetComponent<BossRaidWebSocketClient>();
            BuildCanvas();
        }

        private void OnEnable()
        {
            if (stateStore != null)
            {
                stateStore.StateChanged += Render;
            }

            if (socketClient != null)
            {
                socketClient.StatusChanged += UpdateConnectionStatus;
            }
        }

        private void Start()
        {
            Render(stateStore != null ? stateStore.Current : BossRaidStateStore.CreatePreviewState());
        }

        private void OnDisable()
        {
            if (stateStore != null)
            {
                stateStore.StateChanged -= Render;
            }

            if (socketClient != null)
            {
                socketClient.StatusChanged -= UpdateConnectionStatus;
            }
        }

        private void Update()
        {
            pulse += Time.deltaTime;
            if (backgroundImage != null)
            {
                var t = (Mathf.Sin(pulse * 0.35f) + 1f) * 0.5f;
                backgroundImage.color = Color.Lerp(Background, new Color(0.028f, 0.032f, 0.045f, 1f), t);
            }
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("BossRaidCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root = CreateRect("Root", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backgroundImage = root.gameObject.AddComponent<Image>();
            backgroundImage.color = Background;

            CreateAccentBand(root, 0.06f, Red);
            CreateAccentBand(root, 0.38f, Cyan);
            CreateAccentBand(root, 0.74f, Gold);

            header = CreateRect("Header", root, new Vector2(0f, 1f), Vector2.one, new Vector2(28f, -146f), new Vector2(-28f, -24f));
            content = CreateRect("Content", root, Vector2.zero, new Vector2(1f, 1f), new Vector2(28f, 28f), new Vector2(-28f, -164f));
            prefabLayer = CreateRect("PrefabLayer", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private void Render(BossRaidState nextState)
        {
            state = nextState ?? BossRaidStateStore.CreatePreviewState();
            BossRaidStateStore.Normalize(state);

            Clear(header);
            Clear(content);
            Clear(prefabLayer);

            if (TryBuildPrefab(GetFullScreenPrefabName(), prefabLayer))
            {
                return;
            }

            BuildHeader();

            switch (state.screen)
            {
                case BossRaidScreens.BurgerReveal:
                    BuildBurgerReveal();
                    break;
                case BossRaidScreens.RouletteMode:
                    BuildModeRoulette();
                    break;
                case BossRaidScreens.RouletteMap:
                    BuildMapRoulette();
                    break;
                case BossRaidScreens.DifficultySelect:
                    BuildDifficultySelect();
                    break;
                case BossRaidScreens.MapReady:
                    BuildMapReady();
                    break;
                case BossRaidScreens.InGame:
                    BuildInGame();
                    break;
                case BossRaidScreens.Result:
                    BuildResult();
                    break;
                default:
                    BuildStandby();
                    break;
            }

        }

        private string GetFullScreenPrefabName()
        {
            switch (state.screen)
            {
                case BossRaidScreens.BurgerReveal:
                    return "BurgerMapSelectScreen";
                case BossRaidScreens.RouletteMode:
                    return "ModeSelectScreen";
                case BossRaidScreens.RouletteMap:
                    return "MapSelectScreen";
                case BossRaidScreens.DifficultySelect:
                    return "DifficultySelectScreen";
                case BossRaidScreens.MapReady:
                    return "MapReadyScreen";
                case BossRaidScreens.InGame:
                    return "InGameScreen";
                case BossRaidScreens.Result:
                    return state.lastResult == BossRaidResults.Clear ? "SuccessResultScreen" : "FailResultScreen";
                default:
                    return "StartScreen";
            }
        }

        private void BuildHeader()
        {
            if (TryBuildPrefab("Header", header))
            {
                return;
            }

            var team = state.CurrentTeam;
            var selectedMap = state.SelectedMap;

            var left = CreatePanel("HeaderLeft", header, new Vector2(0f, 0f), new Vector2(0.35f, 1f), Vector2.zero, new Vector2(-12f, 0f), Panel);
            CreateAnchoredText(left, "EventTitle", state.eventTitle, 34, White, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.50f), Vector2.one, new Vector2(24f, 2f), new Vector2(-18f, -14f));
            CreateAnchoredText(left, "Team", team != null ? team.name : "No Team", 24, team != null ? team.color : Cyan, TextAnchor.MiddleLeft, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.48f), new Vector2(24f, 12f), new Vector2(-18f, -4f));

            var center = CreatePanel("HeaderCenter", header, new Vector2(0.35f, 0f), new Vector2(0.65f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f), PanelAlt);
            CreateAnchoredText(center, "Map", selectedMap != null ? selectedMap.title : "Waiting for map", 17, Muted, TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(0f, 0.68f), Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, -12f));
            CreateAnchoredText(center, "Screen", GetScreenLabel(), 34, GetScreenColor(), TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.64f), new Vector2(16f, 8f), new Vector2(-16f, -4f));

            var right = CreatePanel("HeaderRight", header, new Vector2(0.65f, 0f), Vector2.one, new Vector2(12f, 0f), Vector2.zero, Panel);
            BuildStatTile(right, "Prize", FormatWon(state.prizePool), Gold, 0);
            BuildStatTile(right, "Burger", state.burgerCount.ToString(), Gold, 1);
            BuildStatTile(right, "Round", $"{Mathf.Clamp(state.roundIndex + 1, 1, 8)} / 8", Cyan, 2);
            BuildStatTile(right, "Record", $"{state.clearCount}C {state.failCount}F", state.failCount > 0 ? Red : Green, 3);

            connectionText = CreateAnchoredText(header, "Connection", socketClient != null ? socketClient.StatusLabel : state.connectionLabel, 15, Muted, TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(0.65f, 0f), Vector2.one, new Vector2(12f, 2f), new Vector2(-18f, -104f));
        }

        private void BuildStandby()
        {
            if (TryBuildPrefab("StandbyScreen", content))
            {
                return;
            }

            var hero = CreatePanel("StandbyHero", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.045f, 0.05f, 0.07f, 0.92f));
            CreateAnchoredText(hero, "Title", state.eventTitle, 88, White, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.53f), new Vector2(1f, 0.70f), new Vector2(80f, 0f), new Vector2(-80f, 0f));
            CreateAnchoredText(hero, "Subtitle", "Co-op boss raid overlay ready", 30, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.44f), new Vector2(1f, 0.52f), new Vector2(80f, 0f), new Vector2(-80f, 0f));

            var teamRow = CreateRect("TeamRow", hero, new Vector2(0f, 0f), new Vector2(1f, 0.38f), new Vector2(56f, 48f), new Vector2(-56f, -24f));
            BuildTeamCards(teamRow);
        }

        private void BuildBurgerReveal()
        {
            if (TryBuildPrefab("BurgerRevealScreen", content))
            {
                return;
            }

            CreateAnchoredText(content, "Title", "BURGER MAPS LOCKED", 44, Gold, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.90f), Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -18f));
            CreateAnchoredText(content, "Subtitle", "Eight maps carry viewer burger stakes", 20, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.84f), new Vector2(1f, 0.90f), new Vector2(0f, 0f), new Vector2(0f, -4f));
            var gridRoot = CreateRect("BurgerGridRoot", content, new Vector2(0f, 0f), new Vector2(1f, 0.80f), new Vector2(32f, 28f), new Vector2(-32f, -10f));
            BuildBurgerMapGrid(gridRoot);
        }

        private void BuildModeRoulette()
        {
            if (TryBuildPrefab("RouletteModeScreen", content))
            {
                return;
            }

            CreateAnchoredText(content, "Title", "MODE ROULETTE", 50, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.84f), Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -36f));
            var modes = GetModes();
            var area = CreateRect("ModeArea", content, new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.76f), Vector2.zero, Vector2.zero);
            var layout = area.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            for (var i = 0; i < modes.Count; i++)
            {
                var isSelected = string.Equals(modes[i], state.selectedMode, StringComparison.OrdinalIgnoreCase);
                var card = CreatePanel($"Mode_{modes[i]}", area, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, isSelected ? new Color(0.08f, 0.16f, 0.2f, 0.98f) : Panel);
                card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                AddOutline(card, isSelected ? Cyan : Line, isSelected ? 4f : 1f);
                CreateAnchoredText(card, "ModeName", modes[i], 44, isSelected ? Cyan : White, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.26f), new Vector2(1f, 0.78f), new Vector2(16f, 0f), new Vector2(-16f, 0f));
                CreateAnchoredText(card, "ModeCount", $"{CountModeMaps(modes[i])} maps", 20, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.20f), new Vector2(16f, 8f), new Vector2(-16f, 0f));
            }
        }

        private void BuildMapRoulette()
        {
            if (TryBuildPrefab("RouletteMapScreen", content))
            {
                return;
            }

            CreateAnchoredText(content, "Title", $"{state.selectedMode} MAP ROULETTE", 50, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.84f), Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -36f));
            var maps = new List<BossRaidMap>();
            for (var i = 0; i < state.mapPool.Count; i++)
            {
                if (string.Equals(state.mapPool[i].mode, state.selectedMode, StringComparison.OrdinalIgnoreCase))
                {
                    maps.Add(state.mapPool[i]);
                }
            }

            var gridRoot = CreateRect("MapRouletteGrid", content, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.76f), Vector2.zero, Vector2.zero);
            BuildMapGrid(gridRoot, maps, state.selectedMapId, false);
        }

        private void BuildDifficultySelect()
        {
            if (TryBuildPrefab("DifficultySelectScreen", content))
            {
                return;
            }

            var panel = CreatePanel("DifficultySelect", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.045f, 0.05f, 0.07f, 0.94f));
            CreateAnchoredText(panel, "Title", "SELECT DIFFICULTY", 52, Gold, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.88f), Vector2.one, new Vector2(80f, 0f), new Vector2(-80f, -18f));
            CreateAnchoredText(panel, "Lead", "Choose boss HP before the map roulette", 30, White, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.78f), new Vector2(1f, 0.88f), new Vector2(80f, 0f), new Vector2(-80f, 0f));
            CreateAnchoredText(panel, "Meta", "Confirm to enter Mode Roulette", 22, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.72f), new Vector2(1f, 0.78f), new Vector2(120f, 0f), new Vector2(-120f, 0f));

            var cards = CreateRect("DifficultyCards", panel, new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.68f), Vector2.zero, Vector2.zero);
            var layout = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            for (var i = 0; i < state.difficulties.Count; i++)
            {
                var difficulty = state.difficulties[i];
                var isSelected = string.Equals(difficulty.id, state.difficulty, StringComparison.OrdinalIgnoreCase);
                var color = GetDifficultyColor(difficulty.id);
                var card = CreatePanel($"Difficulty_{difficulty.id}", cards, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, isSelected ? new Color(color.r * 0.18f, color.g * 0.18f, color.b * 0.18f, 0.98f) : PanelAlt);
                card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                AddOutline(card, isSelected ? color : Line, isSelected ? 4f : 1.2f);

                CreateAnchoredText(card, "Selected", isSelected ? "CURRENT PICK" : "", 20, color, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.82f), Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, -8f));
                CreateAnchoredText(card, "Label", difficulty.label, 48, isSelected ? color : White, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.54f), new Vector2(1f, 0.82f), new Vector2(20f, 0f), new Vector2(-20f, 0f));
                CreateAnchoredText(card, "Hp", $"HP {FormatNumber(difficulty.bossHp)}", 30, White, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.34f), new Vector2(1f, 0.50f), new Vector2(20f, 0f), new Vector2(-20f, 0f));
                CreateAnchoredText(card, "Prize", $"+{FormatWon(difficulty.prize)}", 26, Gold, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.18f), new Vector2(1f, 0.32f), new Vector2(20f, 0f), new Vector2(-20f, 0f));
            }

            var current = state.CurrentDifficulty;
            CreateAnchoredText(panel, "Current", current != null ? $"{current.label} / HP {FormatNumber(current.bossHp)} / {FormatWon(current.prize)}" : "Difficulty pending", 30, GetDifficultyColor(state.difficulty), TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.08f), new Vector2(1f, 0.18f), new Vector2(80f, 0f), new Vector2(-80f, 0f));
        }

        private void BuildMapReady()
        {
            if (TryBuildPrefab("MapReadyScreen", content))
            {
                return;
            }

            CreatePanel("ReadyStage", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Panel);
            BuildSpectatorSlots(content);

            var chat = CreatePanel("ReadyChat", content, Vector2.zero, new Vector2(0.30f, 0.22f), Vector2.zero, new Vector2(-14f, 0f), PanelAlt);
            CreateAnchoredText(chat, "ChatTitle", "INGAME CHAT", 18, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.68f), Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, -8f));
            CreateAnchoredText(chat, "ChatBody", BuildChatBody(), 18, White, TextAnchor.MiddleLeft, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.66f), new Vector2(18f, 12f), new Vector2(-18f, 0f));

            var mapInfo = CreatePanel("ReadyMapInfo", content, new Vector2(0.64f, 0f), new Vector2(1f, 0.28f), new Vector2(14f, 0f), Vector2.zero, PanelAlt);
            BuildMapInfoPanel(mapInfo, "MAP READY");
        }

        private void BuildInGame()
        {
            if (TryBuildPrefab("InGameScreen", content))
            {
                return;
            }

            CreatePanel("RaidStage", content, new Vector2(0f, 0.30f), Vector2.one, Vector2.zero, Vector2.zero, Panel);
            BuildSpectatorSlots(content);

            var bottom = CreatePanel("BossBarArea", content, Vector2.zero, new Vector2(1f, 0.26f), Vector2.zero, Vector2.zero, PanelAlt);
            BuildBossHealthBar(bottom, new Vector2(36f, 64f), new Vector2(-36f, -42f), false);
        }

        private void BuildResult()
        {
            if (TryBuildPrefab("ResultScreen", content))
            {
                return;
            }

            var isClear = state.lastResult == BossRaidResults.Clear;
            var bannerColor = isClear ? Green : Red;
            var result = CreatePanel("ResultPanel", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.035f, 0.04f, 0.055f, 0.96f));
            CreateAnchoredText(result, "ResultText", isClear ? "CLEAR" : "FAILED", 108, bannerColor, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.55f), new Vector2(1f, 0.76f), new Vector2(80f, 0f), new Vector2(-80f, 0f));
            CreateAnchoredText(result, "ResultMessage", string.IsNullOrEmpty(state.resultMessage) ? BuildResultMessage(isClear) : state.resultMessage, 32, White, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.45f), new Vector2(1f, 0.54f), new Vector2(80f, 0f), new Vector2(-80f, 0f));

            var barRoot = CreatePanel("ResultBossBar", result, new Vector2(0.12f, 0.25f), new Vector2(0.88f, 0.38f), Vector2.zero, Vector2.zero, PanelAlt);
            BuildBossHealthBar(barRoot, new Vector2(24f, 34f), new Vector2(-24f, -30f), true);

            var statRow = CreateRect("ResultStats", result, new Vector2(0.12f, 0.07f), new Vector2(0.88f, 0.20f), Vector2.zero, Vector2.zero);
            BuildResultStat(statRow, "Prize Pool", FormatWon(state.prizePool), Gold, 0);
            BuildResultStat(statRow, "Burgers", state.burgerCount.ToString(), Gold, 1);
            BuildResultStat(statRow, "Burger Miss", state.burgerMissCount.ToString(), Red, 2);
            BuildResultStat(statRow, "Record", $"{state.clearCount}C {state.failCount}F", isClear ? Green : Red, 3);
        }

        private void BuildSelectedMapHero(RectTransform parent, string label, bool reserveRightForTeams)
        {
            var map = state.SelectedMap;
            var difficulty = state.CurrentDifficulty;
            var infoMaxX = reserveRightForTeams ? 0.54f : 0.68f;
            var sideMinX = reserveRightForTeams ? 0.02f : 0.70f;
            var sideMaxX = reserveRightForTeams ? 0.54f : 0.98f;
            var sideAnchor = reserveRightForTeams ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;

            CreateAnchoredText(parent, "Section", label, 24, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.78f), new Vector2(infoMaxX, 0.92f), new Vector2(30f, 0f), new Vector2(-20f, 0f));
            CreateAnchoredText(parent, "MapTitle", map != null ? map.title : "No map selected", 52, White, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.54f), new Vector2(infoMaxX, 0.78f), new Vector2(30f, 0f), new Vector2(-20f, 0f));
            CreateAnchoredText(parent, "MapMeta", map != null ? $"{map.artist} / {map.mapper} / {map.difficultyName}" : "Select a map in Bridge", 22, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.42f), new Vector2(infoMaxX, 0.52f), new Vector2(32f, 0f), new Vector2(-20f, 0f));
            CreateAnchoredText(parent, "Mode", string.IsNullOrEmpty(state.selectedMode) ? "Mode pending" : state.selectedMode, reserveRightForTeams ? 26 : 32, Cyan, sideAnchor, FontStyle.Bold, new Vector2(sideMinX, 0.28f), new Vector2(sideMaxX, 0.40f), reserveRightForTeams ? new Vector2(32f, 0f) : Vector2.zero, reserveRightForTeams ? new Vector2(-20f, 0f) : new Vector2(-34f, 0f));
            CreateAnchoredText(parent, "Difficulty", difficulty != null ? $"{difficulty.label} / HP {FormatNumber(difficulty.bossHp)}" : "Difficulty pending", reserveRightForTeams ? 24 : 28, GetDifficultyColor(state.difficulty), sideAnchor, FontStyle.Bold, new Vector2(sideMinX, 0.16f), new Vector2(sideMaxX, 0.28f), reserveRightForTeams ? new Vector2(32f, 0f) : Vector2.zero, reserveRightForTeams ? new Vector2(-20f, 0f) : new Vector2(-34f, 0f));

            if (map != null && map.isBurger)
            {
                CreateAnchoredText(parent, "Burger", "BURGER TARGET", reserveRightForTeams ? 24 : 28, Gold, sideAnchor, FontStyle.Bold, new Vector2(sideMinX, 0.04f), new Vector2(sideMaxX, 0.16f), reserveRightForTeams ? new Vector2(32f, 0f) : Vector2.zero, reserveRightForTeams ? new Vector2(-20f, 0f) : new Vector2(-34f, 0f));
            }
        }

        private void BuildSpectatorSlots(RectTransform parent)
        {
            var root = CreateRect("SpectatorSlots", parent, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(-740f, -178f), new Vector2(740f, 178f));
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            for (var i = 0; i < 3; i++)
            {
                var slot = CreatePanel($"SpectatorSlot_{i + 1}", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.012f, 0.015f, 0.022f, 0.78f));
                slot.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                AddOutline(slot, Line, 1.5f);
                CreateAnchoredText(slot, "Label", $"SPECTATOR {i + 1}", 18, Muted, TextAnchor.UpperLeft, FontStyle.Bold, new Vector2(0f, 0.80f), Vector2.one, new Vector2(18f, -14f), new Vector2(-18f, -12f));
                CreateAnchoredText(slot, "Guide", "osu! tourney", 22, new Color(Muted.r, Muted.g, Muted.b, 0.42f), TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, 0f));
            }
        }

        private void BuildMapInfoPanel(RectTransform parent, string label)
        {
            var map = state.SelectedMap;
            var difficulty = state.CurrentDifficulty;
            var mode = map != null && !string.IsNullOrEmpty(map.mode) ? map.mode : state.selectedMode;

            CreateAnchoredText(parent, "Label", label, 20, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.82f), Vector2.one, new Vector2(22f, 0f), new Vector2(-22f, -8f));
            CreateAnchoredText(parent, "Title", map != null ? map.title : "No map selected", 34, White, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.64f), new Vector2(1f, 0.84f), new Vector2(22f, 0f), new Vector2(-22f, -2f));
            CreateAnchoredText(parent, "DifficultyName", map != null ? map.difficultyName : "Difficulty pending", 20, Cyan, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.51f), new Vector2(1f, 0.64f), new Vector2(22f, 0f), new Vector2(-22f, 0f));
            CreateAnchoredText(parent, "Artist", map != null ? map.artist : "Artist pending", 18, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.39f), new Vector2(0.62f, 0.51f), new Vector2(22f, 0f), new Vector2(-8f, 0f));
            CreateAnchoredText(parent, "Mapper", map != null ? map.mapper : "Mapper pending", 18, Muted, TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(0.62f, 0.39f), new Vector2(1f, 0.51f), new Vector2(8f, 0f), new Vector2(-22f, 0f));
            CreateAnchoredText(parent, "Mode", string.IsNullOrEmpty(mode) ? "Mode pending" : $"Mode {mode}", 22, Cyan, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.22f), new Vector2(0.48f, 0.36f), new Vector2(22f, 0f), new Vector2(-8f, 0f));
            CreateAnchoredText(parent, "Difficulty", difficulty != null ? difficulty.label : "Difficulty pending", 22, GetDifficultyColor(state.difficulty), TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.48f, 0.22f), new Vector2(0.72f, 0.36f), Vector2.zero, Vector2.zero);
            CreateAnchoredText(parent, "Hp", difficulty != null ? $"HP {FormatNumber(difficulty.bossHp)}" : $"HP {FormatNumber(state.bossHp)}", 22, Gold, TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(0.72f, 0.22f), new Vector2(1f, 0.36f), new Vector2(8f, 0f), new Vector2(-22f, 0f));

            if (map != null && map.isBurger)
            {
                CreateAnchoredText(parent, "Burger", "BURGER TARGET", 18, Gold, TextAnchor.MiddleLeft, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.18f), new Vector2(22f, 8f), new Vector2(-22f, 0f));
            }
        }

        private void BuildTeamCards(RectTransform parent)
        {
            var layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            for (var i = 0; i < state.teams.Count; i++)
            {
                var team = state.teams[i];
                var card = CreatePanel($"TeamCard_{team.id}", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelAlt);
                card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                AddOutline(card, team.id == state.currentTeamId ? team.color : Line, team.id == state.currentTeamId ? 3f : 1f);
                CreateAnchoredText(card, "TeamName", team.name, 30, team.color, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.54f), Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, -20f));
                CreateAnchoredText(card, "Score", FormatNumber(team.score), 40, White, TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.52f), new Vector2(18f, 20f), new Vector2(-18f, -6f));
            }
        }

        private void BuildTeamScoreList(RectTransform parent, bool compact)
        {
            var xMin = compact ? 0.56f : 0f;
            var listRoot = CreateRect("TeamScoreList", parent, new Vector2(xMin, 0f), Vector2.one, new Vector2(compact ? 18f : 24f, 20f), new Vector2(-24f, -74f));
            var layout = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = compact ? 8f : 10f;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            for (var i = 0; i < state.teams.Count; i++)
            {
                var team = state.teams[i];
                var row = CreatePanel($"TeamRow_{team.id}", listRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.03f, 0.036f, 0.05f, 0.92f));
                row.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                AddOutline(row, team.id == state.currentTeamId ? team.color : Line, 1.5f);
                CreateAnchoredText(row, "Name", team.name, compact ? 22 : 26, team.color, TextAnchor.MiddleLeft, FontStyle.Bold, Vector2.zero, new Vector2(0.72f, 1f), new Vector2(18f, 0f), new Vector2(-8f, 0f));
                CreateAnchoredText(row, "Score", FormatNumber(team.score), compact ? 24 : 30, White, TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(0.72f, 0f), Vector2.one, new Vector2(0f, 0f), new Vector2(-20f, 0f));
            }
        }

        private void BuildMapGrid(RectTransform parent, List<BossRaidMap> maps, string selectedId, bool showAllModes)
        {
            var grid = parent.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = maps.Count <= 6 ? 3 : 4;
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.cellSize = maps.Count <= 6 ? new Vector2(420f, 180f) : new Vector2(430f, 102f);

            for (var i = 0; i < maps.Count; i++)
            {
                var map = maps[i];
                var isSelected = map.id == selectedId;
                var isRoundMap = state.selectedRoundMapIds.Count == 0 || state.selectedRoundMapIds.Contains(map.id);
                var color = map.played ? new Color(0.035f, 0.038f, 0.045f, 0.86f) : PanelAlt;
                if (isSelected)
                {
                    color = new Color(0.06f, 0.14f, 0.18f, 0.98f);
                }
                else if (map.isBurger)
                {
                    color = new Color(0.14f, 0.095f, 0.035f, 0.98f);
                }

                var card = CreatePanel($"Map_{map.id}", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color);
                AddOutline(card, isSelected ? Cyan : map.isBurger ? Gold : Line, isSelected ? 4f : 1.5f);
                card.gameObject.AddComponent<LayoutElement>();

                var titleColor = map.played ? Muted : White;
                CreateAnchoredText(card, "MapTitle", map.title, maps.Count <= 6 ? 28 : 19, titleColor, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.44f), Vector2.one, new Vector2(16f, 0f), new Vector2(-56f, -8f));
                CreateAnchoredText(card, "MapMode", showAllModes ? map.mode : map.difficultyName, maps.Count <= 6 ? 21 : 15, map.played ? Muted : Cyan, TextAnchor.MiddleLeft, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.34f), new Vector2(16f, 8f), new Vector2(-16f, -2f));
                if (map.isBurger)
                {
                    CreateAnchoredText(card, "Burger", "BURGER", maps.Count <= 6 ? 20 : 14, Gold, TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(0.58f, 0.38f), new Vector2(1f, 0.72f), new Vector2(0f, 0f), new Vector2(-16f, 0f));
                }

                if (!isRoundMap)
                {
                    var veil = CreateRect("NotRoundVeil", card, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    veil.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
                }
            }
        }

        private void BuildBurgerMapGrid(RectTransform parent)
        {
            var rowLayout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 12f;
            rowLayout.padding = new RectOffset(0, 0, 4, 4);
            rowLayout.childForceExpandHeight = true;
            rowLayout.childForceExpandWidth = true;

            var modes = GetModesInPoolOrder();
            for (var i = 0; i < modes.Count; i++)
            {
                var mode = modes[i];
                var row = CreatePanel($"BurgerRow_{mode}", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.028f, 0.033f, 0.046f, 0.54f));
                row.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

                var rowCards = CreateRect("Cards", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var cardLayout = rowCards.gameObject.AddComponent<HorizontalLayoutGroup>();
                cardLayout.spacing = 10f;
                cardLayout.padding = new RectOffset(0, 0, 0, 0);
                cardLayout.childForceExpandHeight = true;
                cardLayout.childForceExpandWidth = true;

                var maps = GetMapsForMode(mode);
                for (var j = 0; j < maps.Count; j++)
                {
                    BuildBurgerMapCard(rowCards, maps[j]);
                }
            }
        }

        private void BuildBurgerMapCard(RectTransform parent, BossRaidMap map)
        {
            var color = map.played ? new Color(0.035f, 0.038f, 0.045f, 0.86f) : PanelAlt;
            if (map.isBurger)
            {
                color = new Color(0.14f, 0.095f, 0.035f, 0.98f);
            }

            var card = CreatePanel($"BurgerMap_{map.id}", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color);
            card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            AddOutline(card, map.isBurger ? Gold : Line, map.isBurger ? 2.2f : 1.2f);

            CreateAnchoredText(card, "MapTitle", map.title, 20, map.played ? Muted : White, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.44f), Vector2.one, new Vector2(14f, 0f), new Vector2(-14f, -4f));
            CreateAnchoredText(card, "MapId", string.IsNullOrEmpty(map.id) ? map.mode : map.id, 15, map.played ? Muted : Cyan, TextAnchor.MiddleLeft, FontStyle.Bold, Vector2.zero, new Vector2(0.52f, 0.34f), new Vector2(14f, 6f), new Vector2(-4f, 0f));

            if (map.isBurger)
            {
                CreateAnchoredText(card, "Burger", "BURGER", 14, Gold, TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(0.56f, 0.34f), new Vector2(1f, 0.72f), new Vector2(0f, 0f), new Vector2(-14f, 0f));
            }
        }

        private void BuildBossHealthBar(RectTransform parent, Vector2 offsetMin, Vector2 offsetMax, bool resultMode)
        {
            if (!resultMode)
            {
                CreateAnchoredText(parent, "BossLabel", "Boss HP", 24, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.76f), Vector2.one, new Vector2(offsetMin.x, 0f), new Vector2(offsetMax.x, -8f));
                var difficulty = state.CurrentDifficulty;
                CreateAnchoredText(parent, "BossDifficulty", difficulty != null ? difficulty.label : "Difficulty pending", 24, GetDifficultyColor(state.difficulty), TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(0.52f, 0.76f), Vector2.one, new Vector2(0f, 0f), new Vector2(offsetMax.x, -8f));
                offsetMax = new Vector2(offsetMax.x, Mathf.Min(offsetMax.y, -74f));
            }

            var bar = CreatePanel("BossBar", parent, Vector2.zero, Vector2.one, offsetMin, offsetMax, new Color(0.22f, 0.035f, 0.045f, 1f));
            AddOutline(bar, Line, 2f);
            var ratio = state.bossHp <= 0 ? 1f : Mathf.Clamp01((float)state.totalScore / state.bossHp);
            var fill = CreateRect("DamageFill", bar, Vector2.zero, new Vector2(ratio, 1f), Vector2.zero, Vector2.zero);
            fill.gameObject.AddComponent<Image>().color = ratio >= 1f ? Green : Cyan;
            CreateAnchoredText(bar, "DamageText", $"{FormatNumber(state.totalScore)} / {FormatNumber(state.bossHp)}", 36, White, TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
        }

        private void BuildStatTile(RectTransform parent, string label, string value, Color color, int index)
        {
            var width = 0.25f;
            var tile = CreateRect($"Stat_{label}", parent, new Vector2(width * index, 0f), new Vector2(width * (index + 1), 1f), new Vector2(8f, 26f), new Vector2(-8f, -14f));
            tile.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.036f, 0.052f, 0.92f);
            AddOutline(tile, Line, 1f);
            CreateAnchoredText(tile, "Label", label, 13, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.62f), Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, -8f));
            CreateAnchoredText(tile, "Value", value, 23, color, TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.58f), new Vector2(4f, 6f), new Vector2(-4f, -2f));
        }

        private void BuildResultStat(RectTransform parent, string label, string value, Color color, int index)
        {
            var width = 0.25f;
            var tile = CreatePanel($"ResultStat_{label}", parent, new Vector2(width * index, 0f), new Vector2(width * (index + 1), 1f), new Vector2(8f, 0f), new Vector2(-8f, 0f), PanelAlt);
            AddOutline(tile, Line, 1f);
            CreateAnchoredText(tile, "Label", label, 17, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.60f), Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, -6f));
            CreateAnchoredText(tile, "Value", value, 30, color, TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.56f), new Vector2(8f, 8f), new Vector2(-8f, -2f));
        }

        private bool TryBuildPrefab(string resourceName, Transform parent)
        {
            if (!usePrefabUi)
            {
                return false;
            }

            var prefab = Resources.Load<GameObject>(PrefabResourcePath + resourceName);
            if (prefab == null)
            {
                return false;
            }

            var instance = Instantiate(prefab, parent, false);
            instance.name = resourceName;
            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            ApplyPrefabBindings(instance.transform, null);
            ApplyPrefabDynamicText(instance.transform);
            return true;
        }

        private void ApplyPrefabDynamicText(Transform rootTransform)
        {
            var texts = rootTransform.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                if (texts[i].name == "ChatBody")
                {
                    texts[i].text = BuildChatBody();
                    texts[i].fontSize = 18;
                    texts[i].resizeTextMaxSize = 18;
                    texts[i].resizeTextMinSize = 10;
                }
            }
        }

        private void ApplyPrefabBindings(Transform rootTransform, PrefabContext context)
        {
            var bindings = rootTransform.GetComponentsInChildren<BossRaidUiBinding>(true);
            for (var i = 0; i < bindings.Length; i++)
            {
                ApplyPrefabBinding(bindings[i], context);
            }
        }

        private void ApplyPrefabBinding(BossRaidUiBinding binding, PrefabContext context)
        {
            if (binding == null)
            {
                return;
            }

            var activeContext = context ?? ResolvePrefabContext(binding);
            if (binding.key == "ItemVisual")
            {
                UpdatePrefabItemVisuals(binding.gameObject, activeContext);
            }

            var value = ResolvePrefabText(binding.key, activeContext);
            var text = binding.GetComponent<Text>();
            if (text != null && !string.IsNullOrEmpty(binding.key))
            {
                text.text = value;
                if (binding.key == "ConnectionStatus")
                {
                    connectionText = text;
                }
            }

            if (binding.hideWhenEmpty)
            {
                binding.gameObject.SetActive(!string.IsNullOrEmpty(value));
            }

            var color = ResolvePrefabColor(binding.colorRole, activeContext);
            if (binding.colorRole != BossRaidUiColorRole.None)
            {
                if (text != null)
                {
                    text.color = color;
                }

                var graphic = binding.GetComponent<Graphic>();
                if (graphic != null && graphic != text)
                {
                    graphic.color = color;
                }
            }

            if (binding.key == "BossDamageFill")
            {
                var rect = binding.GetComponent<RectTransform>();
                if (rect != null)
                {
                    var ratio = state.bossHp <= 0 ? 1f : Mathf.Clamp01((float)state.totalScore / state.bossHp);
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = new Vector2(ratio, 1f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            }
        }

        private PrefabContext ResolvePrefabContext(BossRaidUiBinding binding)
        {
            var index = Mathf.Max(0, binding.index);
            switch (binding.source)
            {
                case BossRaidUiBindingSource.Team:
                    if (index < state.teams.Count)
                    {
                        var team = state.teams[index];
                        return new PrefabContext { team = team, index = index, selected = team.id == state.currentTeamId };
                    }
                    break;
                case BossRaidUiBindingSource.Mode:
                    var modes = GetModes();
                    if (index < modes.Count)
                    {
                        var mode = modes[index];
                        return new PrefabContext { mode = mode, index = index, selected = mode == state.selectedMode };
                    }
                    break;
                case BossRaidUiBindingSource.Difficulty:
                    if (index < state.difficulties.Count)
                    {
                        var difficulty = state.difficulties[index];
                        return new PrefabContext { difficulty = difficulty, index = index, selected = difficulty.id == state.difficulty };
                    }
                    break;
                case BossRaidUiBindingSource.SelectedModeMap:
                    var selectedMode = state.selectedMode;
                    if (string.IsNullOrEmpty(selectedMode))
                    {
                        var modeList = GetModes();
                        selectedMode = modeList.Count > 0 ? modeList[0] : "";
                    }

                    var mapsForMode = GetMapsForMode(selectedMode);
                    if (index < mapsForMode.Count)
                    {
                        var map = mapsForMode[index];
                        var isRoundMap = state.selectedRoundMapIds.Count == 0 || state.selectedRoundMapIds.Contains(map.id);
                        return new PrefabContext { map = map, index = index, selected = map.id == state.selectedMapId, roundMap = isRoundMap };
                    }
                    break;
                case BossRaidUiBindingSource.BurgerMap:
                    if (index < state.mapPool.Count)
                    {
                        var map = state.mapPool[index];
                        var isRoundMap = state.selectedRoundMapIds.Count == 0 || state.selectedRoundMapIds.Contains(map.id);
                        return new PrefabContext { map = map, index = index, selected = map.id == state.selectedMapId, roundMap = isRoundMap };
                    }
                    break;
                case BossRaidUiBindingSource.ResultStat:
                    return BuildResultStatContext(index);
                case BossRaidUiBindingSource.Spectator:
                    return new PrefabContext { index = index };
            }

            return null;
        }

        private PrefabContext BuildResultStatContext(int index)
        {
            switch (index)
            {
                case 0:
                    return new PrefabContext { statLabel = "Prize Pool", statValue = FormatWon(state.prizePool), statColor = Gold, index = index };
                case 1:
                    return new PrefabContext { statLabel = "Burgers", statValue = state.burgerCount.ToString(), statColor = Gold, index = index };
                case 2:
                    return new PrefabContext { statLabel = "Burger Miss", statValue = state.burgerMissCount.ToString(), statColor = Red, index = index };
                default:
                    return new PrefabContext { statLabel = "Record", statValue = $"{state.clearCount}C {state.failCount}F", statColor = state.lastResult == BossRaidResults.Clear ? Green : Red, index = index };
            }
        }

        private void UpdatePrefabItemVisuals(GameObject item, PrefabContext context)
        {
            if (item == null || context == null)
            {
                return;
            }

            var outline = item.GetComponent<Outline>();
            if (outline != null)
            {
                if (context.team != null)
                {
                    outline.effectColor = context.selected ? context.team.color : Line;
                    outline.effectDistance = context.selected ? new Vector2(3f, -3f) : new Vector2(1f, -1f);
                }
                else if (context.difficulty != null)
                {
                    outline.effectColor = context.selected ? GetDifficultyColor(context.difficulty.id) : Line;
                    outline.effectDistance = context.selected ? new Vector2(4f, -4f) : new Vector2(1.2f, -1.2f);
                }
                else if (context.map != null)
                {
                    outline.effectColor = context.selected ? Cyan : context.map.isBurger ? Gold : Line;
                    outline.effectDistance = context.selected ? new Vector2(4f, -4f) : new Vector2(1.5f, -1.5f);
                }
            }

            var image = item.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            if (context.difficulty != null && context.selected)
            {
                var color = GetDifficultyColor(context.difficulty.id);
                image.color = new Color(color.r * 0.18f, color.g * 0.18f, color.b * 0.18f, 0.98f);
            }
            else if (context.map != null)
            {
                if (context.selected)
                {
                    image.color = new Color(0.06f, 0.14f, 0.18f, 0.98f);
                }
                else if (context.map.isBurger)
                {
                    image.color = new Color(0.14f, 0.095f, 0.035f, 0.98f);
                }
                else if (context.map.played || !context.roundMap)
                {
                    image.color = new Color(0.035f, 0.038f, 0.045f, 0.86f);
                }
            }
        }

        private string ResolvePrefabText(string key, PrefabContext context)
        {
            var map = state.SelectedMap;
            var difficulty = state.CurrentDifficulty;
            switch (key)
            {
                case "EventTitle":
                    return state.eventTitle;
                case "CurrentTeamName":
                    return state.CurrentTeam != null ? state.CurrentTeam.name : "No Team";
                case "HeaderMapTitle":
                    return map != null ? map.title : "Waiting for map";
                case "ScreenLabel":
                    return GetScreenLabel();
                case "ConnectionStatus":
                    return socketClient != null ? socketClient.StatusLabel : state.connectionLabel;
                case "IngameChat":
                    return BuildChatBody();
                case "PrizePool":
                    return FormatWon(state.prizePool);
                case "BurgerCount":
                    return state.burgerCount.ToString();
                case "Round":
                    return $"{Mathf.Clamp(state.roundIndex + 1, 1, 8)} / 8";
                case "Record":
                    return $"{state.clearCount}C {state.failCount}F";
                case "StandbySubtitle":
                    return "Co-op boss raid overlay ready";
                case "MapRouletteTitle":
                    return string.IsNullOrEmpty(state.selectedMode) ? "MAP ROULETTE" : $"{state.selectedMode} MAP ROULETTE";
                case "SelectedMapTitle":
                    return map != null ? map.title : "No map selected";
                case "SelectedMapDifficultyName":
                    return map != null ? map.difficultyName : "Difficulty pending";
                case "SelectedMapArtist":
                    return map != null ? map.artist : "Artist pending";
                case "SelectedMapMapper":
                    return map != null ? map.mapper : "Mapper pending";
                case "SelectedMapMode":
                    return map != null && !string.IsNullOrEmpty(map.mode) ? $"Mode {map.mode}" : string.IsNullOrEmpty(state.selectedMode) ? "Mode pending" : $"Mode {state.selectedMode}";
                case "CurrentDifficultyLabel":
                    return difficulty != null ? difficulty.label : "Difficulty pending";
                case "CurrentDifficultyHp":
                    return difficulty != null ? $"HP {FormatNumber(difficulty.bossHp)}" : $"HP {FormatNumber(state.bossHp)}";
                case "CurrentDifficultySummary":
                    return difficulty != null ? $"{difficulty.label} / HP {FormatNumber(difficulty.bossHp)} / {FormatWon(difficulty.prize)}" : "Difficulty pending";
                case "BossDifficultyLabel":
                    return difficulty != null ? difficulty.label : "Difficulty pending";
                case "BossDamageText":
                    return $"{FormatNumber(state.totalScore)} / {FormatNumber(state.bossHp)}";
                case "ResultText":
                    return state.lastResult == BossRaidResults.Clear ? "CLEAR" : "FAILED";
                case "ResultMessage":
                    return string.IsNullOrEmpty(state.resultMessage) ? BuildResultMessage(state.lastResult == BossRaidResults.Clear) : state.resultMessage;
                case "BurgerMarker":
                    return map != null && map.isBurger ? "BURGER TARGET" : "";
                case "TeamName":
                    return context != null && context.team != null ? context.team.name : "";
                case "TeamScore":
                    return context != null && context.team != null ? FormatNumber(context.team.score) : "";
                case "SpectatorLabel":
                    return context != null ? $"SPECTATOR {context.index + 1}" : "SPECTATOR";
                case "ModeName":
                    return context != null ? context.mode : "";
                case "ModeCount":
                    return context != null ? $"{CountModeMaps(context.mode)} maps" : "";
                case "DifficultySelected":
                    return context != null && context.selected ? "CURRENT PICK" : "";
                case "DifficultyLabel":
                    return context != null && context.difficulty != null ? context.difficulty.label : "";
                case "DifficultyHp":
                    return context != null && context.difficulty != null ? $"HP {FormatNumber(context.difficulty.bossHp)}" : "";
                case "DifficultyPrize":
                    return context != null && context.difficulty != null ? $"+{FormatWon(context.difficulty.prize)}" : "";
                case "MapTitle":
                    return context != null && context.map != null ? context.map.title : "";
                case "MapSubtitle":
                    if (context == null || context.map == null)
                    {
                        return "";
                    }
                    return state.screen == BossRaidScreens.RouletteMap ? context.map.difficultyName : $"{context.map.mode} / {context.map.id}";
                case "MapId":
                    return context != null && context.map != null ? context.map.id : "";
                case "MapBurgerTag":
                    return context != null && context.map != null && context.map.isBurger ? "BURGER" : "";
                case "StatLabel":
                    return context != null ? context.statLabel : "";
                case "StatValue":
                    return context != null ? context.statValue : "";
                default:
                    return "";
            }
        }

        private Color ResolvePrefabColor(BossRaidUiColorRole role, PrefabContext context)
        {
            switch (role)
            {
                case BossRaidUiColorRole.White:
                    return White;
                case BossRaidUiColorRole.Muted:
                    return Muted;
                case BossRaidUiColorRole.Red:
                    return Red;
                case BossRaidUiColorRole.Green:
                    return Green;
                case BossRaidUiColorRole.Cyan:
                    return Cyan;
                case BossRaidUiColorRole.Gold:
                    return Gold;
                case BossRaidUiColorRole.CurrentTeam:
                    return state.CurrentTeam != null ? state.CurrentTeam.color : Cyan;
                case BossRaidUiColorRole.Team:
                    return context != null && context.team != null ? context.team.color : Cyan;
                case BossRaidUiColorRole.Screen:
                    return GetScreenColor();
                case BossRaidUiColorRole.Difficulty:
                    return context != null && context.difficulty != null ? GetDifficultyColor(context.difficulty.id) : GetDifficultyColor(state.difficulty);
                case BossRaidUiColorRole.Result:
                    return state.lastResult == BossRaidResults.Clear ? Green : Red;
                case BossRaidUiColorRole.Context:
                    return context != null ? context.statColor : White;
                default:
                    return White;
            }
        }

        private void UpdateConnectionStatus(string status)
        {
            if (connectionText != null)
            {
                connectionText.text = status;
            }
        }

        private string GetScreenLabel()
        {
            switch (state.screen)
            {
                case BossRaidScreens.BurgerReveal:
                    return "BURGER REVEAL";
                case BossRaidScreens.RouletteMode:
                    return "MODE ROULETTE";
                case BossRaidScreens.RouletteMap:
                    return "MAP ROULETTE";
                case BossRaidScreens.DifficultySelect:
                    return "DIFFICULTY";
                case BossRaidScreens.MapReady:
                    return "MAP READY";
                case BossRaidScreens.InGame:
                    return "RAID LIVE";
                case BossRaidScreens.Result:
                    return state.lastResult == BossRaidResults.Clear ? "RESULT: CLEAR" : "RESULT: FAILED";
                default:
                    return "STANDBY";
            }
        }

        private Color GetScreenColor()
        {
            if (state.screen == BossRaidScreens.Result)
            {
                return state.lastResult == BossRaidResults.Clear ? Green : Red;
            }

            if (state.screen == BossRaidScreens.BurgerReveal)
            {
                return Gold;
            }

            if (state.screen == BossRaidScreens.DifficultySelect)
            {
                return GetDifficultyColor(state.difficulty);
            }

            if (state.screen == BossRaidScreens.InGame)
            {
                return Red;
            }

            return Cyan;
        }

        private Color GetDifficultyColor(string difficulty)
        {
            switch (difficulty)
            {
                case BossRaidDifficulties.Easy:
                    return Green;
                case BossRaidDifficulties.Hard:
                    return Red;
                default:
                    return Gold;
            }
        }

        private string BuildResultMessage(bool isClear)
        {
            var map = state.SelectedMap;
            if (isClear)
            {
                if (map != null && map.isBurger && state.difficulty == BossRaidDifficulties.Hard)
                {
                    return "Boss defeated. Viewer burger added.";
                }

                return "Boss defeated. Prize pool increased.";
            }

            return "Boss survived. Failure count increased.";
        }

        private string BuildChatBody()
        {
            if (state == null || state.chatMessages == null || state.chatMessages.Count == 0)
            {
                return "Waiting for room chat...";
            }

            var lines = new List<string>();
            var start = Mathf.Max(0, state.chatMessages.Count - 5);
            for (var i = start; i < state.chatMessages.Count; i++)
            {
                var item = state.chatMessages[i];
                if (item == null)
                {
                    continue;
                }

                var message = CleanChatText(item.message);
                if (string.IsNullOrEmpty(message))
                {
                    continue;
                }

                var time = CleanChatText(item.time);
                var sender = CleanChatText(item.sender);
                var prefix = string.IsNullOrEmpty(sender) ? "" : $"{sender}: ";
                lines.Add(string.IsNullOrEmpty(time) ? $"{prefix}{message}" : $"[{time}] {prefix}{message}");
            }

            return lines.Count == 0 ? "Waiting for room chat..." : string.Join("\n", lines);
        }

        private static string CleanChatText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            return value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private List<string> GetModes()
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

        private List<string> GetModesInPoolOrder()
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

        private List<BossRaidMap> GetMapsForMode(string mode)
        {
            var maps = new List<BossRaidMap>();
            for (var i = 0; i < state.mapPool.Count; i++)
            {
                if (string.Equals(state.mapPool[i].mode, mode, StringComparison.OrdinalIgnoreCase))
                {
                    maps.Add(state.mapPool[i]);
                }
            }

            return maps;
        }

        private int CountModeMaps(string mode)
        {
            var count = 0;
            for (var i = 0; i < state.mapPool.Count; i++)
            {
                if (string.Equals(state.mapPool[i].mode, mode, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rect = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, Color color, TextAnchor anchor, FontStyle style, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = CreateRect(name, parent, Vector2.zero, Vector2.one, offsetMin, offsetMax);
            return ConfigureText(rect, value, size, color, anchor, style);
        }

        private static Text CreateAnchoredText(Transform parent, string name, string value, int size, Color color, TextAnchor anchor, FontStyle style, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            return ConfigureText(rect, value, size, color, anchor, style);
        }

        private static Text ConfigureText(RectTransform rect, string value, int size, Color color, TextAnchor anchor, FontStyle style)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = BossRaidUiFont.Default;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, Mathf.FloorToInt(size * 0.55f));
            text.resizeTextMaxSize = size;

            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static void AddOutline(RectTransform rect, Color color, float distance)
        {
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private static void CreateAccentBand(RectTransform parent, float x, Color color)
        {
            var band = CreateRect("AccentBand", parent, new Vector2(x, 0f), new Vector2(x, 1f), new Vector2(-2f, 0f), new Vector2(2f, 0f));
            band.gameObject.AddComponent<Image>().color = new Color(color.r, color.g, color.b, 0.22f);
        }

        private static void Clear(Transform transformToClear)
        {
            for (var i = transformToClear.childCount - 1; i >= 0; i--)
            {
                Destroy(transformToClear.GetChild(i).gameObject);
            }
        }

        private static string FormatNumber(int value)
        {
            return value.ToString("N0");
        }

        private static string FormatWon(int value)
        {
            return value <= 0 ? "0" : $"{value:N0} KRW";
        }

        private static class BossRaidUiFont
        {
            private static Font cached;

            public static Font Default
            {
                get
                {
                    if (cached != null)
                    {
                        return cached;
                    }

                    try
                    {
                        cached = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial", "Segoe UI" }, 32);
                    }
                    catch
                    {
                        cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }

                    if (cached == null)
                    {
                        cached = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    }

                    return cached;
                }
            }
        }
    }
}
