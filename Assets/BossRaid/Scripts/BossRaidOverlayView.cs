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
        private static readonly Color Cyan = new Color32(0, 240, 255, 255);
        private static readonly Color Gold = new Color(1f, 0.72f, 0.22f, 1f);
        private static readonly Color Magenta = new Color(1f, 0.18f, 0.72f, 1f);
        private const string PrefabResourcePath = "BossRaidUi_v3/";
        private const string FallbackPrefabResourcePath = "BossRaidUi/";
        private const int MinimumReadablePrefabFontSize = 16;
        private const float TextSupersampleScale = 0.5f;
        private const float TextSupersampleMultiplier = 2f;
        private static readonly Vector2 FallbackSpectatorViewportSize = new Vector2(480f, 360f);
        private static readonly Vector2 MapReadySpectatorViewportSize = new Vector2(456f, 342f);
        private static readonly Vector2 InGameSpectatorViewportSize = new Vector2(588f, 441f);
        private const float SpectatorViewportSpacing = 18f;

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

        private sealed class RoomChatLine
        {
            public string sender;
            public string message;
            public Color color;
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
            canvas.pixelPerfect = true;

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
            BuildStatTile(right, "Record", $"{state.clearCount}-{state.failCount}", state.failCount > 0 ? Red : Green, 3);

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
            BuildResultStat(statRow, "Record", $"{state.clearCount}-{state.failCount}", isClear ? Green : Red, 3);
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
            var rowSize = GetSpectatorRowSize(3, FallbackSpectatorViewportSize);
            var root = CreateRect("SpectatorSlots", parent, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), -rowSize * 0.5f, rowSize * 0.5f);
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = SpectatorViewportSpacing;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            for (var i = 0; i < 3; i++)
            {
                var slot = CreatePanel($"SpectatorSlot_{i + 1}", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.012f, 0.015f, 0.022f, 0.78f));
                ApplySpectatorLayoutElement(slot, FallbackSpectatorViewportSize);
                AddOutline(slot, Line, 1.5f);
                CreateAnchoredText(slot, "Label", $"SPECTATOR {i + 1}", 18, Muted, TextAnchor.UpperLeft, FontStyle.Bold, new Vector2(0f, 0.80f), Vector2.one, new Vector2(18f, -14f), new Vector2(-18f, -12f));
                CreateAnchoredText(slot, "Guide", "osu! tourney", 22, new Color(Muted.r, Muted.g, Muted.b, 0.42f), TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, 0f));
            }
        }

        private static Vector2 GetSpectatorRowSize(int slotCount, Vector2 viewportSize)
        {
            return new Vector2(
                viewportSize.x * slotCount + SpectatorViewportSpacing * Mathf.Max(0, slotCount - 1),
                viewportSize.y);
        }

        private static void ApplySpectatorLayoutElement(RectTransform slot, Vector2 viewportSize)
        {
            if (slot == null)
            {
                return;
            }

            var layoutElement = slot.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = slot.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minWidth = viewportSize.x;
            layoutElement.minHeight = viewportSize.y;
            layoutElement.preferredWidth = viewportSize.x;
            layoutElement.preferredHeight = viewportSize.y;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
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

            var usingV3Prefab = true;
            var prefab = Resources.Load<GameObject>(PrefabResourcePath + resourceName);
            if (prefab == null)
            {
                usingV3Prefab = false;
                prefab = Resources.Load<GameObject>(FallbackPrefabResourcePath + resourceName);
            }

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
            if (usingV3Prefab)
            {
                ApplyV3NamedState(instance.transform, resourceName);
                ApplyV3Readability(instance.transform);
            }

            return true;
        }

        private static void ApplyV3Readability(Transform rootTransform)
        {
            var texts = rootTransform.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                text.raycastTarget = false;
                if (text.fontSize < MinimumReadablePrefabFontSize)
                {
                    text.fontSize = MinimumReadablePrefabFontSize;
                }

                text.resizeTextMaxSize = Mathf.Max(text.resizeTextMaxSize, text.fontSize);
                text.resizeTextMinSize = Mathf.Max(12, Mathf.FloorToInt(text.fontSize * 0.72f));
                ApplyTextSupersampling(text);

                if (text.font != null)
                {
                    text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                    var fontTexture = text.font.material != null ? text.font.material.mainTexture : null;
                    if (fontTexture != null)
                    {
                        fontTexture.filterMode = FilterMode.Point;
                    }
                }

                var outlines = text.GetComponents<Outline>();
                for (var j = 0; j < outlines.Length; j++)
                {
                    outlines[j].enabled = false;
                }

                var shadows = text.GetComponents<Shadow>();
                for (var j = 0; j < shadows.Length; j++)
                {
                    var shadow = shadows[j];
                    if (shadow is Outline)
                    {
                        continue;
                    }

                    var color = shadow.effectColor;
                    color.a = Mathf.Min(color.a, 0.45f);
                    shadow.effectColor = color;
                    shadow.effectDistance = ClampEffectDistance(shadow.effectDistance, 0.5f);
                    shadow.useGraphicAlpha = true;
                }
            }
        }

        private static Vector2 ClampEffectDistance(Vector2 distance, float maxAbs)
        {
            return new Vector2(ClampSignedDistance(distance.x, maxAbs), ClampSignedDistance(distance.y, maxAbs));
        }

        private static float ClampSignedDistance(float value, float maxAbs)
        {
            if (Mathf.Approximately(value, 0f))
            {
                return 0f;
            }

            return Mathf.Sign(value) * Mathf.Min(Mathf.Abs(value), maxAbs);
        }

        private static void ApplyTextSupersampling(Text text)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            var scale = rect.localScale;
            if (HasSupersampledTextScale(text))
            {
                return;
            }

            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(text.fontSize * TextSupersampleMultiplier));
            text.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(text.resizeTextMinSize * TextSupersampleMultiplier));
            text.resizeTextMaxSize = Mathf.Max(text.fontSize, Mathf.RoundToInt(text.resizeTextMaxSize * TextSupersampleMultiplier));

            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            ExpandRectForSupersampledText(rect);
            rect.localScale = new Vector3(TextSupersampleScale, TextSupersampleScale, scale.z);
        }

        private static void ExpandRectForSupersampledText(RectTransform rect)
        {
            var anchoredPosition = rect.anchoredPosition;
            var size = rect.rect.size;
            if (size.x > 0.1f)
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x * TextSupersampleMultiplier);
            }
            else
            {
                rect.sizeDelta = new Vector2(rect.sizeDelta.x * TextSupersampleMultiplier, rect.sizeDelta.y);
            }

            if (size.y > 0.1f)
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y * TextSupersampleMultiplier);
            }
            else
            {
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, rect.sizeDelta.y * TextSupersampleMultiplier);
            }

            rect.anchoredPosition = anchoredPosition;
        }

        private static bool HasSupersampledTextScale(Text text)
        {
            var rect = text != null ? text.GetComponent<RectTransform>() : null;
            if (rect == null)
            {
                return false;
            }

            var scale = rect.localScale;
            return Mathf.Approximately(scale.x, TextSupersampleScale) && Mathf.Approximately(scale.y, TextSupersampleScale);
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
                    if (HasSupersampledTextScale(texts[i]))
                    {
                        texts[i].fontSize = Mathf.RoundToInt(18 * TextSupersampleMultiplier);
                        texts[i].resizeTextMaxSize = Mathf.RoundToInt(18 * TextSupersampleMultiplier);
                        texts[i].resizeTextMinSize = Mathf.RoundToInt(10 * TextSupersampleMultiplier);
                    }
                }
            }
        }

        private void ApplyV3NamedState(Transform rootTransform, string resourceName)
        {
            if (rootTransform == null)
            {
                return;
            }

            switch (resourceName)
            {
                case "StartScreen":
                    ApplyV3Standby(rootTransform);
                    break;
                case "BurgerMapSelectScreen":
                    ApplyV3BurgerReveal(rootTransform);
                    break;
                case "DifficultySelectScreen":
                    ApplyV3DifficultySelect(rootTransform);
                    break;
                case "ModeSelectScreen":
                    ApplyV3ModeSelect(rootTransform);
                    break;
                case "MapSelectScreen":
                    ApplyV3MapSelect(rootTransform);
                    break;
                case "MapReadyScreen":
                    ApplyV3MapReady(rootTransform);
                    break;
                case "InGameScreen":
                    ApplyV3InGame(rootTransform);
                    break;
                case "SuccessResultScreen":
                    ApplyV3Result(rootTransform, true);
                    break;
                case "FailResultScreen":
                    ApplyV3Result(rootTransform, false);
                    break;
            }
        }

        private void ApplyV3Standby(Transform rootTransform)
        {
            SetNamedRectCenter(rootTransform, "InsertCoin", new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(900f, 44f));
            SetNamedRectCenter(rootTransform, "Title", new Vector2(0.5f, 0.5f), new Vector2(0f, 115f), new Vector2(1700f, 220f));
            SetNamedRectCenter(rootTransform, "Subtitle", new Vector2(0.5f, 0.5f), new Vector2(0f, -72f), new Vector2(1300f, 58f));
            SetNamedRectCenter(rootTransform, "VersionBar", new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(1120f, 48f));
            SetNamedRectCenter(rootTransform, "Teams", new Vector2(0.5f, 0.5f), new Vector2(0f, -340f), new Vector2(1680f, 190f));
            SetNamedRectCenter(rootTransform, "Footer", new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(900f, 30f));
            ApplyV3VersionChips(rootTransform);

            var cards = FindRectsByPrefix(rootTransform, "TeamCard_");
            for (var i = 0; i < cards.Count; i++)
            {
                var team = i < state.teams.Count ? state.teams[i] : null;
                var accent = team != null ? team.color : Line;
                SetNamedTextBox(cards[i], "Name",
                    new Vector2(0f, 0.56f), new Vector2(1f, 0.88f),
                    new Vector2(20f, 0f), new Vector2(-20f, 0f),
                    TextAnchor.MiddleCenter, 32);
                SetNamedTextBox(cards[i], "Roster",
                    new Vector2(0f, 0.20f), new Vector2(1f, 0.58f),
                    new Vector2(18f, 0f), new Vector2(-18f, 0f),
                    TextAnchor.MiddleCenter, 20);
                SetNamedTextBox(cards[i], "Ready",
                    new Vector2(0f, 0f), new Vector2(1f, 0.20f),
                    new Vector2(20f, 2f), new Vector2(-20f, -2f),
                    TextAnchor.MiddleRight, 13);

                if (team != null)
                {
                    SetNamedText(cards[i], "Name", team.name);
                    SetNamedTextColor(cards[i], "Name", accent);
                }
            }
        }

        private void ApplyV3VersionChips(Transform rootTransform)
        {
            var versionBar = FindRect(rootTransform, "VersionBar");
            if (versionBar == null)
            {
                return;
            }

            var layout = versionBar.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 20f;
            }

            var chips = FindRectsByPrefix(versionBar, "Chip_");
            for (var i = 0; i < chips.Count; i++)
            {
                var chip = chips[i];
                var layoutElement = chip.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = chip.gameObject.AddComponent<LayoutElement>();
                }

                layoutElement.preferredWidth = 240f;
                layoutElement.minWidth = 190f;
                layoutElement.preferredHeight = 34f;
                layoutElement.minHeight = 34f;

                chip.anchorMin = new Vector2(0f, 0.5f);
                chip.anchorMax = new Vector2(1f, 0.5f);
                chip.pivot = new Vector2(0.5f, 0.5f);
                chip.sizeDelta = new Vector2(chip.sizeDelta.x, 34f);

                var text = GetFirstText(chip);
                var accent = text != null ? text.color : Line;
                SetPanelVisual(chip, new Color(0.045f, 0.012f, 0.10f, 0.96f), accent, 2f);
                SetNamedTextBox(chip, "Text",
                    Vector2.zero, Vector2.one,
                    new Vector2(12f, 0f), new Vector2(-12f, 0f),
                    TextAnchor.MiddleCenter, 14);
            }
        }

        private void ApplyV3BurgerReveal(Transform rootTransform)
        {
            var pickedCount = 0;
            for (var i = 0; i < state.mapPool.Count; i++)
            {
                if (state.mapPool[i].isBurger)
                {
                    pickedCount += 1;
                }
            }

            SetNamedText(rootTransform, "CapR", $"{pickedCount} / {Mathf.Max(1, state.mapPool.Count)} PICKED");
            SetNamedText(rootTransform, "Footer", $"PRESS [ SPACE ] TO REROLL - {pickedCount}/{Mathf.Max(1, state.mapPool.Count)} LOCKED");

            var usedCells = new HashSet<RectTransform>();
            for (var i = 0; i < state.mapPool.Count; i++)
            {
                var map = state.mapPool[i];
                var cell = FindRect(rootTransform, "Cell_" + map.id);
                if (cell == null)
                {
                    continue;
                }

                usedCells.Add(cell);
                UpdateV3BurgerMapCell(cell, map);
            }

            var cells = FindRectsByPrefix(rootTransform, "Cell_");
            var fallbackIndex = 0;
            for (var i = 0; i < cells.Count && fallbackIndex < state.mapPool.Count; i++)
            {
                if (usedCells.Contains(cells[i]))
                {
                    continue;
                }

                UpdateV3BurgerMapCell(cells[i], state.mapPool[fallbackIndex]);
                fallbackIndex += 1;
            }
        }

        private void UpdateV3BurgerMapCell(RectTransform cell, BossRaidMap map)
        {
            if (cell == null || map == null)
            {
                return;
            }

            var isBurger = map.isBurger;
            SetPanelVisual(cell,
                isBurger ? new Color(0.14f, 0.095f, 0.035f, 0.98f) : PanelAlt,
                Line,
                1.4f);

            SetNamedText(cell, "Id", GetMapDisplayId(map));
            SetNamedText(cell, "Title", map.title);
            SetNamedText(cell, "Meta", BuildMapMeta(map));
            SetNamedTextColor(cell, "Id", isBurger ? Gold : Cyan);
            SetNamedTextContainedBox(cell, "Title",
                new Vector2(0f, 0.34f), new Vector2(1f, 0.72f),
                new Vector2(14f, 0f), new Vector2(-14f, 0f),
                TextAnchor.MiddleCenter, 24, 14);
            SetNamedTextContainedBox(cell, "Meta",
                new Vector2(0f, 0.04f), new Vector2(1f, 0.32f),
                new Vector2(14f, 0f), new Vector2(isBurger ? -112f : -14f, 0f),
                TextAnchor.MiddleCenter, 20, 13);
            SetNamedObjectActive(cell, "Burger", isBurger);
            SetNamedObjectActive(cell, "Stamp", isBurger);

            if (isBurger)
            {
                var burger = EnsureText(cell, "Burger",
                    new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-46f, -42f), new Vector2(-10f, -8f),
                    28, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                burger.text = "★";
                burger.color = Gold;
                var burgerImage = burger.GetComponent<Image>();
                if (burgerImage != null)
                {
                    burgerImage.color = Color.clear;
                    burgerImage.raycastTarget = false;
                }

                EnsureTag(cell, "Stamp", "JACKPOT", Gold, Background,
                    new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(-100f, 8f), new Vector2(-12f, 30f), 13);
            }
        }

        private void ApplyV3DifficultySelect(Transform rootTransform)
        {
            SetNamedText(rootTransform, "Small", $"STAGE {Mathf.Clamp(state.roundIndex + 1, 1, 8)} / 8 - {GetCurrentTeamName()}");

            var cards = FindRectsByPrefix(rootTransform, "Diff_");
            for (var i = 0; i < cards.Count; i++)
            {
                var difficulty = FindDifficultyForCard(cards[i].name, i);
                if (difficulty == null)
                {
                    cards[i].gameObject.SetActive(false);
                    continue;
                }

                cards[i].gameObject.SetActive(true);
                UpdateV3DifficultyCard(cards[i], difficulty, i);
            }

            SetNamedText(rootTransform, "Footer", "[1][2][3] / [E][N][H] SELECT - [ENTER] CONFIRM");
        }

        private void UpdateV3DifficultyCard(RectTransform card, BossRaidDifficultyConfig difficulty, int index)
        {
            var isSelected = string.Equals(difficulty.id, state.difficulty, StringComparison.OrdinalIgnoreCase);
            var accent = GetV3DifficultyColor(difficulty.id);
            SetPanelVisual(card,
                isSelected ? new Color(accent.r * 0.18f, accent.g * 0.18f, accent.b * 0.18f, 0.98f) : PanelAlt,
                Line,
                1.4f);

            SetNamedText(card, "Ribbon", $"STAGE {index + 1} - {ToUpperSafe(difficulty.label)}");
            SetNamedText(card, "Name", ToUpperSafe(difficulty.label));
            SetNamedText(card, "HpVal", FormatNumber(difficulty.bossHp));
            SetNamedText(card, "PrVal", FormatWon(difficulty.prize));
            SetNamedTextColor(card, "Name", accent);
            SetNamedTextColor(card, "HpVal", isSelected ? accent : White);
            SetNamedTextColor(card, "PrVal", Cyan);
            ApplyV3DifficultyCardLayout(card);

            if (isSelected)
            {
                EnsureTag(card, "Pick", "SELECTED", Background, accent,
                    new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-150f, -50f), new Vector2(-18f, -22f), 13);
            }
            else
            {
                SetNamedObjectActive(card, "Pick", false);
            }
        }

        private static void ApplyV3DifficultyCardLayout(RectTransform card)
        {
            SetNamedTextBox(card, "Icon",
                new Vector2(0f, 0.66f), new Vector2(1f, 0.86f),
                new Vector2(24f, 0f), new Vector2(-24f, 0f),
                TextAnchor.MiddleCenter, 58);
            SetNamedTextBox(card, "Name",
                new Vector2(0f, 0.48f), new Vector2(1f, 0.66f),
                new Vector2(24f, 0f), new Vector2(-24f, 0f),
                TextAnchor.MiddleCenter, 52);
            SetNamedTextBox(card, "HpLbl",
                new Vector2(0f, 0.34f), new Vector2(1f, 0.42f),
                new Vector2(24f, 0f), new Vector2(-24f, 0f),
                TextAnchor.MiddleCenter, 14);
            SetNamedTextBox(card, "HpVal",
                new Vector2(0f, 0.24f), new Vector2(1f, 0.36f),
                new Vector2(24f, 0f), new Vector2(-24f, 0f),
                TextAnchor.MiddleCenter, 40);
            SetNamedTextBox(card, "PrLbl",
                new Vector2(0f, 0.12f), new Vector2(1f, 0.20f),
                new Vector2(24f, 0f), new Vector2(-24f, 0f),
                TextAnchor.MiddleCenter, 14);
            SetNamedTextBox(card, "PrVal",
                new Vector2(0f, 0.02f), new Vector2(1f, 0.15f),
                new Vector2(24f, 0f), new Vector2(-24f, 0f),
                TextAnchor.MiddleCenter, 40);

            var divider = FindRect(card, "Divider");
            if (divider != null)
            {
                divider.anchorMin = new Vector2(0f, 0.44f);
                divider.anchorMax = new Vector2(1f, 0.44f);
                divider.offsetMin = new Vector2(32f, -1f);
                divider.offsetMax = new Vector2(-32f, 1f);
            }
        }

        private void ApplyV3ModeSelect(Transform rootTransform)
        {
            var modes = GetModes();
            SetNamedText(rootTransform, "Small", $"TEAM {GetCurrentTeamName()} - BOSS {GetCurrentDifficultyLabel()} - STAGE {Mathf.Clamp(state.roundIndex + 1, 1, 8)}/8");
            SetNamedText(rootTransform, "Footer", "PRESS [ SPACE ] TO SPIN THE WHEEL");

            var tiles = FindRectsByPrefix(rootTransform, "Mode_");
            for (var i = 0; i < tiles.Count; i++)
            {
                var mode = i < modes.Count ? modes[i] : ExtractNameSuffix(tiles[i].name, "Mode_");
                if (string.IsNullOrEmpty(mode))
                {
                    tiles[i].gameObject.SetActive(false);
                    continue;
                }

                tiles[i].gameObject.SetActive(true);
                UpdateV3ModeTile(tiles[i], mode);
            }
        }

        private void UpdateV3ModeTile(RectTransform tile, string mode)
        {
            var isSelected = string.Equals(mode, state.selectedMode, StringComparison.OrdinalIgnoreCase);
            SetPanelVisual(tile,
                isSelected ? new Color(0.13f, 0.055f, 0.12f, 0.98f) : PanelAlt,
                isSelected ? Magenta : Line,
                isSelected ? 3f : 1.4f);

            SetNamedText(tile, "Label", mode);
            SetNamedText(tile, "Desc", GetModeDescription(mode));
            SetNamedText(tile, "Count", $"{CountModeMaps(mode)} MAPS");
            SetNamedTextColor(tile, "Label", isSelected ? Magenta : White);
            SetNamedObjectActive(tile, "Spin", isSelected);

            if (isSelected)
            {
                EnsureTag(tile, "Spin", "NOW SPINNING", Magenta, Background,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-150f, -10f), new Vector2(150f, 16f), 14);
            }
        }

        private void ApplyV3MapSelect(Transform rootTransform)
        {
            var selectedMode = state.selectedMode;
            if (string.IsNullOrEmpty(selectedMode))
            {
                var modes = GetModes();
                selectedMode = modes.Count > 0 ? modes[0] : "";
            }

            SetNamedText(rootTransform, "Small", $"STAGE {Mathf.Clamp(state.roundIndex + 1, 1, 8)} / 8 - MODE LOCKED");
            SetNamedText(rootTransform, "HR", selectedMode);
            SetNamedText(rootTransform, "Footer", "PRESS [ SPACE ] TO DRAW THE MAP");

            var maps = GetMapsForMode(selectedMode);
            var highlightedMap = state.SelectedMap;
            if (highlightedMap == null && maps.Count > 0)
            {
                highlightedMap = maps[0];
            }

            ApplyV3MapLotteryHeader(rootTransform, selectedMode, highlightedMap);

            var cells = FindRectsByPrefix(rootTransform, "Cell_");
            for (var i = 0; i < cells.Count; i++)
            {
                if (i >= maps.Count)
                {
                    cells[i].gameObject.SetActive(false);
                    continue;
                }

                cells[i].gameObject.SetActive(true);
                UpdateV3MapRouletteCell(cells[i], maps[i]);
            }
        }

        private void UpdateV3MapRouletteCell(RectTransform cell, BossRaidMap map)
        {
            if (cell == null || map == null)
            {
                return;
            }

            var isSelected = string.Equals(map.id, state.selectedMapId, StringComparison.OrdinalIgnoreCase);
            var isRoundMap = state.selectedRoundMapIds.Count == 0 || state.selectedRoundMapIds.Contains(map.id);
            var isPlayed = map.played || !isRoundMap;
            var fill = isSelected
                ? new Color(0.13f, 0.055f, 0.12f, 0.98f)
                : map.isBurger
                    ? new Color(0.14f, 0.095f, 0.035f, 0.98f)
                    : PanelAlt;

            SetPanelVisual(cell, fill, isSelected ? Magenta : Line, isSelected ? 3f : 1.4f);
            SetCanvasGroupAlpha(cell, isPlayed ? 0.35f : 1f);

            SetNamedText(cell, "IdTag", GetMapDisplayId(map));
            SetNamedText(cell, "ModeTag", map.mode);
            SetNamedText(cell, "Title", map.title);
            SetNamedText(cell, "Diff", map.difficultyName);
            SetNamedText(cell, "Creator", BuildMapCreator(map));
            SetNamedTextColor(cell, "Title", isSelected ? Magenta : White);
            SetNamedTextColor(cell, "IdTag", map.isBurger ? Gold : Cyan);
            SetNamedTextContainedBox(cell, "Title",
                new Vector2(0f, 0.50f), new Vector2(1f, 0.72f),
                new Vector2(28f, 0f), new Vector2(-28f, 0f),
                TextAnchor.MiddleCenter, 32, 15);
            SetNamedTextContainedBox(cell, "Diff",
                new Vector2(0f, 0.30f), new Vector2(1f, 0.50f),
                new Vector2(28f, 0f), new Vector2(-28f, 0f),
                TextAnchor.MiddleCenter, 22, 13);
            SetNamedTextContainedBox(cell, "Creator",
                new Vector2(0f, 0.08f), new Vector2(1f, 0.26f),
                new Vector2(28f, 0f), new Vector2(-28f, 0f),
                TextAnchor.MiddleCenter, 20, 13);
            SetNamedObjectActive(cell, "Burger", map.isBurger);
            SetNamedObjectActive(cell, "BurgerBadge", map.isBurger);
            SetNamedObjectActive(cell, "Spin", isSelected);

            if (map.isBurger)
            {
                EnsureTag(cell, "BurgerBadge", "BGR", Gold, Background,
                    new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-66f, -50f), new Vector2(-14f, -14f), 14);
            }

            if (isSelected)
            {
                EnsureTag(cell, "Spin", "NOW SPINNING", Magenta, Background,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-150f, -10f), new Vector2(150f, 16f), 14);
            }

            if (isPlayed)
            {
                var played = EnsureTag(cell, "Played", "PLAYED", Background, Muted,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-150f, -34f), new Vector2(150f, 34f), 38);
                played.localRotation = Quaternion.Euler(0f, 0f, 15f);
            }
            else
            {
                SetNamedObjectActive(cell, "Played", false);
            }
        }

        private void ApplyV3MapLotteryHeader(Transform rootTransform, string selectedMode, BossRaidMap map)
        {
            var titleRow = FindRect(rootTransform, "TitleRow");
            if (titleRow != null)
            {
                var layout = titleRow.GetComponent<HorizontalLayoutGroup>();
                if (layout != null)
                {
                    layout.enabled = false;
                }

                titleRow.anchorMin = new Vector2(0.5f, 1f);
                titleRow.anchorMax = new Vector2(0.5f, 1f);
                titleRow.pivot = new Vector2(0.5f, 0.5f);
                titleRow.anchoredPosition = new Vector2(0f, -145f);
                titleRow.sizeDelta = new Vector2(1280f, 104f);
            }

            SetNamedText(rootTransform, "HR", selectedMode);
            SetNamedTextBox(rootTransform, "HR",
                new Vector2(0f, 0f), new Vector2(0.35f, 1f),
                new Vector2(0f, 0f), new Vector2(-24f, 0f),
                TextAnchor.MiddleRight, 70);
            SetNamedTextBox(rootTransform, "Lottery",
                new Vector2(0.35f, 0f), new Vector2(1f, 1f),
                new Vector2(24f, 0f), Vector2.zero,
                TextAnchor.MiddleLeft, 70);

            var info = EnsureText(rootTransform, "SelectedInfo",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-760f, -232f), new Vector2(760f, -198f),
                20, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            info.text = BuildMapDetailLine(map);
            info.color = map != null && map.isBurger ? Gold : Muted;
        }

        private void ApplyV3Hud(Transform rootTransform, string screenLabel, Color labelColor)
        {
            var map = state.SelectedMap;
            var difficulty = state.CurrentDifficulty;
            var team = state.CurrentTeam;
            var stage = Mathf.Clamp(state.roundIndex + 1, 1, 8);

            SetNamedText(rootTransform, "Event", string.IsNullOrEmpty(state.eventTitle) ? "BOSS RAID" : state.eventTitle);
            SetNamedText(rootTransform, "Name", team != null ? team.name : "NO TEAM");
            SetNamedText(rootTransform, "Label", screenLabel);
            SetNamedText(rootTransform, "Map", BuildHudMapLine(map, difficulty));
            SetNamedText(rootTransform, "Conn", string.IsNullOrEmpty(state.connectionLabel) ? "BRIDGE ONLINE" : state.connectionLabel);
            SetNamedTextColor(rootTransform, "Label", labelColor);
            SetNamedTextContainedBox(rootTransform, "Map",
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(20f, 12f), new Vector2(-20f, 38f),
                TextAnchor.MiddleCenter, 24, 13);

            SetHudStat(rootTransform, "Stat_STAGE", "STAGE", $"{stage}/8", Cyan);
            var prizeValue = state.prizePool > 0 ? state.prizePool : difficulty != null ? difficulty.prize : 0;
            SetHudStat(rootTransform, "Stat_PRIZE", "PRIZE", FormatCompactPrize(prizeValue), Gold);
            SetHudStat(rootTransform, "Stat_BURGER", "BURGER", $"x{state.burgerCount}", Magenta);
            SetHudStat(rootTransform, "Stat_RECORD", "RECORD", $"{state.clearCount}-{state.failCount}", state.lastResult == BossRaidResults.Fail ? Red : Green);
        }

        private void ApplyV3SelectedMapBanner(Transform rootTransform)
        {
            var map = state.SelectedMap;
            var difficulty = state.CurrentDifficulty;
            var stage = Mathf.Clamp(state.roundIndex + 1, 1, 8);
            var mode = GetDisplayMode(map);
            var difficultyLabel = difficulty != null ? ToUpperSafe(difficulty.label) : ToUpperSafe(state.difficulty);
            var bossHp = difficulty != null ? difficulty.bossHp : state.bossHp;

            var banner = FindRect(rootTransform, "MapBanner");
            if (banner != null)
            {
                SetNamedText(banner, "Cap", $"STAGE {stage:00} / 08");
                SetNamedText(banner, "Title", map != null ? map.title : "NO MAP SELECTED");
                SetNamedText(banner, "Subline", BuildMapDetailLine(map));
                SetNamedText(banner, "V_MODE", ToUpperSafe(mode));
                SetNamedText(banner, "V_BOSS", difficultyLabel);
                SetNamedText(banner, "V_HP", FormatNumber(bossHp));
                SetNamedTextColor(banner, "V_BOSS", GetV3DifficultyColor(state.difficulty));
                SetNamedTextContainedBox(banner, "Title",
                    new Vector2(0f, 0.30f), new Vector2(1f, 0.85f),
                    new Vector2(36f, 0f), new Vector2(-420f, 0f),
                    TextAnchor.MiddleLeft, 64, 24);
                SetNamedTextContainedBox(banner, "Subline",
                    new Vector2(0f, 0f), new Vector2(1f, 0.32f),
                    new Vector2(36f, 16f), new Vector2(-420f, 0f),
                    TextAnchor.MiddleLeft, 20, 12);
                SetNamedObjectActive(banner, "BurgerStamp", map != null && map.isBurger);
            }
        }

        private void ApplyV3MapReady(Transform rootTransform)
        {
            ApplyV3Hud(rootTransform, "MAP READY", Magenta);
            ApplyV3SelectedMapBanner(rootTransform);
            ApplyV3SpectatorLayout(rootTransform, new Vector2(-200f, -650f), MapReadySpectatorViewportSize);
            HideSpectatorNameTags(rootTransform);
            ApplyV3RoomChat(rootTransform);
        }

        private void ApplyV3InGame(Transform rootTransform)
        {
            ApplyV3Hud(rootTransform, "RAID LIVE", Magenta);
            ApplyV3SpectatorLayout(rootTransform, new Vector2(0f, -500f), InGameSpectatorViewportSize);
            HideInGameSlotNames(rootTransform);
            ApplyV3PlayerScoreRow(rootTransform);
            ApplyV3BossBar(rootTransform);
        }

        private static void ApplyV3SpectatorLayout(Transform rootTransform, Vector2 anchoredCenter, Vector2 viewportSize)
        {
            var spectators = FindRect(rootTransform, "Spectators");
            if (spectators == null)
            {
                return;
            }

            var slotCount = Mathf.Max(1, spectators.childCount);
            spectators.anchorMin = new Vector2(0.5f, 1f);
            spectators.anchorMax = new Vector2(0.5f, 1f);
            spectators.pivot = new Vector2(0.5f, 0.5f);
            spectators.anchoredPosition = anchoredCenter;
            spectators.sizeDelta = GetSpectatorRowSize(slotCount, viewportSize);

            var layout = spectators.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = SpectatorViewportSpacing;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.childAlignment = TextAnchor.MiddleCenter;
            }

            for (var i = 0; i < spectators.childCount; i++)
            {
                var slot = spectators.GetChild(i).GetComponent<RectTransform>();
                if (slot == null)
                {
                    continue;
                }

                ApplySpectatorLayoutElement(slot, viewportSize);
                slot.sizeDelta = viewportSize;
            }
        }

        private void HideSpectatorNameTags(Transform rootTransform)
        {
            var spectators = FindRect(rootTransform, "Spectators");
            if (spectators == null)
            {
                return;
            }

            for (var i = 0; i < spectators.childCount; i++)
            {
                SetNamedObjectActive(spectators.GetChild(i), "Tag", false);
            }
        }

        private void HideInGameSlotNames(Transform rootTransform)
        {
            var spectators = FindRect(rootTransform, "Spectators");
            if (spectators == null)
            {
                return;
            }

            for (var i = 0; i < spectators.childCount; i++)
            {
                SetNamedObjectActive(spectators.GetChild(i), "Name", false);
            }
        }

        private void ApplyV3RoomChat(Transform rootTransform)
        {
            var chat = FindRect(rootTransform, "Chat");
            if (chat == null)
            {
                return;
            }

            var lines = BuildRoomChatLines(5);
            if (lines.Count == 0)
            {
                for (var i = 0; i < 5; i++)
                {
                    SetNamedObjectActive(chat, "Who" + i, false);
                    SetNamedObjectActive(chat, "Msg" + i, false);
                    SetNamedText(chat, "Who" + i, "");
                    SetNamedText(chat, "Msg" + i, "");
                }

                return;
            }

            for (var i = 0; i < 5; i++)
            {
                var hasLine = i < lines.Count;
                SetNamedObjectActive(chat, "Who" + i, hasLine);
                SetNamedObjectActive(chat, "Msg" + i, hasLine);

                if (!hasLine)
                {
                    if (i != 0)
                    {
                        SetNamedText(chat, "Who" + i, "");
                        SetNamedText(chat, "Msg" + i, "");
                    }

                    continue;
                }

                SetNamedText(chat, "Who" + i, lines[i].sender);
                SetNamedText(chat, "Msg" + i, lines[i].message);
                SetNamedTextColor(chat, "Who" + i, lines[i].color);
                SetNamedTextColor(chat, "Msg" + i, White);
            }
        }

        private void ApplyV3PlayerScoreRow(Transform rootTransform)
        {
            var row = FindRect(rootTransform, "PlayerRow");
            if (row == null)
            {
                return;
            }

            for (var i = 0; i < row.childCount; i++)
            {
                var tile = row.GetChild(i);
                var label = $"P{i + 1}";
                var player = GetCurrentTeamPlayer(i);
                var score = player != null ? player.score : 0;

                SetNamedObjectActive(tile, "Idx", true);
                SetNamedObjectActive(tile, "Name", false);
                SetNamedObjectActive(tile, "Score", true);
                SetNamedText(tile, "Idx", label);
                SetNamedText(tile, "Score", FormatPlayerScore(score));
                SetNamedTextColor(tile, "Idx", Cyan);
                SetNamedTextColor(tile, "Score", Gold);

                var idxRect = FindRect(tile, "Idx");
                if (idxRect != null)
                {
                    idxRect.anchorMin = Vector2.zero;
                    idxRect.anchorMax = new Vector2(0.24f, 1f);
                    idxRect.offsetMin = new Vector2(18f, 0f);
                    idxRect.offsetMax = new Vector2(-4f, 0f);
                }

                var scoreRect = FindRect(tile, "Score");
                if (scoreRect != null)
                {
                    scoreRect.anchorMin = new Vector2(0.24f, 0f);
                    scoreRect.anchorMax = Vector2.one;
                    scoreRect.offsetMin = new Vector2(8f, 0f);
                    scoreRect.offsetMax = new Vector2(-20f, 0f);
                }

                var idxText = GetFirstText(idxRect);
                if (idxText != null)
                {
                    idxText.alignment = TextAnchor.MiddleCenter;
                }

                var scoreText = GetFirstText(scoreRect);
                if (scoreText != null)
                {
                    scoreText.alignment = TextAnchor.MiddleRight;
                    scoreText.resizeTextMaxSize = Mathf.Max(scoreText.resizeTextMaxSize, scoreText.fontSize);
                }
            }
        }

        private BossRaidPlayer GetCurrentTeamPlayer(int index)
        {
            var team = state.CurrentTeam;
            if (team == null || team.players == null || index < 0 || index >= team.players.Count)
            {
                return null;
            }

            return team.players[index];
        }

        private void ApplyV3BossBar(Transform rootTransform)
        {
            var difficulty = state.CurrentDifficulty;
            var difficultyLabel = difficulty != null ? ToUpperSafe(difficulty.label) : ToUpperSafe(state.difficulty);
            var bossHp = difficulty != null ? difficulty.bossHp : state.bossHp;
            var stage = Mathf.Clamp(state.roundIndex + 1, 1, 8);
            var ratio = bossHp <= 0 ? 1f : Mathf.Clamp01((float)state.totalScore / bossHp);
            var percent = ratio * 100f;

            SetNamedText(rootTransform, "CapR", $"STAGE {stage:00}");
            SetNamedText(rootTransform, "Meta", $"BOSS HP   - {difficultyLabel} -");
            SetNamedText(rootTransform, "Dmg", $"{FormatNumber(state.totalScore)}  / {FormatNumber(bossHp)}");
            SetNamedText(rootTransform, "Pct", $"{percent:0.0}%");

            var fill = FindRect(rootTransform, "Fill");
            if (fill != null)
            {
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(ratio, 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
            }

            var fillCap = FindRect(rootTransform, "FillCap");
            if (fillCap != null)
            {
                fillCap.anchorMin = new Vector2(ratio, 0f);
                fillCap.anchorMax = new Vector2(ratio, 1f);
                fillCap.offsetMin = new Vector2(-4f, 0f);
                fillCap.offsetMax = Vector2.zero;
                fillCap.gameObject.SetActive(ratio > 0.01f && ratio < 0.99f);
            }
        }

        private void ApplyV3Result(Transform rootTransform, bool isClear)
        {
            ApplyV3Hud(rootTransform, isClear ? "STAGE CLEAR" : "GAME OVER", isClear ? Green : Red);

            var message = string.IsNullOrEmpty(state.resultMessage)
                ? BuildResultMessage(isClear)
                : state.resultMessage;

            SetNamedText(rootTransform, "Message", "> " + ToUpperSafe(message));
            SetNamedText(rootTransform, "MapInfo", BuildResultMapInfo());
            CenterNamedText(rootTransform, "Message", new Vector2(0f, -540f), new Vector2(3400f, 100f));
            CenterNamedText(rootTransform, "MapInfo", new Vector2(0f, -590f), new Vector2(3400f, 80f));
            ApplyV3ResultBossBar(rootTransform, isClear);
            ApplyV3ResultStats(rootTransform, isClear);
        }

        private void ApplyV3ResultBossBar(Transform rootTransform, bool isClear)
        {
            var difficulty = state.CurrentDifficulty;
            var bossHp = difficulty != null ? difficulty.bossHp : state.bossHp;
            var ratio = bossHp <= 0 ? (isClear ? 1f : 0f) : Mathf.Clamp01((float)state.totalScore / bossHp);
            if (isClear)
            {
                ratio = Mathf.Max(ratio, 1f);
            }

            var bossBar = FindRect(rootTransform, "BossBar");
            if (bossBar == null)
            {
                return;
            }

            var fill = FindRect(bossBar, "Fill");
            if (fill != null)
            {
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(ratio, 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;

                var image = fill.GetComponent<Image>();
                if (image != null)
                {
                    image.color = isClear ? Green : Red;
                }
            }

            SetNamedText(bossBar, "Lbl", $"{ratio * 100f:0.0}% - {(isClear ? "BOSS DEFEATED" : "BOSS SURVIVED")}");
        }

        private void ApplyV3ResultStats(Transform rootTransform, bool isClear)
        {
            var stats = FindRect(rootTransform, "Stats");
            if (stats == null)
            {
                return;
            }

            var difficulty = state.CurrentDifficulty;
            var clearBonus = difficulty != null ? difficulty.prize : 0;
            SetV3ResultStat(stats, "Stat_PRIZE POOL", "PRIZE POOL", FormatNumber(state.prizePool), isClear && clearBonus > 0 ? $"KRW - +{FormatNumber(clearBonus)}" : "KRW - UNCHANGED", Gold);
            SetV3ResultStat(stats, "Stat_BURGERS", "BURGERS", $"x{state.burgerCount}", "earned", Magenta);
            SetV3ResultStat(stats, "Stat_BURGER MISS", "BURGER MISS", $"x{state.burgerMissCount}", "missed", Red);
            SetV3ResultStat(stats, "Stat_RECORD", "RECORD", $"{state.clearCount}-{state.failCount}", "clear / fail", isClear ? Green : Red);
        }

        private static void SetV3ResultStat(Transform statsRoot, string tileName, string label, string value, string unit, Color valueColor)
        {
            var tile = FindRect(statsRoot, tileName);
            if (tile == null)
            {
                return;
            }

            SetNamedText(tile, "Lbl", label);
            SetNamedText(tile, "Val", value);
            SetNamedText(tile, "Unit", unit);
            SetNamedTextColor(tile, "Val", valueColor);
            SetNamedTextContainedBox(tile, "Val",
                new Vector2(0f, 0.18f), new Vector2(1f, 0.82f),
                Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter, 72, 24);
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
                    if (activeContext != null && !string.IsNullOrEmpty(activeContext.mode) && binding.key == "ModeName")
                    {
                        color = activeContext.selected ? Cyan : White;
                    }

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
                    return new PrefabContext { statLabel = "Record", statValue = $"{state.clearCount}-{state.failCount}", statColor = state.lastResult == BossRaidResults.Clear ? Green : Red, index = index };
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
                else if (!string.IsNullOrEmpty(context.mode))
                {
                    outline.effectColor = context.selected ? Cyan : Line;
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
            else if (!string.IsNullOrEmpty(context.mode))
            {
                image.color = context.selected
                    ? new Color(0.06f, 0.14f, 0.18f, 0.98f)
                    : Panel;
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
                    return $"{state.clearCount}-{state.failCount}";
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

        private static RectTransform EnsureTag(RectTransform parent, string name, string value, Color fill, Color textColor, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, int textSize)
        {
            var tag = FindRect(parent, name);
            if (tag == null || tag.parent != parent)
            {
                tag = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            }
            else
            {
                tag.anchorMin = anchorMin;
                tag.anchorMax = anchorMax;
                tag.offsetMin = offsetMin;
                tag.offsetMax = offsetMax;
            }

            tag.gameObject.SetActive(true);
            tag.SetAsLastSibling();

            var image = tag.GetComponent<Image>();
            if (image == null)
            {
                image = tag.gameObject.AddComponent<Image>();
            }

            image.color = fill;
            image.raycastTarget = false;

            var outline = tag.GetComponent<Outline>();
            if (outline == null)
            {
                outline = tag.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = textColor;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            var text = GetFirstText(tag);
            if (text == null)
            {
                var textRect = CreateRect("T", tag, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                text = ConfigureText(textRect, value, textSize, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            text.text = value;
            text.color = textColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = Mathf.Max(text.fontSize, textSize);
            text.resizeTextMaxSize = Mathf.Max(text.resizeTextMaxSize, text.fontSize);
            text.raycastTarget = false;
            return tag;
        }

        private static void SetPanelVisual(RectTransform rect, Color fill, Color border, float distance)
        {
            if (rect == null)
            {
                return;
            }

            var image = rect.GetComponent<Image>();
            if (image != null)
            {
                image.color = fill;
                image.raycastTarget = false;
            }

            if (SetSharpBorderColor(rect, border))
            {
                var existingOutline = rect.GetComponent<Outline>();
                if (existingOutline != null)
                {
                    existingOutline.enabled = false;
                }

                return;
            }

            var outline = rect.GetComponent<Outline>();
            if (outline == null)
            {
                outline = rect.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = border;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = false;
        }

        private static bool SetSharpBorderColor(RectTransform rect, Color color)
        {
            if (rect == null)
            {
                return false;
            }

            var updated = false;
            updated |= SetDirectChildImageColor(rect, "B_Top", color);
            updated |= SetDirectChildImageColor(rect, "B_Bottom", color);
            updated |= SetDirectChildImageColor(rect, "B_Left", color);
            updated |= SetDirectChildImageColor(rect, "B_Right", color);
            return updated;
        }

        private static bool SetDirectChildImageColor(Transform parent, string name, Color color)
        {
            if (parent == null)
            {
                return false;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var image = child.GetComponent<Image>();
                if (image == null)
                {
                    return false;
                }

                image.color = color;
                image.raycastTarget = false;
                return true;
            }

            return false;
        }

        private static void SetCanvasGroupAlpha(RectTransform rect, float alpha)
        {
            if (rect == null)
            {
                return;
            }

            var group = rect.GetComponent<CanvasGroup>();
            if (group == null && alpha < 0.99f)
            {
                group = rect.gameObject.AddComponent<CanvasGroup>();
            }

            if (group != null)
            {
                group.alpha = alpha;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        private BossRaidDifficultyConfig FindDifficultyForCard(string cardName, int fallbackIndex)
        {
            var suffix = ExtractNameSuffix(cardName, "Diff_");
            for (var i = 0; i < state.difficulties.Count; i++)
            {
                var difficulty = state.difficulties[i];
                if (string.Equals(suffix, difficulty.id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(suffix, difficulty.label, StringComparison.OrdinalIgnoreCase))
                {
                    return difficulty;
                }
            }

            return fallbackIndex >= 0 && fallbackIndex < state.difficulties.Count ? state.difficulties[fallbackIndex] : null;
        }

        private static Color GetV3DifficultyColor(string difficulty)
        {
            if (string.Equals(difficulty, BossRaidDifficulties.Easy, StringComparison.OrdinalIgnoreCase))
            {
                return Green;
            }

            if (string.Equals(difficulty, BossRaidDifficulties.Hard, StringComparison.OrdinalIgnoreCase))
            {
                return Magenta;
            }

            return Gold;
        }

        private string GetCurrentTeamName()
        {
            return state.CurrentTeam != null ? state.CurrentTeam.name : "NO TEAM";
        }

        private string GetCurrentDifficultyLabel()
        {
            return state.CurrentDifficulty != null ? ToUpperSafe(state.CurrentDifficulty.label) : "PENDING";
        }

        private static string FormatCompactHp(int value)
        {
            return value >= 1000 ? $"{value / 1000:N0}K" : FormatNumber(value);
        }

        private static string FormatPlayerScore(int value)
        {
            return value <= 0 ? "000,000" : FormatNumber(value);
        }

        private static string GetMapDisplayId(BossRaidMap map)
        {
            if (map == null)
            {
                return "";
            }

            return string.IsNullOrEmpty(map.id) ? map.mode : map.id;
        }

        private static string BuildMapMeta(BossRaidMap map)
        {
            if (map == null)
            {
                return "";
            }

            if (!string.IsNullOrEmpty(map.mapper))
            {
                return ToUpperSafe(map.mapper);
            }

            if (!string.IsNullOrEmpty(map.artist))
            {
                return ToUpperSafe(map.artist);
            }

            return ToUpperSafe(map.difficultyName);
        }

        private static string BuildMapCreator(BossRaidMap map)
        {
            if (map == null)
            {
                return "";
            }

            var artist = string.IsNullOrEmpty(map.artist) ? "UNKNOWN" : ToUpperSafe(map.artist);
            var mapper = string.IsNullOrEmpty(map.mapper) ? "UNKNOWN" : ToUpperSafe(map.mapper);
            return $"BY  {artist} / {mapper}";
        }

        private static string BuildMapDetailLine(BossRaidMap map)
        {
            if (map == null)
            {
                return "MAP INFO PENDING";
            }

            var title = string.IsNullOrEmpty(map.title) ? "NO MAP" : ToUpperSafe(map.title);
            var difficulty = string.IsNullOrEmpty(map.difficultyName) ? "NO DIFFICULTY" : ToUpperSafe(map.difficultyName);
            var artist = string.IsNullOrEmpty(map.artist) ? "UNKNOWN ARTIST" : ToUpperSafe(map.artist);
            var mapper = string.IsNullOrEmpty(map.mapper) ? "UNKNOWN MAPPER" : ToUpperSafe(map.mapper);
            return $"{title}  /  {difficulty}  /  ARTIST {artist}  /  MAPPER {mapper}";
        }

        private string BuildHudMapLine(BossRaidMap map, BossRaidDifficultyConfig difficulty)
        {
            if (map == null)
            {
                return "MAP PENDING";
            }

            var mode = GetDisplayMode(map);
            var difficultyLabel = difficulty != null ? difficulty.label : state.difficulty;
            return $"[{ToUpperSafe(mode)}]  {map.title}  -  {map.difficultyName}  /  {ToUpperSafe(difficultyLabel)}";
        }

        private string GetDisplayMode(BossRaidMap map)
        {
            if (map != null && !string.IsNullOrEmpty(map.mode))
            {
                return map.mode;
            }

            return state != null ? state.selectedMode : "";
        }

        private static string FormatCompactPrize(int value)
        {
            if (value <= 0)
            {
                return "0";
            }

            return value >= 1000 ? $"{value / 1000:N0}K" : value.ToString("N0");
        }

        private string BuildResultMapInfo()
        {
            var map = state.SelectedMap;
            var difficulty = state.CurrentDifficulty;
            var mapName = map != null && !string.IsNullOrEmpty(map.title) ? map.title : "NO MAP";
            var mode = map != null && !string.IsNullOrEmpty(map.mode) ? map.mode : state.selectedMode;
            var difficultyName = difficulty != null ? difficulty.label : state.difficulty;
            var hp = difficulty != null ? difficulty.bossHp : state.bossHp;

            return $"MAP  {ToUpperSafe(mapName)}  /  MODE  {ToUpperSafe(mode)}  /  BOSS  {ToUpperSafe(difficultyName)} - {FormatNumber(hp)} HP";
        }

        private static string GetModeDescription(string mode)
        {
            switch (ToUpperSafe(mode))
            {
                case "NM":
                    return "NO MOD";
                case "HD":
                    return "HIDDEN";
                case "HR":
                    return "HARD ROCK";
                case "DT":
                    return "DOUBLE TIME";
                default:
                    return "MODE";
            }
        }

        private static string ToUpperSafe(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : value.ToUpperInvariant();
        }

        private static string ExtractNameSuffix(string name, string prefix)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }

            return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? name.Substring(prefix.Length) : name;
        }

        private static void SetNamedObjectActive(Transform rootTransform, string name, bool active)
        {
            var target = FindDeep(rootTransform, name);
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }

        private static void SetNamedText(Transform rootTransform, string name, string value)
        {
            var target = FindDeep(rootTransform, name);
            var text = GetFirstText(target);
            if (text != null)
            {
                text.text = value ?? "";
            }
        }

        private static void SetNamedTextColor(Transform rootTransform, string name, Color color)
        {
            var target = FindDeep(rootTransform, name);
            var text = GetFirstText(target);
            if (text != null)
            {
                text.color = color;
            }
        }

        private static void SetHudStat(Transform rootTransform, string tileName, string label, string value, Color valueColor)
        {
            var tile = FindRect(rootTransform, tileName);
            if (tile == null)
            {
                return;
            }

            SetNamedText(tile, "Lbl", label);
            SetNamedText(tile, "Val", value);
            SetNamedTextColor(tile, "Val", valueColor);
        }

        private static void SetNamedRectCenter(Transform rootTransform, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var rect = FindRect(rootTransform, name);
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void SetNamedTextBox(Transform rootTransform, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, TextAnchor alignment, int fontSize)
        {
            var rect = FindRect(rootTransform, name);
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = GetFirstText(rect);
            if (text == null)
            {
                return;
            }

            text.alignment = alignment;
            text.fontSize = Mathf.Max(fontSize, MinimumReadablePrefabFontSize);
            text.resizeTextMaxSize = Mathf.Max(text.resizeTextMaxSize, text.fontSize);
            text.resizeTextMinSize = Mathf.Min(text.resizeTextMinSize, text.fontSize);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static void SetNamedTextContainedBox(Transform rootTransform, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, TextAnchor alignment, int maxFontSize, int minFontSize)
        {
            var rect = FindRect(rootTransform, name);
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = GetFirstText(rect);
            if (text == null)
            {
                return;
            }

            text.alignment = alignment;
            text.fontSize = Mathf.Max(maxFontSize, MinimumReadablePrefabFontSize);
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Clamp(minFontSize, 8, text.fontSize);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 0.9f;
        }

        private static Text EnsureText(Transform rootTransform, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, int fontSize, Color color, TextAnchor alignment, FontStyle style)
        {
            var rect = FindRect(rootTransform, name);
            Text text = null;
            if (rect == null)
            {
                rect = CreateRect(name, rootTransform, anchorMin, anchorMax, offsetMin, offsetMax);
                text = ConfigureText(rect, "", fontSize, color, alignment, style);
            }
            else
            {
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
                text = GetFirstText(rect);
                if (text == null)
                {
                    text = ConfigureText(rect, "", fontSize, color, alignment, style);
                }
            }

            text.fontSize = Mathf.Max(fontSize, MinimumReadablePrefabFontSize);
            text.resizeTextMaxSize = Mathf.Max(text.resizeTextMaxSize, text.fontSize);
            text.alignment = alignment;
            text.fontStyle = style;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void CenterNamedText(Transform rootTransform, string name, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var rect = FindRect(rootTransform, name);
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var text = GetFirstText(rect);
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        private static Text GetFirstText(Transform rootTransform)
        {
            if (rootTransform == null)
            {
                return null;
            }

            var text = rootTransform.GetComponent<Text>();
            return text != null ? text : rootTransform.GetComponentInChildren<Text>(true);
        }

        private static RectTransform FindRect(Transform rootTransform, string name)
        {
            var target = FindDeep(rootTransform, name);
            return target != null ? target.GetComponent<RectTransform>() : null;
        }

        private static Transform FindDeep(Transform rootTransform, string name)
        {
            if (rootTransform == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (string.Equals(rootTransform.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return rootTransform;
            }

            for (var i = 0; i < rootTransform.childCount; i++)
            {
                var found = FindDeep(rootTransform.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static List<RectTransform> FindRectsByPrefix(Transform rootTransform, string prefix)
        {
            var results = new List<RectTransform>();
            CollectRectsByPrefix(rootTransform, prefix, results);
            return results;
        }

        private static void CollectRectsByPrefix(Transform rootTransform, string prefix, List<RectTransform> results)
        {
            if (rootTransform == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(prefix) && rootTransform.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var rect = rootTransform.GetComponent<RectTransform>();
                if (rect != null)
                {
                    results.Add(rect);
                }
            }

            for (var i = 0; i < rootTransform.childCount; i++)
            {
                CollectRectsByPrefix(rootTransform.GetChild(i), prefix, results);
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
            AppendPlayerChatLines(lines);

            return lines.Count == 0 ? "Waiting for player chat..." : string.Join("\n", lines);
        }

        private void AppendPlayerChatLines(List<string> lines)
        {
            var candidates = new List<string>();
            for (var i = state.chatMessages.Count - 1; i >= 0 && candidates.Count < 5; i--)
            {
                var item = state.chatMessages[i];
                if (item == null)
                {
                    continue;
                }

                var kind = CleanChatText(item.kind);
                if (!string.Equals(kind, "chat", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var message = CleanChatText(item.message);
                if (string.IsNullOrEmpty(message))
                {
                    continue;
                }

                var sender = CleanChatText(item.sender);
                if (IsHiddenChatSender(sender) || IsHiddenChatMessage(message))
                {
                    continue;
                }

                var prefix = string.IsNullOrEmpty(sender) ? "" : $"{sender}: ";
                candidates.Add($"{prefix}{message}");
            }

            for (var i = candidates.Count - 1; i >= 0; i--)
            {
                lines.Add(candidates[i]);
            }
        }

        private List<RoomChatLine> BuildRoomChatLines(int maxLines)
        {
            var lines = new List<RoomChatLine>();
            if (state == null || state.chatMessages == null || maxLines <= 0)
            {
                return lines;
            }

            var candidates = new List<RoomChatLine>();
            for (var i = state.chatMessages.Count - 1; i >= 0 && candidates.Count < maxLines; i--)
            {
                var item = state.chatMessages[i];
                if (item == null)
                {
                    continue;
                }

                var kind = CleanChatText(item.kind);
                if (!string.Equals(kind, "chat", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(kind, "bancho", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var message = CleanChatText(item.message);
                if (string.IsNullOrEmpty(message))
                {
                    continue;
                }

                var sender = CleanChatText(item.sender);
                candidates.Add(new RoomChatLine
                {
                    sender = FormatRoomChatSender(sender, kind),
                    message = message,
                    color = string.Equals(kind, "bancho", StringComparison.OrdinalIgnoreCase) ? Gold : Magenta
                });
            }

            for (var i = candidates.Count - 1; i >= 0; i--)
            {
                lines.Add(candidates[i]);
            }

            return lines;
        }

        private static string FormatRoomChatSender(string sender, string kind)
        {
            if (string.Equals(kind, "bancho", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sender, "BanchoBot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sender, "Bancho Bot", StringComparison.OrdinalIgnoreCase))
            {
                return "BANCHO";
            }

            return string.IsNullOrEmpty(sender) ? "CHAT" : ToUpperSafe(sender);
        }

        private static bool IsHiddenChatSender(string sender)
        {
            return string.Equals(sender, "Bridge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sender, "BanchoBot", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHiddenChatMessage(string message)
        {
            return message.StartsWith("!mp ", StringComparison.OrdinalIgnoreCase);
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
            ApplyTextSupersampling(text);

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
