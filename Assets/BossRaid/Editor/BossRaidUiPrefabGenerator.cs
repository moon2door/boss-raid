using System.IO;
using BossRaid;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BossRaid.Editor
{
    public static class BossRaidUiPrefabGenerator
    {
        private const string UiFolder = "Assets/BossRaid/Resources/BossRaidUi";
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
        private static readonly Vector2 DesignResolution = new Vector2(1920f, 1080f);

        [MenuItem("Boss Raid/Generate UI Prefabs")]
        public static void GenerateDefaultPrefabs()
        {
            if (Directory.Exists(UiFolder))
            {
                FileUtil.DeleteFileOrDirectory(UiFolder);
                FileUtil.DeleteFileOrDirectory(UiFolder + ".meta");
            }

            Directory.CreateDirectory(UiFolder);

            SavePrefab(CreateStartScreen(), UiFolder + "/StartScreen.prefab");
            SavePrefab(CreateBurgerMapSelectScreen(), UiFolder + "/BurgerMapSelectScreen.prefab");
            SavePrefab(CreateDifficultySelectScreen(), UiFolder + "/DifficultySelectScreen.prefab");
            SavePrefab(CreateModeSelectScreen(), UiFolder + "/ModeSelectScreen.prefab");
            SavePrefab(CreateMapSelectScreen(), UiFolder + "/MapSelectScreen.prefab");
            SavePrefab(CreateMapReadyScreen(), UiFolder + "/MapReadyScreen.prefab");
            SavePrefab(CreateInGameScreen(), UiFolder + "/InGameScreen.prefab");
            SavePrefab(CreateResultScreen(true), UiFolder + "/SuccessResultScreen.prefab");
            SavePrefab(CreateResultScreen(false), UiFolder + "/FailResultScreen.prefab");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Boss Raid full-screen UI prefabs generated in " + UiFolder);
        }

        private static GameObject CreateStartScreen()
        {
            var root = ScreenRoot("StartScreen", "STANDBY");
            var content = Content(root.transform);
            var hero = PanelRect("StandbyHero", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.045f, 0.05f, 0.07f, 0.92f));
            Text(hero, "Title", "BOSS RAID", "EventTitle", 88, BossRaidUiColorRole.White, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.53f), new Vector2(1f, 0.70f), new Vector2(80f, 0f), new Vector2(-80f, 0f));
            Text(hero, "Subtitle", "Co-op boss raid overlay ready", "StandbySubtitle", 30, BossRaidUiColorRole.Muted, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.44f), new Vector2(1f, 0.52f), new Vector2(80f, 0f), new Vector2(-80f, 0f));

            var teamRow = Rect("TeamRow", hero, new Vector2(0f, 0f), new Vector2(1f, 0.38f), new Vector2(56f, 48f), new Vector2(-56f, -24f));
            Horizontal(teamRow.gameObject, 18f, true);
            TeamCard(teamRow, 0, "삼랄부마스터", "0", Red);
            TeamCard(teamRow, 1, "Team B", "0", new Color(0.2f, 0.55f, 0.95f, 1f));
            TeamCard(teamRow, 2, "Team C", "0", Green);
            TeamCard(teamRow, 3, "Team D", "0", Gold);
            return root;
        }

        private static GameObject CreateBurgerMapSelectScreen()
        {
            var root = ScreenRoot("BurgerMapSelectScreen", "BURGER REVEAL");
            var content = Content(root.transform);
            Text(content, "Title", "BURGER MAPS LOCKED", "", 44, BossRaidUiColorRole.Gold, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.90f), Vector2.one, Vector2.zero, new Vector2(0f, -18f));
            Text(content, "Subtitle", "Eight maps carry viewer burger stakes", "", 20, BossRaidUiColorRole.Muted, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.84f), new Vector2(1f, 0.90f), Vector2.zero, new Vector2(0f, -4f));

            var gridRoot = Rect("BurgerGridRoot", content, new Vector2(0f, 0f), new Vector2(1f, 0.80f), new Vector2(32f, 28f), new Vector2(-32f, -10f));
            Vertical(gridRoot.gameObject, 12f, true);
            var modes = new[] { "NM", "HD", "HR", "DT" };
            for (var row = 0; row < modes.Length; row++)
            {
                var rowPanel = PanelRect("BurgerRow_" + modes[row], gridRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.028f, 0.033f, 0.046f, 0.54f));
                rowPanel.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                Horizontal(rowPanel.gameObject, 10f, true);
                for (var col = 0; col < 6; col++)
                {
                    var index = row * 6 + col;
                    MapCard(rowPanel, index, modes[row] + (col + 1), modes[row] + " / " + modes[row] + (col + 1), BossRaidUiBindingSource.BurgerMap, 20, 15, true);
                }
            }

            return root;
        }

        private static GameObject CreateDifficultySelectScreen()
        {
            var root = ScreenRoot("DifficultySelectScreen", "DIFFICULTY");
            var content = Content(root.transform);
            PanelRect("DifficultySelect", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.045f, 0.05f, 0.07f, 0.94f));
            Text(content, "Title", "SELECT DIFFICULTY", "", 52, BossRaidUiColorRole.Gold, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.88f), Vector2.one, new Vector2(80f, 0f), new Vector2(-80f, -18f));
            Text(content, "Lead", "Choose boss HP before the map roulette", "", 30, BossRaidUiColorRole.White, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.78f), new Vector2(1f, 0.88f), new Vector2(80f, 0f), new Vector2(-80f, 0f));
            Text(content, "Meta", "Confirm to enter Mode Roulette", "", 22, BossRaidUiColorRole.Muted, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.72f), new Vector2(1f, 0.78f), new Vector2(120f, 0f), new Vector2(-120f, 0f));

            var cards = Rect("DifficultyCards", content, new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.68f), Vector2.zero, Vector2.zero);
            Horizontal(cards.gameObject, 20f, true);
            DifficultyCard(cards, 0, "Easy", "HP 1,000,000", "+3,000 KRW", Green);
            DifficultyCard(cards, 1, "Normal", "HP 1,400,000", "+5,000 KRW", Gold);
            DifficultyCard(cards, 2, "Hard", "HP 2,000,000", "+15,000 KRW", Red);
            Text(content, "Current", "Normal / HP 1,400,000 / 5,000 KRW", "CurrentDifficultySummary", 30, BossRaidUiColorRole.Difficulty, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.08f), new Vector2(1f, 0.18f), new Vector2(80f, 0f), new Vector2(-80f, 0f));
            return root;
        }

        private static GameObject CreateModeSelectScreen()
        {
            var root = ScreenRoot("ModeSelectScreen", "MODE ROULETTE");
            var content = Content(root.transform);
            Text(content, "Title", "MODE ROULETTE", "", 50, BossRaidUiColorRole.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.84f), Vector2.one, Vector2.zero, new Vector2(0f, -36f));
            var area = Rect("ModeArea", content, new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.76f), Vector2.zero, Vector2.zero);
            Horizontal(area.gameObject, 18f, true);
            var modes = new[] { "NM", "HD", "HR", "DT" };
            for (var i = 0; i < modes.Length; i++)
            {
                ModeCard(area, i, modes[i]);
            }

            return root;
        }

        private static GameObject CreateMapSelectScreen()
        {
            var root = ScreenRoot("MapSelectScreen", "MAP ROULETTE");
            var content = Content(root.transform);
            Text(content, "Title", "NM MAP ROULETTE", "MapRouletteTitle", 50, BossRaidUiColorRole.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.84f), Vector2.one, Vector2.zero, new Vector2(0f, -36f));
            var gridRoot = Rect("MapRouletteGrid", content, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.76f), Vector2.zero, Vector2.zero);
            Grid(gridRoot.gameObject, 3, new Vector2(420f, 180f), new Vector2(14f, 14f));
            for (var i = 0; i < 6; i++)
            {
                MapCard(gridRoot, i, "NM" + (i + 1), "Even now, I'm searching for you.", BossRaidUiBindingSource.SelectedModeMap, 28, 21, false);
            }

            return root;
        }

        private static GameObject CreateMapReadyScreen()
        {
            var root = ScreenRoot("MapReadyScreen", "MAP READY");
            var content = Content(root.transform);
            PanelRect("ReadyStage", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Panel);
            SpectatorSlots(content);
            var chat = PanelRect("ReadyChat", content, Vector2.zero, new Vector2(0.30f, 0.22f), Vector2.zero, new Vector2(-14f, 0f), PanelAlt);
            Text(chat, "ChatTitle", "INGAME CHAT", "", 18, BossRaidUiColorRole.Muted, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.68f), Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, -8f));
            Text(chat, "ChatBody", "Waiting for room chat...", "IngameChat", 18, BossRaidUiColorRole.White, TextAnchor.MiddleLeft, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.66f), new Vector2(18f, 12f), Vector2.zero);
            var info = PanelRect("ReadyMapInfo", content, new Vector2(0.64f, 0f), new Vector2(1f, 0.28f), new Vector2(14f, 0f), Vector2.zero, PanelAlt);
            MapInfo(info, "MAP READY");
            return root;
        }

        private static GameObject CreateInGameScreen()
        {
            var root = ScreenRoot("InGameScreen", "RAID LIVE");
            var content = Content(root.transform);
            PanelRect("RaidStage", content, new Vector2(0f, 0.30f), Vector2.one, Vector2.zero, Vector2.zero, Panel);
            SpectatorSlots(content);
            var bottom = PanelRect("BossBarArea", content, Vector2.zero, new Vector2(1f, 0.26f), Vector2.zero, Vector2.zero, PanelAlt);
            BossBar(bottom, new Vector2(36f, 64f), new Vector2(-36f, -42f), false);
            return root;
        }

        private static GameObject CreateResultScreen(bool isClear)
        {
            var root = ScreenRoot(isClear ? "SuccessResultScreen" : "FailResultScreen", isClear ? "RESULT: CLEAR" : "RESULT: FAILED");
            var content = Content(root.transform);
            PanelRect("ResultPanel", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.035f, 0.04f, 0.055f, 0.96f));
            Text(content, "ResultText", isClear ? "CLEAR" : "FAILED", "ResultText", 108, BossRaidUiColorRole.Result, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.55f), new Vector2(1f, 0.76f), new Vector2(80f, 0f), new Vector2(-80f, 0f));
            Text(content, "ResultMessage", isClear ? "Boss defeated. Prize pool increased." : "Boss survived. Failure count increased.", "ResultMessage", 32, BossRaidUiColorRole.White, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.45f), new Vector2(1f, 0.54f), new Vector2(80f, 0f), new Vector2(-80f, 0f));
            var barRoot = PanelRect("ResultBossBar", content, new Vector2(0.12f, 0.25f), new Vector2(0.88f, 0.38f), Vector2.zero, Vector2.zero, PanelAlt);
            BossBar(barRoot, new Vector2(24f, 34f), new Vector2(-24f, -30f), true);
            var stats = Rect("ResultStats", content, new Vector2(0.12f, 0.07f), new Vector2(0.88f, 0.20f), Vector2.zero, Vector2.zero);
            Horizontal(stats.gameObject, 0f, true);
            ResultStat(stats, 0, "Prize Pool", "0", Gold);
            ResultStat(stats, 1, "Burgers", "0", Gold);
            ResultStat(stats, 2, "Burger Miss", "0", Red);
            ResultStat(stats, 3, "Record", isClear ? "1C 0F" : "0C 1F", isClear ? Green : Red);
            return root;
        }

        private static GameObject ScreenRoot(string name, string screenLabel)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = DesignResolution;
            root.AddComponent<Image>().color = Background;
            Accent(root.transform, 0.06f, Red);
            Accent(root.transform, 0.38f, Cyan);
            Accent(root.transform, 0.74f, Gold);
            Header(root.transform, screenLabel);
            return root;
        }

        private static RectTransform Content(Transform parent)
        {
            return Rect("Content", parent, Vector2.zero, Vector2.one, new Vector2(28f, 28f), new Vector2(-28f, -164f));
        }

        private static void Header(Transform root, string screenLabel)
        {
            var header = Rect("Header", root, new Vector2(0f, 1f), Vector2.one, new Vector2(28f, -146f), new Vector2(-28f, -24f));
            var left = PanelRect("HeaderLeft", header, new Vector2(0f, 0f), new Vector2(0.35f, 1f), Vector2.zero, new Vector2(-12f, 0f), Panel);
            Text(left, "EventTitle", "BOSS RAID", "EventTitle", 34, BossRaidUiColorRole.White, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.50f), Vector2.one, new Vector2(24f, 2f), new Vector2(-18f, -14f));
            Text(left, "Team", "삼랄부마스터", "CurrentTeamName", 24, BossRaidUiColorRole.CurrentTeam, TextAnchor.MiddleLeft, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.48f), new Vector2(24f, 12f), new Vector2(-18f, -4f));
            var center = PanelRect("HeaderCenter", header, new Vector2(0.35f, 0f), new Vector2(0.65f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f), PanelAlt);
            Text(center, "Map", "Waiting for map", "HeaderMapTitle", 17, BossRaidUiColorRole.Muted, TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(0f, 0.68f), Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, -12f));
            Text(center, "Screen", screenLabel, "ScreenLabel", 34, BossRaidUiColorRole.Screen, TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.64f), new Vector2(16f, 8f), new Vector2(-16f, -4f));
            var right = PanelRect("HeaderRight", header, new Vector2(0.65f, 0f), Vector2.one, new Vector2(12f, 0f), Vector2.zero, Panel);
            HeaderStat(right, "Prize", "0", "PrizePool", Gold, 0);
            HeaderStat(right, "Burger", "0", "BurgerCount", Gold, 1);
            HeaderStat(right, "Round", "1 / 8", "Round", Cyan, 2);
            HeaderStat(right, "Record", "0C 0F", "Record", Green, 3);
            Text(header, "Connection", "CONNECTED", "ConnectionStatus", 15, BossRaidUiColorRole.Muted, TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(0.65f, 0f), Vector2.one, new Vector2(12f, 2f), new Vector2(-18f, -104f));
        }

        private static void HeaderStat(Transform parent, string label, string display, string key, Color color, int index)
        {
            var width = 0.25f;
            var tile = Rect("Stat_" + label, parent, new Vector2(width * index, 0f), new Vector2(width * (index + 1), 1f), new Vector2(8f, 26f), new Vector2(-8f, -14f));
            tile.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.036f, 0.052f, 0.92f);
            Outline(tile.gameObject, Line, 1f);
            Text(tile, "Label", label, "", 13, BossRaidUiColorRole.Muted, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.62f), Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, -8f));
            Text(tile, "Value", display, key, 23, ColorRoleFor(color), TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.58f), new Vector2(4f, 6f), new Vector2(-4f, -2f));
        }

        private static void TeamCard(Transform parent, int index, string name, string score, Color outlineColor)
        {
            var card = PanelRect("TeamCard_" + (index + 1), parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelAlt);
            card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Outline(card.gameObject, index == 0 ? outlineColor : Line, index == 0 ? 3f : 1f);
            Bind(card.gameObject, "ItemVisual", BossRaidUiBindingSource.Team, index, BossRaidUiColorRole.None, false);
            Text(card, "TeamName", name, "TeamName", 30, BossRaidUiColorRole.Team, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.54f), Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, -20f), BossRaidUiBindingSource.Team, index);
            Text(card, "Score", score, "TeamScore", 40, BossRaidUiColorRole.White, TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.52f), new Vector2(18f, 20f), new Vector2(-18f, -6f), BossRaidUiBindingSource.Team, index);
        }

        private static void ModeCard(Transform parent, int index, string mode)
        {
            var card = PanelRect("Mode_" + mode, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Panel);
            card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Outline(card.gameObject, Line, 1f);
            Bind(card.gameObject, "ItemVisual", BossRaidUiBindingSource.Mode, index, BossRaidUiColorRole.None, false);
            Text(card, "ModeName", mode, "ModeName", 44, BossRaidUiColorRole.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.26f), new Vector2(1f, 0.78f), new Vector2(16f, 0f), new Vector2(-16f, 0f), BossRaidUiBindingSource.Mode, index);
            Text(card, "ModeCount", "6 maps", "ModeCount", 20, BossRaidUiColorRole.Muted, TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.20f), new Vector2(16f, 8f), new Vector2(-16f, 0f), BossRaidUiBindingSource.Mode, index);
        }

        private static void DifficultyCard(Transform parent, int index, string label, string hp, string prize, Color color)
        {
            var card = PanelRect("Difficulty_" + label, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelAlt);
            card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Outline(card.gameObject, label == "Normal" ? color : Line, label == "Normal" ? 4f : 1.2f);
            Bind(card.gameObject, "ItemVisual", BossRaidUiBindingSource.Difficulty, index, BossRaidUiColorRole.None, false);
            Text(card, "Selected", label == "Normal" ? "CURRENT PICK" : "", "DifficultySelected", 20, BossRaidUiColorRole.Difficulty, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.82f), Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, -8f), BossRaidUiBindingSource.Difficulty, index);
            Text(card, "Label", label, "DifficultyLabel", 48, BossRaidUiColorRole.Difficulty, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.54f), new Vector2(1f, 0.82f), new Vector2(20f, 0f), new Vector2(-20f, 0f), BossRaidUiBindingSource.Difficulty, index);
            Text(card, "Hp", hp, "DifficultyHp", 30, BossRaidUiColorRole.White, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.34f), new Vector2(1f, 0.50f), new Vector2(20f, 0f), new Vector2(-20f, 0f), BossRaidUiBindingSource.Difficulty, index);
            Text(card, "Prize", prize, "DifficultyPrize", 26, BossRaidUiColorRole.Gold, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.18f), new Vector2(1f, 0.32f), new Vector2(20f, 0f), new Vector2(-20f, 0f), BossRaidUiBindingSource.Difficulty, index);
        }

        private static void MapCard(Transform parent, int index, string title, string subtitle, BossRaidUiBindingSource source, int titleSize, int subtitleSize, bool flexible)
        {
            var card = PanelRect("MapCard_" + index, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelAlt);
            if (flexible)
            {
                card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            }
            Outline(card.gameObject, Line, 1.5f);
            Bind(card.gameObject, "ItemVisual", source, index, BossRaidUiColorRole.None, false);
            Text(card, "MapTitle", title, "MapTitle", titleSize, BossRaidUiColorRole.White, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.44f), Vector2.one, new Vector2(16f, 0f), new Vector2(-56f, -8f), source, index);
            Text(card, "MapSubtitle", subtitle, "MapSubtitle", subtitleSize, BossRaidUiColorRole.Cyan, TextAnchor.MiddleLeft, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.34f), new Vector2(16f, 8f), new Vector2(-16f, -2f), source, index);
            Text(card, "Burger", "", "MapBurgerTag", 14, BossRaidUiColorRole.Gold, TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(0.58f, 0.38f), new Vector2(1f, 0.72f), Vector2.zero, new Vector2(-16f, 0f), source, index).GetComponent<BossRaidUiBinding>().hideWhenEmpty = true;
        }

        private static void SpectatorSlots(Transform content)
        {
            var slots = Rect("SpectatorSlots", content, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(-740f, -178f), new Vector2(740f, 178f));
            Horizontal(slots.gameObject, 20f, false);
            for (var i = 0; i < 3; i++)
            {
                var slot = PanelRect("SpectatorSlot_" + (i + 1), slots, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.012f, 0.015f, 0.022f, 0.78f));
                var layout = slot.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = 480f;
                layout.preferredHeight = 356f;
                Outline(slot.gameObject, Line, 1.5f);
                Bind(slot.gameObject, "ItemVisual", BossRaidUiBindingSource.Spectator, i, BossRaidUiColorRole.None, false);
                Text(slot, "Label", "SPECTATOR " + (i + 1), "SpectatorLabel", 18, BossRaidUiColorRole.Muted, TextAnchor.UpperLeft, FontStyle.Bold, new Vector2(0f, 0.80f), Vector2.one, new Vector2(18f, -14f), new Vector2(-18f, -12f), BossRaidUiBindingSource.Spectator, i);
                Text(slot, "Guide", "osu! tourney", "", 22, BossRaidUiColorRole.Muted, TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, 0f), BossRaidUiBindingSource.Spectator, i);
            }
        }

        private static void MapInfo(Transform parent, string label)
        {
            Text(parent, "Label", label, "", 20, BossRaidUiColorRole.Muted, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.82f), Vector2.one, new Vector2(22f, 0f), new Vector2(-22f, -8f));
            Text(parent, "Title", "No map selected", "SelectedMapTitle", 34, BossRaidUiColorRole.White, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.64f), new Vector2(1f, 0.84f), new Vector2(22f, 0f), new Vector2(-22f, -2f));
            Text(parent, "DifficultyName", "Difficulty pending", "SelectedMapDifficultyName", 20, BossRaidUiColorRole.Cyan, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.51f), new Vector2(1f, 0.64f), new Vector2(22f, 0f), Vector2.zero);
            Text(parent, "Artist", "Artist pending", "SelectedMapArtist", 18, BossRaidUiColorRole.Muted, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.39f), new Vector2(0.62f, 0.51f), new Vector2(22f, 0f), new Vector2(-8f, 0f));
            Text(parent, "Mapper", "Mapper pending", "SelectedMapMapper", 18, BossRaidUiColorRole.Muted, TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(0.62f, 0.39f), new Vector2(1f, 0.51f), new Vector2(8f, 0f), new Vector2(-22f, 0f));
            Text(parent, "Mode", "Mode pending", "SelectedMapMode", 22, BossRaidUiColorRole.Cyan, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.22f), new Vector2(0.48f, 0.36f), new Vector2(22f, 0f), new Vector2(-8f, 0f));
            Text(parent, "Difficulty", "Normal", "CurrentDifficultyLabel", 22, BossRaidUiColorRole.Difficulty, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.48f, 0.22f), new Vector2(0.72f, 0.36f), Vector2.zero, Vector2.zero);
            Text(parent, "Hp", "HP 1,400,000", "CurrentDifficultyHp", 22, BossRaidUiColorRole.Gold, TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(0.72f, 0.22f), new Vector2(1f, 0.36f), new Vector2(8f, 0f), new Vector2(-22f, 0f));
            Text(parent, "Burger", "", "BurgerMarker", 18, BossRaidUiColorRole.Gold, TextAnchor.MiddleLeft, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.18f), new Vector2(22f, 8f), new Vector2(-22f, 0f)).GetComponent<BossRaidUiBinding>().hideWhenEmpty = true;
        }

        private static void BossBar(Transform parent, Vector2 offsetMin, Vector2 offsetMax, bool resultMode)
        {
            if (!resultMode)
            {
                Text(parent, "BossLabel", "Boss HP", "", 24, BossRaidUiColorRole.Muted, TextAnchor.MiddleLeft, FontStyle.Bold, new Vector2(0f, 0.76f), Vector2.one, new Vector2(offsetMin.x, 0f), new Vector2(offsetMax.x, -8f));
                Text(parent, "BossDifficulty", "Normal", "BossDifficultyLabel", 24, BossRaidUiColorRole.Difficulty, TextAnchor.MiddleRight, FontStyle.Bold, new Vector2(0.52f, 0.76f), Vector2.one, Vector2.zero, new Vector2(offsetMax.x, -8f));
                offsetMax = new Vector2(offsetMax.x, Mathf.Min(offsetMax.y, -74f));
            }

            var bar = PanelRect("BossBar", parent, Vector2.zero, Vector2.one, offsetMin, offsetMax, new Color(0.22f, 0.035f, 0.045f, 1f));
            Outline(bar.gameObject, Line, 2f);
            var fill = Rect("DamageFill", bar, Vector2.zero, new Vector2(0.72f, 1f), Vector2.zero, Vector2.zero);
            fill.gameObject.AddComponent<Image>().color = Cyan;
            Bind(fill.gameObject, "BossDamageFill", BossRaidUiBindingSource.None, -1, BossRaidUiColorRole.None, false);
            Text(bar, "DamageText", "0 / 1,400,000", "BossDamageText", 36, BossRaidUiColorRole.White, TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
        }

        private static void ResultStat(Transform parent, int index, string label, string value, Color color)
        {
            var width = 0.25f;
            var tile = PanelRect("ResultStat_" + index, parent, new Vector2(width * index, 0f), new Vector2(width * (index + 1), 1f), new Vector2(8f, 0f), new Vector2(-8f, 0f), PanelAlt);
            Outline(tile.gameObject, Line, 1f);
            Bind(tile.gameObject, "ItemVisual", BossRaidUiBindingSource.ResultStat, index, BossRaidUiColorRole.None, false);
            Text(tile, "Label", label, "StatLabel", 17, BossRaidUiColorRole.Muted, TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0f, 0.60f), Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, -6f), BossRaidUiBindingSource.ResultStat, index);
            Text(tile, "Value", value, "StatValue", 30, BossRaidUiColorRole.Context, TextAnchor.MiddleCenter, FontStyle.Bold, Vector2.zero, new Vector2(1f, 0.56f), new Vector2(8f, 8f), new Vector2(-8f, -2f), BossRaidUiBindingSource.ResultStat, index);
        }

        private static RectTransform PanelRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rect = Rect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
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

        private static Text Text(Transform parent, string name, string display, string key, int size, BossRaidUiColorRole role, TextAnchor anchor, FontStyle style, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, BossRaidUiBindingSource source = BossRaidUiBindingSource.None, int index = -1)
        {
            var rect = Rect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = display;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = ResolveEditorColor(role);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, Mathf.FloorToInt(size * 0.55f));
            text.resizeTextMaxSize = size;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(2f, -2f);
            Bind(rect.gameObject, key, source, index, role, false);
            return text;
        }

        private static void Bind(GameObject go, string key, BossRaidUiBindingSource source, int index, BossRaidUiColorRole role, bool hideWhenEmpty)
        {
            var binding = go.AddComponent<BossRaidUiBinding>();
            binding.key = key;
            binding.source = source;
            binding.index = index;
            binding.colorRole = role;
            binding.hideWhenEmpty = hideWhenEmpty;
        }

        private static void Accent(Transform parent, float x, Color color)
        {
            var band = Rect("AccentBand", parent, new Vector2(x, 0f), new Vector2(x, 1f), new Vector2(-2f, 0f), new Vector2(2f, 0f));
            band.gameObject.AddComponent<Image>().color = new Color(color.r, color.g, color.b, 0.22f);
        }

        private static void Horizontal(GameObject go, float spacing, bool expand)
        {
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childForceExpandWidth = expand;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
        }

        private static void Vertical(GameObject go, float spacing, bool expand)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = expand;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
        }

        private static void Grid(GameObject go, int columns, Vector2 cellSize, Vector2 spacing)
        {
            var grid = go.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.MiddleCenter;
        }

        private static void Outline(GameObject go, Color color, float distance)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private static BossRaidUiColorRole ColorRoleFor(Color color)
        {
            if (color == Red)
            {
                return BossRaidUiColorRole.Red;
            }

            if (color == Green)
            {
                return BossRaidUiColorRole.Green;
            }

            if (color == Cyan)
            {
                return BossRaidUiColorRole.Cyan;
            }

            if (color == Gold)
            {
                return BossRaidUiColorRole.Gold;
            }

            return BossRaidUiColorRole.White;
        }

        private static Color ResolveEditorColor(BossRaidUiColorRole role)
        {
            switch (role)
            {
                case BossRaidUiColorRole.Muted:
                    return Muted;
                case BossRaidUiColorRole.Red:
                case BossRaidUiColorRole.Result:
                    return Red;
                case BossRaidUiColorRole.Green:
                    return Green;
                case BossRaidUiColorRole.Cyan:
                    return Cyan;
                case BossRaidUiColorRole.Gold:
                case BossRaidUiColorRole.Difficulty:
                    return Gold;
                case BossRaidUiColorRole.CurrentTeam:
                case BossRaidUiColorRole.Team:
                    return Red;
                default:
                    return White;
            }
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
