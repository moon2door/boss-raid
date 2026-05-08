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

        private static readonly Vector2 DesignResolution = new Vector2(1920f, 1080f);
        private static readonly Color Background = C(0x05, 0x00, 0x18);
        private static readonly Color Deep = C(0x0d, 0x03, 0x22, 0.96f);
        private static readonly Color Mid = C(0x1a, 0x07, 0x38, 0.94f);
        private static readonly Color Panel = C(0x24, 0x0a, 0x44, 0.92f);
        private static readonly Color Card = C(0x2a, 0x0e, 0x4a, 0.94f);
        private static readonly Color Line = C(0x6f, 0x3a, 0xa8, 0.72f);
        private static readonly Color GridLine = C(0x5e, 0x2d, 0x8f, 0.46f);
        private static readonly Color White = C(0xf8, 0xee, 0xf8);
        private static readonly Color Soft = C(0xd3, 0xbc, 0xe8);
        private static readonly Color Muted = C(0x8a, 0x6f, 0xc4);
        private static readonly Color Faint = C(0x5e, 0x4a, 0x82);
        private static readonly Color Magenta = C(0xff, 0x2b, 0xd6);
        private static readonly Color MagentaBright = C(0xff, 0x6e, 0xe0);
        private static readonly Color Cyan = C(0x00, 0xf0, 0xff);
        private static readonly Color CyanBright = C(0x80, 0xfb, 0xff);
        private static readonly Color Yellow = C(0xff, 0xe6, 0x00);
        private static readonly Color YellowBright = C(0xff, 0xf4, 0x5a);
        private static readonly Color Orange = C(0xff, 0x8a, 0x3c);
        private static readonly Color Green = C(0x00, 0xff, 0x88);
        private static readonly Color Red = C(0xff, 0x30, 0x60);

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
            Debug.Log("Boss Raid v2 UI prefabs generated in " + UiFolder);
        }

        private static GameObject CreateStartScreen()
        {
            var root = ScreenRoot("StartScreen", false, true);

            var insertCoin = TextAbs(root.transform, "InsertCoin", "INSERT COIN TO BEGIN", "", 18, CyanBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 170f, 1920f, 42f);
            Blink(insertCoin, 0.9f, 1f, 0.25f);
            TextAbs(root.transform, "TitleShadow", "BOSS RAID", "", 150, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold, 82f, 288f, 1760f, 172f, BossRaidUiColorRole.None, BossRaidUiBindingSource.None, -1, false, true);
            var startTitle = TextAbs(root.transform, "Title", "BOSS RAID", "EventTitle", 150, YellowBright, TextAnchor.MiddleCenter, FontStyle.Bold, 80f, 278f, 1760f, 172f, BossRaidUiColorRole.Gold, BossRaidUiBindingSource.None, -1, false, true);
            ApplyChromaticGlow(startTitle, 3f);
            TextAbs(root.transform, "Subtitle", "CO-OP RAID OVERLAY_", "StandbySubtitle", 38, CyanBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 482f, 1920f, 58f);

            var version = RectAbs("VersionBar", root.transform, 450f, 570f, 1020f, 44f);
            var tags = new[] { "SYSTEM ONLINE", "v0.9.4", "4 TEAMS LOADED", "24 MAPS READY" };
            for (var i = 0; i < tags.Length; i++)
            {
                var tile = PanelAbs("Version_" + i, version, i * 255f, 0f, 238f, 40f, C(0x0d, 0x03, 0x22, 0.72f), i == 0 ? Green : GridLine, 1.5f);
                TextAbs(tile, "Text", tags[i], i == 0 ? "ConnectionStatus" : "", 16, i == 0 ? Green : Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 0f, 238f, 40f);
            }

            var teamArea = RectAbs("TeamCards", root.transform, 140f, 710f, 1640f, 200f);
            var cardWidth = (1640f - 88f) / 4f;
            for (var i = 0; i < 4; i++)
            {
                TeamCard(teamArea, i, 22f + i * (cardWidth + 22f), 0f, cardWidth, 200f);
            }

            var startFooter = TextAbs(root.transform, "Footer", "PRESS ANY KEY TO START", "", 16, YellowBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 1000f, 1920f, 44f);
            Blink(startFooter, 0.9f, 1f, 0.3f);
            return root;
        }

        private static GameObject CreateBurgerMapSelectScreen()
        {
            var root = ScreenRoot("BurgerMapSelectScreen", false, false);

            TextAbs(root.transform, "Small", "BONUS LOTTERY", "", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 50f, 1920f, 28f);
            var burgerTitle = TextAbs(root.transform, "Title", "BURGER LOCKDOWN", "", 70, YellowBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 84f, 1920f, 86f, BossRaidUiColorRole.Gold, BossRaidUiBindingSource.None, -1, false, true);
            ApplyChromaticGlow(burgerTitle, 2f);
            TextAbs(root.transform, "Subtitle", "EIGHT MAPS CARRY VIEWER BURGER STAKES", "", 30, CyanBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 170f, 1920f, 40f);

            var frame = PanelAbs("ReelFrame", root.transform, 60f, 230f, 1800f, 720f, C(0x0d, 0x03, 0x22, 0.78f), Magenta, 3f);
            TextAbs(frame, "FrameTag", "BONUS LOTTERY", "", 14, MagentaBright, TextAnchor.MiddleLeft, FontStyle.Bold, 30f, -20f, 320f, 30f);
            TextAbs(frame, "Picked", "8 / 24 PICKED", "", 14, YellowBright, TextAnchor.MiddleRight, FontStyle.Bold, 1430f, -20f, 330f, 30f);

            var modes = new[] { "NM", "HD", "HR", "DT" };
            var rowHeight = (720f - 52f - 54f) / 4f;
            for (var row = 0; row < modes.Length; row++)
            {
                var y = 26f + row * (rowHeight + 18f);
                var modeTag = PanelAbs("ModeTag_" + modes[row], frame, 26f, y, 88f, rowHeight, C(0x00, 0xf0, 0xff, 0.16f), Cyan, 2f);
                TextAbs(modeTag, "Text", modes[row], "", 34, CyanBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 0f, 88f, rowHeight);

                var cardWidth = (1800f - 52f - 88f - 84f) / 6f;
                for (var col = 0; col < 6; col++)
                {
                    var index = row * 6 + col;
                    BurgerMapCard(frame, index, 128f + col * (cardWidth + 14f), y, cardWidth, rowHeight);
                }
            }

            TextAbs(root.transform, "Footer", "PRESS SPACE TO REROLL  /  8 OF 24 LOCKED", "", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 990f, 1920f, 42f);
            return root;
        }

        private static GameObject CreateDifficultySelectScreen()
        {
            var root = ScreenRoot("DifficultySelectScreen", false, true);

            TextAbs(root.transform, "Small", "STAGE SELECT", "", 16, MagentaBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 80f, 1920f, 28f);
            var diffTitle = TextAbs(root.transform, "Title", "SELECT STAGE", "", 94, YellowBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 116f, 1920f, 120f, BossRaidUiColorRole.Gold, BossRaidUiBindingSource.None, -1, false, true);
            ApplyChromaticGlow(diffTitle, 2.5f);
            TextAbs(root.transform, "Desc", "CHOOSE THE BOSS DIFFICULTY", "", 32, Soft, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 252f, 1920f, 44f);

            var cardWidth = (1760f - 72f) / 3f;
            for (var i = 0; i < 3; i++)
            {
                DifficultyCard(root.transform, i, 80f + i * (cardWidth + 36f), 360f, cardWidth, 540f);
            }

            TextAbs(root.transform, "Footer", "1 / 2 / 3 SELECT    ENTER CONFIRM", "", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 988f, 1920f, 42f);
            return root;
        }

        private static GameObject CreateModeSelectScreen()
        {
            var root = ScreenRoot("ModeSelectScreen", false, false);

            TextAbs(root.transform, "Small", "TEAM READY  /  BOSS STAGE  /  MODE LOTTERY", "", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 80f, 1920f, 28f);
            var modeTitle = TextAbs(root.transform, "Title", "MODE LOTTERY", "", 94, YellowBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 116f, 1920f, 120f, BossRaidUiColorRole.Gold, BossRaidUiBindingSource.None, -1, false, true);
            ApplyChromaticGlow(modeTitle, 2.5f);

            TextAbs(root.transform, "PointerTop", "▼", "", 42, YellowBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 278f, 1920f, 38f);
            var frame = PanelAbs("ModeReelFrame", root.transform, 80f, 320f, 1760f, 540f, C(0x0d, 0x03, 0x22, 0.72f), Cyan, 4f);
            TextAbs(frame, "FrameTag", "MODE LOTTERY", "", 15, CyanBright, TextAnchor.MiddleLeft, FontStyle.Bold, 30f, -22f, 360f, 34f);

            var modes = new[] { "NM", "HD", "HR", "DT" };
            var tileWidth = (1760f - 60f - 72f) / 4f;
            for (var i = 0; i < modes.Length; i++)
            {
                ModeCard(frame, i, modes[i], 30f + i * (tileWidth + 24f), 30f, tileWidth, 480f);
            }

            TextAbs(root.transform, "PointerBottom", "▲", "", 42, YellowBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 835f, 1920f, 38f);
            TextAbs(root.transform, "Footer", "PRESS SPACE TO SPIN THE WHEEL", "", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 978f, 1920f, 42f);
            return root;
        }

        private static GameObject CreateMapSelectScreen()
        {
            var root = ScreenRoot("MapSelectScreen", false, false);

            TextAbs(root.transform, "Small", "STAGE LOCKED  /  MODE LOCKED", "", 15, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 60f, 1920f, 28f);
            var mapTitle = TextAbs(root.transform, "Title", "MAP LOTTERY", "MapRouletteTitle", 76, YellowBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 94f, 1920f, 92f, BossRaidUiColorRole.Gold, BossRaidUiBindingSource.None, -1, false, true);
            ApplyChromaticGlow(mapTitle, 2f);

            var grid = RectAbs("MapGrid", root.transform, 60f, 240f, 1800f, 710f);
            var cellWidth = (1800f - 52f) / 3f;
            var cellHeight = (710f - 26f) / 2f;
            for (var i = 0; i < 6; i++)
            {
                var col = i % 3;
                var row = i / 3;
                MapRouletteCard(grid, i, col * (cellWidth + 26f), row * (cellHeight + 26f), cellWidth, cellHeight);
            }

            TextAbs(root.transform, "Footer", "PRESS SPACE TO DRAW THE MAP", "", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 988f, 1920f, 42f);
            return root;
        }

        private static GameObject CreateMapReadyScreen()
        {
            var root = ScreenRoot("MapReadyScreen", true, false, "MAP READY");

            MapBanner(root.transform, 60f, 200f, 1800f, 220f);
            SpectatorSlots(root.transform, 60f, 460f, 1400f, 380f, false);
            ReadySidePanel(root.transform);
            return root;
        }

        private static GameObject CreateInGameScreen()
        {
            var root = ScreenRoot("InGameScreen", true, false, "RAID LIVE");

            SpectatorSlots(root.transform, 60f, 200f, 1800f, 600f, true);
            var boss = PanelAbs("BossBarArea", root.transform, 60f, 830f, 1800f, 220f, C(0x24, 0x0a, 0x44, 0.92f), Magenta, 3f);
            TextAbs(boss, "AreaTag", "BOSS BATTLE", "", 16, MagentaBright, TextAnchor.MiddleLeft, FontStyle.Bold, 30f, -22f, 320f, 36f);
            TextAbs(boss, "StageTag", "STAGE", "Round", 16, CyanBright, TextAnchor.MiddleRight, FontStyle.Bold, 1430f, -22f, 330f, 36f);
            TextAbs(boss, "BossMeta", "BOSS HP", "", 24, White, TextAnchor.MiddleLeft, FontStyle.Bold, 30f, 20f, 480f, 40f);
            TextAbs(boss, "BossDifficulty", "NORMAL", "BossDifficultyLabel", 24, YellowBright, TextAnchor.MiddleLeft, FontStyle.Bold, 230f, 20f, 320f, 40f, BossRaidUiColorRole.Difficulty);
            TextAbs(boss, "BossNumbers", "0 / 1,400,000", "BossDamageText", 34, YellowBright, TextAnchor.MiddleRight, FontStyle.Bold, 1130f, 14f, 620f, 52f);
            BossBar(boss, 30f, 76f, 1740f, 64f, false);
            PlayerRow(boss, 30f, 156f, 1740f, 48f);
            return root;
        }

        private static GameObject CreateResultScreen(bool isClear)
        {
            var root = ScreenRoot(isClear ? "SuccessResultScreen" : "FailResultScreen", true, false, isClear ? "STAGE CLEAR" : "GAME OVER");
            var resultColor = isClear ? Green : Red;

            TextAbs(root.transform, "Crest", isClear ? "STAGE CLEAR" : "BOSS SURVIVED", "", 46, resultColor, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 198f, 1920f, 64f, BossRaidUiColorRole.Result, BossRaidUiBindingSource.None, -1, false, true);
            var verdict = TextAbs(root.transform, "Verdict", isClear ? "VICTORY" : "DEFEAT", "ResultText", 150, resultColor, TextAnchor.MiddleCenter, FontStyle.Bold, 80f, 268f, 1760f, 170f, BossRaidUiColorRole.Result, BossRaidUiBindingSource.None, -1, false, true);
            ApplyChromaticGlow(verdict, 3f);
            TextAbs(root.transform, "Message", isClear ? "BOSS DEFEATED. PRIZE POOL INCREASED." : "BOSS SURVIVED. FAILURE COUNT INCREASED.", "ResultMessage", 38, White, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 462f, 1920f, 56f);
            TextAbs(root.transform, "MapInfo", "MAP / MODE / BOSS", "ResultMapInfo", 28, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 160f, 528f, 1600f, 48f);
            var extra = TextAbs(root.transform, "Extra", isClear ? "PRIZE ADDED TO POOL" : "INSERT COIN TO CONTINUE", "ResultExtra", 22, isClear ? YellowBright : Red, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 596f, 1920f, 44f);
            Blink(extra, 0.9f, 1f, 0.3f);

            var bar = PanelAbs("ResultBossBar", root.transform, 200f, 658f, 1520f, 90f, C(0x1a, 0x07, 0x38), Cyan, 3f);
            BossBar(bar, 0f, 0f, 1520f, 90f, true);

            var stats = RectAbs("Stats", root.transform, 60f, 800f, 1800f, 220f);
            var statWidth = (1800f - 72f) / 4f;
            ResultStat(stats, 0, 0f, 0f, statWidth, 200f);
            ResultStat(stats, 1, statWidth + 24f, 0f, statWidth, 200f);
            ResultStat(stats, 2, (statWidth + 24f) * 2f, 0f, statWidth, 200f);
            ResultStat(stats, 3, (statWidth + 24f) * 3f, 0f, statWidth, 200f);
            return root;
        }

        private static GameObject ScreenRoot(string name, bool useHud, bool useSun, string screenLabel = "STANDBY")
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = DesignResolution;
            root.AddComponent<Image>().color = Background;

            BackgroundDecor(root.transform, useSun);
            if (useHud)
            {
                Hud(root.transform, screenLabel);
            }

            return root;
        }

        private static void BackgroundDecor(Transform root, bool useSun)
        {
            PanelAbs("RadialCyan", root, -120f, 620f, 2160f, 460f, C(0x00, 0xf0, 0xff, 0.055f), C(0x00, 0xf0, 0xff, 0f), 0f);
            PanelAbs("RadialMagenta", root, 300f, 170f, 1320f, 500f, C(0xff, 0x2b, 0xd6, 0.075f), C(0xff, 0x2b, 0xd6, 0f), 0f);

            if (useSun)
            {
                var sun = PanelAbs("SunHorizon", root, 690f, 160f, 540f, 300f, C(0xff, 0x8a, 0x3c, 0.30f), C(0xff, 0xe6, 0x00, 0.18f), 2f);
                for (var i = 0; i < 8; i++)
                {
                    PanelAbs("SunScan_" + i, sun, 0f, 34f + i * 32f, 540f, 8f, C(0x05, 0x00, 0x18, 0.55f), C(0x05, 0x00, 0x18, 0f), 0f);
                }
            }

            var floor = RectAbs("GridFloor", root, 0f, 680f, 1920f, 400f);
            floor.gameObject.AddComponent<Image>().color = C(0x1a, 0x07, 0x38, 0.24f);
            for (var i = 0; i <= 8; i++)
            {
                PanelAbs("GridH_" + i, floor, 0f, i * 50f, 1920f, 2f, C(0x00, 0xf0, 0xff, 0.18f), C(0x00, 0xf0, 0xff, 0f), 0f);
            }

            for (var i = 0; i <= 12; i++)
            {
                PanelAbs("GridV_" + i, floor, i * 160f, 0f, 2f, 400f, C(0xff, 0x2b, 0xd6, 0.11f), C(0xff, 0x2b, 0xd6, 0f), 0f);
            }

            for (var i = 0; i < 70; i++)
            {
                PanelAbs("Scan_" + i, root, 0f, i * 16f, 1920f, 1f, C(0x00, 0x00, 0x00, 0.10f), C(0x00, 0x00, 0x00, 0f), 0f);
            }
        }

        private static void Hud(Transform root, string screenLabel)
        {
            var hud = RectAbs("Hud", root, 24f, 24f, 1872f, 124f);
            var left = PanelAbs("HeaderLeft", hud, 0f, 0f, 540f, 124f, Panel, Cyan, 2f);
            TextAbs(left, "Corner", "EVENT", "", 16, CyanBright, TextAnchor.MiddleLeft, FontStyle.Bold, 14f, -14f, 160f, 26f);
            TextAbs(left, "EventTitle", "BOSS RAID", "EventTitle", 34, YellowBright, TextAnchor.MiddleLeft, FontStyle.Bold, 20f, 16f, 500f, 44f, BossRaidUiColorRole.Gold, BossRaidUiBindingSource.None, -1, false, true);
            PanelAbs("TeamBadge", left, 20f, 76f, 14f, 32f, Magenta, Magenta, 0f);
            TextAbs(left, "Role", "CURRENT TEAM", "", 16, Faint, TextAnchor.MiddleLeft, FontStyle.Bold, 48f, 70f, 220f, 22f);
            TextAbs(left, "Team", "Team A", "CurrentTeamName", 28, MagentaBright, TextAnchor.MiddleLeft, FontStyle.Bold, 48f, 88f, 460f, 30f, BossRaidUiColorRole.CurrentTeam);

            var center = PanelAbs("HeaderCenter", hud, 556f, 0f, 716f, 124f, Panel, Magenta, 2f);
            TextAbs(center, "Corner", "STATUS", "", 16, MagentaBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, -14f, 716f, 26f);
            TextAbs(center, "Eyebrow", "NOW SHOWING", "", 16, Faint, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 16f, 716f, 24f);
            var screenLabelText = TextAbs(center, "Screen", screenLabel, "ScreenLabel", 36, MagentaBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 42f, 716f, 48f, BossRaidUiColorRole.Screen, BossRaidUiBindingSource.None, -1, false, true);
            ApplyChromaticGlow(screenLabelText, 1.5f);
            TextAbs(center, "Map", "Waiting for map", "HeaderMapTitle", 24, Soft, TextAnchor.MiddleCenter, FontStyle.Bold, 28f, 90f, 660f, 28f);

            var right = PanelAbs("HeaderRight", hud, 1288f, 0f, 584f, 124f, Panel, Yellow, 2f);
            TextAbs(right, "Corner", "SCOREBOARD", "", 16, YellowBright, TextAnchor.MiddleLeft, FontStyle.Bold, 14f, -14f, 220f, 26f);
            HeaderStat(right, "Stage", "Round", CyanBright, 0);
            HeaderStat(right, "Prize", "PrizePool", YellowBright, 1);
            HeaderStat(right, "Burger", "BurgerCount", MagentaBright, 2);
            HeaderStat(right, "Record", "Record", Green, 3);
            TextAbs(hud, "Connection", "BRIDGE ONLINE", "ConnectionStatus", 16, Green, TextAnchor.MiddleRight, FontStyle.Bold, 1330f, 126f, 540f, 30f);
        }

        private static void HeaderStat(Transform parent, string label, string key, Color color, int index)
        {
            var tile = PanelAbs("Stat_" + label, parent, 12f + index * 140f, 14f, 130f, 96f, C(0x0d, 0x03, 0x22, 0.72f), GridLine, 1.3f);
            TextAbs(tile, "Label", label, "", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 4f, 8f, 122f, 24f);
            TextAbs(tile, "Value", "0", key, 30, color, TextAnchor.MiddleCenter, FontStyle.Bold, 4f, 36f, 122f, 50f);
        }

        private static void TeamCard(Transform parent, int index, float x, float y, float width, float height)
        {
            var color = TeamColor(index);
            var card = PanelAbs("TeamCard_" + (index + 1), parent, x, y, width, height, C(0x24, 0x0a, 0x44, 0.82f), color, 2f);
            Bind(card.gameObject, "ItemVisual", BossRaidUiBindingSource.Team, index, BossRaidUiColorRole.None, false);
            var ribbon = PanelAbs("Ribbon", card, 0f, -16f, 140f, 30f, color, color, 0f);
            TextAbs(ribbon, "Text", "PLAYER " + (index + 1).ToString("00"), "", 14, Background, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 0f, 140f, 30f);
            TextAbs(card, "TeamName", "TEAM", "TeamName", 34, color, TextAnchor.MiddleLeft, FontStyle.Bold, 22f, 34f, width - 44f, 50f, BossRaidUiColorRole.Team, BossRaidUiBindingSource.Team, index);
            TextAbs(card, "Roster", "ROSTER PENDING", "TeamRoster", 22, Soft, TextAnchor.MiddleLeft, FontStyle.Bold, 22f, 96f, width - 44f, 58f, BossRaidUiColorRole.Muted, BossRaidUiBindingSource.Team, index);
            TextAbs(card, "Ready", "READY", "TeamReadyStatus", 16, index == 0 ? Green : Faint, TextAnchor.MiddleRight, FontStyle.Bold, width - 170f, 160f, 148f, 28f, BossRaidUiColorRole.None, BossRaidUiBindingSource.Team, index);
        }

        private static void BurgerMapCard(Transform parent, int index, float x, float y, float width, float height)
        {
            var card = PanelAbs("BurgerMap_" + index, parent, x, y, width, height, Card, Line, 1.5f);
            Bind(card.gameObject, "ItemVisual", BossRaidUiBindingSource.BurgerMap, index, BossRaidUiColorRole.None, false);
            TextAbs(card, "Id", "NM1", "MapId", 18, CyanBright, TextAnchor.MiddleLeft, FontStyle.Bold, 16f, 12f, width - 32f, 26f, BossRaidUiColorRole.Cyan, BossRaidUiBindingSource.BurgerMap, index);
            TextAbs(card, "Title", "Initial", "MapTitle", 24, White, TextAnchor.MiddleLeft, FontStyle.Bold, 16f, 46f, width - 32f, 42f, BossRaidUiColorRole.White, BossRaidUiBindingSource.BurgerMap, index);
            TextAbs(card, "Meta", "Mapper", "MapCreator", 20, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, 16f, height - 36f, width - 32f, 26f, BossRaidUiColorRole.Muted, BossRaidUiBindingSource.BurgerMap, index);

            var stamp = PanelAbs("BurgerStamp", card, width - 116f, height - 36f, 100f, 28f, Yellow, Yellow, 0f);
            Bind(stamp.gameObject, "MapBurgerTag", BossRaidUiBindingSource.BurgerMap, index, BossRaidUiColorRole.None, true);
            TextAbs(stamp, "Text", "JACKPOT", "", 14, Background, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 0f, 100f, 28f);
            BlinkGroup(stamp, 2.0f, 1f, 0.65f, true);
        }

        private static void DifficultyCard(Transform parent, int index, float x, float y, float width, float height)
        {
            var colors = new[] { Green, YellowBright, MagentaBright };
            var names = new[] { "Easy", "Normal", "Hard" };
            var card = PanelAbs("Difficulty_" + index, parent, x, y, width, height, Card, GridLine, 2f);
            Bind(card.gameObject, "ItemVisual", BossRaidUiBindingSource.Difficulty, index, BossRaidUiColorRole.None, false);
            var ribbon = PanelAbs("Ribbon", card, width * 0.5f - 130f, -18f, 260f, 34f, colors[index], colors[index], 0f);
            TextAbs(ribbon, "Text", "STAGE " + (index + 1), "", 14, Background, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 0f, 260f, 34f);
            TextAbs(card, "Selected", "", "DifficultySelected", 16, colors[index], TextAnchor.MiddleRight, FontStyle.Bold, width - 180f, 20f, 156f, 30f, BossRaidUiColorRole.Difficulty, BossRaidUiBindingSource.Difficulty, index, true);
            TextAbs(card, "Icon", index == 0 ? "I" : index == 1 ? "II" : "III", "", 76, colors[index], TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 74f, width, 96f);
            TextAbs(card, "Label", names[index].ToUpperInvariant(), "DifficultyLabel", 58, colors[index], TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 178f, width, 76f, BossRaidUiColorRole.Difficulty, BossRaidUiBindingSource.Difficulty, index);
            PanelAbs("Rule", card, 42f, 282f, width - 84f, 2f, GridLine, GridLine, 0f);
            TextAbs(card, "HpLabel", "BOSS HP", "", 16, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, 42f, 314f, 200f, 28f);
            TextAbs(card, "Hp", "HP", "DifficultyHp", 40, White, TextAnchor.MiddleRight, FontStyle.Bold, 240f, 298f, width - 282f, 56f, BossRaidUiColorRole.White, BossRaidUiBindingSource.Difficulty, index);
            TextAbs(card, "PrizeLabel", "PRIZE", "", 16, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, 42f, 392f, 200f, 28f);
            TextAbs(card, "Prize", "+0", "DifficultyPrize", 40, CyanBright, TextAnchor.MiddleRight, FontStyle.Bold, 240f, 376f, width - 282f, 56f, BossRaidUiColorRole.Cyan, BossRaidUiBindingSource.Difficulty, index);
        }

        private static void ModeCard(Transform parent, int index, string mode, float x, float y, float width, float height)
        {
            var card = PanelAbs("Mode_" + mode, parent, x, y, width, height, Card, GridLine, 2f);
            Bind(card.gameObject, "ItemVisual", BossRaidUiBindingSource.Mode, index, BossRaidUiColorRole.None, false);

            var spin = PanelAbs("SpinStamp", card, width * 0.5f - 130f, -20f, 260f, 36f, Magenta, Magenta, 0f);
            Bind(spin.gameObject, "ModeActiveTag", BossRaidUiBindingSource.Mode, index, BossRaidUiColorRole.None, true);
            TextAbs(spin, "Text", "NOW SPINNING", "", 14, Background, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 0f, 260f, 36f);
            BlinkGroup(spin, 0.7f, 1f, 0.35f);

            TextAbs(card, "ModeName", mode, "ModeName", 118, White, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 112f, width, 142f, BossRaidUiColorRole.Cyan, BossRaidUiBindingSource.Mode, index);
            TextAbs(card, "Desc", ModeDescription(mode), "ModeDescription", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 286f, width, 30f, BossRaidUiColorRole.Muted, BossRaidUiBindingSource.Mode, index);
            TextAbs(card, "Count", "6 MAPS", "ModeCount", 30, CyanBright, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 328f, width, 46f, BossRaidUiColorRole.Cyan, BossRaidUiBindingSource.Mode, index);
        }

        private static void MapRouletteCard(Transform parent, int index, float x, float y, float width, float height)
        {
            var card = PanelAbs("MapCard_" + index, parent, x, y, width, height, Card, GridLine, 2f);
            Bind(card.gameObject, "ItemVisual", BossRaidUiBindingSource.SelectedModeMap, index, BossRaidUiColorRole.None, false);

            var active = PanelAbs("ActiveStamp", card, width * 0.5f - 130f, -20f, 260f, 36f, Magenta, Magenta, 0f);
            Bind(active.gameObject, "MapActiveTag", BossRaidUiBindingSource.SelectedModeMap, index, BossRaidUiColorRole.None, true);
            TextAbs(active, "Text", "NOW SPINNING", "", 14, Background, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 0f, 260f, 36f);
            BlinkGroup(active, 0.7f, 1f, 0.35f);

            TextAbs(card, "Id", "NM1", "MapId", 17, CyanBright, TextAnchor.MiddleLeft, FontStyle.Bold, 28f, 24f, 120f, 30f, BossRaidUiColorRole.Cyan, BossRaidUiBindingSource.SelectedModeMap, index);
            TextAbs(card, "Mode", "NM", "SelectedMapMode", 17, YellowBright, TextAnchor.MiddleLeft, FontStyle.Bold, 158f, 24f, 120f, 30f, BossRaidUiColorRole.Gold, BossRaidUiBindingSource.SelectedModeMap, index);
            TextAbs(card, "Title", "Initial", "MapTitle", 34, White, TextAnchor.MiddleLeft, FontStyle.Bold, 28f, 74f, width - 56f, 54f, BossRaidUiColorRole.White, BossRaidUiBindingSource.SelectedModeMap, index);
            TextAbs(card, "Diff", "Difficulty", "MapDifficultyName", 24, CyanBright, TextAnchor.MiddleLeft, FontStyle.Bold, 28f, 132f, width - 56f, 42f, BossRaidUiColorRole.Cyan, BossRaidUiBindingSource.SelectedModeMap, index);
            PanelAbs("Rule", card, 28f, height - 62f, width - 56f, 1.5f, GridLine, GridLine, 0f);
            TextAbs(card, "Creator", "BY", "MapCreator", 22, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, 28f, height - 54f, width - 56f, 36f, BossRaidUiColorRole.Muted, BossRaidUiBindingSource.SelectedModeMap, index);

            var stamp = PanelAbs("BurgerStamp", card, width - 138f, 22f, 110f, 32f, Yellow, Yellow, 0f);
            Bind(stamp.gameObject, "MapBurgerTag", BossRaidUiBindingSource.SelectedModeMap, index, BossRaidUiColorRole.None, true);
            TextAbs(stamp, "Text", "JACKPOT", "", 15, Background, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 0f, 110f, 32f);
            BlinkGroup(stamp, 2.0f, 1f, 0.65f, true);

            TextAbs(card, "Played", "PLAYED", "MapPlayedTag", 36, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, height * 0.5f - 36f, width, 72f, BossRaidUiColorRole.Muted, BossRaidUiBindingSource.SelectedModeMap, index, true);
        }

        private static void MapBanner(Transform parent, float x, float y, float width, float height)
        {
            var banner = PanelAbs("MapBanner", parent, x, y, width, height, Panel, Magenta, 3f);
            TextAbs(banner, "Stage", "STAGE", "Round", 16, CyanBright, TextAnchor.MiddleLeft, FontStyle.Bold, 30f, -22f, 300f, 34f);

            var burgerStamp = PanelAbs("BurgerStamp", banner, width - 290f, -22f, 260f, 38f, Yellow, Yellow, 0f);
            Bind(burgerStamp.gameObject, "BurgerMarker", BossRaidUiBindingSource.None, -1, BossRaidUiColorRole.None, true);
            TextAbs(burgerStamp, "Text", "JACKPOT TARGET", "", 16, Background, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 0f, 260f, 38f);
            BlinkGroup(burgerStamp, 2.0f, 1f, 0.65f, true);

            TextAbs(banner, "Eyebrow", "SELECTED MAP", "", 17, CyanBright, TextAnchor.MiddleLeft, FontStyle.Bold, 36f, 28f, 420f, 30f);
            TextAbs(banner, "Title", "No map selected", "SelectedMapTitle", 62, White, TextAnchor.MiddleLeft, FontStyle.Bold, 36f, 58f, 1160f, 76f);
            TextAbs(banner, "Artist", "ARTIST", "SelectedMapArtist", 25, Soft, TextAnchor.MiddleLeft, FontStyle.Bold, 36f, 144f, 380f, 36f);
            TextAbs(banner, "Mapper", "MAPPER", "SelectedMapMapper", 25, Soft, TextAnchor.MiddleLeft, FontStyle.Bold, 430f, 144f, 360f, 36f);
            TextAbs(banner, "DiffName", "DIFFICULTY", "SelectedMapDifficultyName", 25, Soft, TextAnchor.MiddleLeft, FontStyle.Bold, 800f, 144f, 560f, 36f);
            PanelAbs("Divider", banner, 1378f, 26f, 2f, 168f, C(0xb3, 0x14, 0x8a, 0.65f), C(0xb3, 0x14, 0x8a, 0f), 0f);
            TextAbs(banner, "ModeLabel", "MODE", "", 16, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, 1410f, 42f, 120f, 28f);
            TextAbs(banner, "Mode", "NM", "SelectedMapMode", 34, CyanBright, TextAnchor.MiddleRight, FontStyle.Bold, 1510f, 34f, 230f, 42f, BossRaidUiColorRole.Cyan);
            TextAbs(banner, "BossLabel", "BOSS", "", 16, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, 1410f, 92f, 120f, 28f);
            TextAbs(banner, "Diff", "Normal", "CurrentDifficultyLabel", 30, YellowBright, TextAnchor.MiddleRight, FontStyle.Bold, 1510f, 84f, 230f, 42f, BossRaidUiColorRole.Difficulty);
            TextAbs(banner, "HpLabel", "HP", "", 16, Muted, TextAnchor.MiddleLeft, FontStyle.Bold, 1410f, 142f, 120f, 28f);
            TextAbs(banner, "Hp", "HP 1,400,000", "CurrentDifficultyHp", 28, MagentaBright, TextAnchor.MiddleRight, FontStyle.Bold, 1490f, 134f, 250f, 42f);
        }

        private static void SpectatorSlots(Transform parent, float x, float y, float width, float height, bool live)
        {
            var root = RectAbs("SpectatorSlots", parent, x, y, width, height);
            var slotWidth = (width - 36f) / 3f;
            for (var i = 0; i < 3; i++)
            {
                var slot = PanelAbs("SpectatorSlot_" + (i + 1), root, i * (slotWidth + 18f), 0f, slotWidth, height, C(0x05, 0x00, 0x18, 0.90f), GridLine, 2f);
                Bind(slot.gameObject, "ItemVisual", BossRaidUiBindingSource.Spectator, i, BossRaidUiColorRole.None, false);
                TextAbs(slot, "PlayerSlot", "P" + (i + 1), "PlayerSlot", 16, CyanBright, TextAnchor.MiddleLeft, FontStyle.Bold, 16f, 14f, 80f, 30f, BossRaidUiColorRole.Cyan, BossRaidUiBindingSource.CurrentTeamPlayer, i);
                TextAbs(slot, "PlayerName", "PLAYER", "PlayerName", live ? 28 : 20, live ? CyanBright : Soft, live ? TextAnchor.LowerLeft : TextAnchor.UpperLeft, FontStyle.Bold, live ? 18f : 88f, live ? height - 60f : 14f, slotWidth - 36f, live ? 42f : 32f, live ? BossRaidUiColorRole.Cyan : BossRaidUiColorRole.White, BossRaidUiBindingSource.CurrentTeamPlayer, i);
                if (live)
                {
                    var liveTag = PanelAbs("LiveTag", slot, slotWidth - 100f, 14f, 84f, 30f, Red, Red, 0f);
                    TextAbs(liveTag, "Text", "LIVE", "", 14, White, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 0f, 84f, 30f);
                }

                TextAbs(slot, "Guide", live ? "in-game capture" : "- osu! tourney -", "", 20, Faint, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 0f, slotWidth, height);
            }
        }

        private static void ReadySidePanel(Transform parent)
        {
            var panel = PanelAbs("SidePanel", parent, 1480f, 460f, 380f, 540f, Panel, Cyan, 2f);
            TextAbs(panel, "Title", "ROOM CHAT", "", 18, CyanBright, TextAnchor.MiddleLeft, FontStyle.Bold, 22f, 22f, 330f, 34f);
            PanelAbs("Rule", panel, 22f, 66f, 336f, 2f, C(0x00, 0x8a, 0x9a, 0.75f), C(0x00, 0x8a, 0x9a, 0f), 0f);
            var chat = PanelAbs("ChatBox", panel, 22f, 88f, 336f, 420f, C(0x05, 0x00, 0x18, 0.72f), C(0xb3, 0x14, 0x8a, 0.75f), 1.5f);
            TextAbs(chat, "ChatBody", "Waiting for room chat...", "IngameChat", 22, Soft, TextAnchor.LowerLeft, FontStyle.Bold, 14f, 12f, 308f, 392f);
        }

        private static void BossBar(Transform parent, float x, float y, float width, float height, bool resultMode)
        {
            var track = PanelAbs("BossBar", parent, x, y, width, height, Mid, C(0x00, 0x8a, 0x9a, 0.8f), 2f);
            var fill = RectStretch("DamageFill", track, Vector2.zero, new Vector2(resultMode ? 1f : 0.42f, 1f), Vector2.zero, Vector2.zero);
            fill.gameObject.AddComponent<Image>().color = resultMode ? Green : Magenta;
            fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Bind(fill.gameObject, "BossDamageFill", BossRaidUiBindingSource.None, -1, resultMode ? BossRaidUiColorRole.Result : BossRaidUiColorRole.None, false);
            for (var i = 0; i < 32; i++)
            {
                PanelAbs("Stripe_" + i, fill, i * 54f, 0f, 5f, height, C(0x00, 0x00, 0x00, 0.18f), C(0x00, 0x00, 0x00, 0f), 0f);
            }

            TextAbs(track, "Percent", "0.0%", "BossDamagePercent", 27, White, TextAnchor.MiddleCenter, FontStyle.Bold, 0f, 0f, width, height);
        }

        private static void PlayerRow(Transform parent, float x, float y, float width, float height)
        {
            var row = RectAbs("PlayerRow", parent, x, y, width, height);
            var tileWidth = (width - 32f) / 3f;
            for (var i = 0; i < 3; i++)
            {
                var tile = PanelAbs("PlayerTile_" + (i + 1), row, i * (tileWidth + 16f), 0f, tileWidth, height, C(0x05, 0x00, 0x18, 0.72f), GridLine, 1.4f);
                Bind(tile.gameObject, "ItemVisual", BossRaidUiBindingSource.CurrentTeamPlayer, i, BossRaidUiColorRole.None, false);
                TextAbs(tile, "Slot", "P" + (i + 1), "PlayerSlot", 16, MagentaBright, TextAnchor.MiddleLeft, FontStyle.Bold, 18f, 0f, 64f, height, BossRaidUiColorRole.CurrentTeam, BossRaidUiBindingSource.CurrentTeamPlayer, i);
                TextAbs(tile, "Name", "PLAYER", "PlayerName", 24, White, TextAnchor.MiddleLeft, FontStyle.Bold, 80f, 0f, tileWidth - 250f, height, BossRaidUiColorRole.White, BossRaidUiBindingSource.CurrentTeamPlayer, i);
                TextAbs(tile, "Score", "-- TOSU --", "", 22, Faint, TextAnchor.MiddleRight, FontStyle.Bold, tileWidth - 200f, 0f, 182f, height);
            }
        }

        private static void ResultStat(Transform parent, int index, float x, float y, float width, float height)
        {
            var colors = new[] { YellowBright, MagentaBright, Red, Green };
            var tile = PanelAbs("ResultStat_" + index, parent, x, y, width, height, Panel, colors[index], 2f);
            Bind(tile.gameObject, "ItemVisual", BossRaidUiBindingSource.ResultStat, index, BossRaidUiColorRole.None, false);
            TextAbs(tile, "Label", "STAT", "StatLabel", 18, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 18f, 22f, width - 36f, 34f, BossRaidUiColorRole.Muted, BossRaidUiBindingSource.ResultStat, index);
            TextAbs(tile, "Value", "0", "StatValue", 60, colors[index], TextAnchor.MiddleCenter, FontStyle.Bold, 18f, 70f, width - 36f, 86f, BossRaidUiColorRole.Context, BossRaidUiBindingSource.ResultStat, index);
            TextAbs(tile, "Unit", index == 0 ? "KRW" : index == 3 ? "CLEAR / FAIL" : "COUNT", "", 22, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 18f, 158f, width - 36f, 34f);
        }

        private static RectTransform PanelAbs(string name, Transform parent, float x, float y, float width, float height, Color color, Color outline, float outlineDistance)
        {
            var rect = RectAbs(name, parent, x, y, width, height);
            rect.gameObject.AddComponent<Image>().color = color;
            if (outlineDistance > 0f)
            {
                var line = rect.gameObject.AddComponent<Outline>();
                line.effectColor = outline;
                line.effectDistance = new Vector2(outlineDistance, -outlineDistance);
            }

            return rect;
        }

        private static RectTransform RectAbs(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static RectTransform RectStretch(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
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

        private static Text TextAbs(
            Transform parent,
            string name,
            string display,
            string key,
            int size,
            Color color,
            TextAnchor anchor,
            FontStyle style,
            float x,
            float y,
            float width,
            float height,
            BossRaidUiColorRole colorRole = BossRaidUiColorRole.None,
            BossRaidUiBindingSource source = BossRaidUiBindingSource.None,
            int index = -1,
            bool hideWhenEmpty = false,
            bool glow = false)
        {
            var rect = RectAbs(name, parent, x, y, width, height);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = display;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, Mathf.FloorToInt(size * 0.55f));
            text.resizeTextMaxSize = size;

            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = glow ? new Color(color.r, color.g, color.b, 0.62f) : new Color(0f, 0f, 0f, 0.72f);
            shadow.effectDistance = glow ? new Vector2(2f, -2f) : new Vector2(2f, -2f);

            if (!string.IsNullOrEmpty(key) || source != BossRaidUiBindingSource.None || colorRole != BossRaidUiColorRole.None || hideWhenEmpty)
            {
                Bind(rect.gameObject, key, source, index, colorRole, hideWhenEmpty);
            }

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

        private static void Blink(Component target, float interval, float onAlpha, float offAlpha, bool smooth = false)
        {
            if (target == null)
            {
                return;
            }

            var blink = target.gameObject.AddComponent<BossRaidUiBlink>();
            blink.intervalSeconds = interval;
            blink.onAlpha = onAlpha;
            blink.offAlpha = offAlpha;
            blink.smooth = smooth;
        }

        private static void BlinkGroup(Component target, float interval, float onAlpha, float offAlpha, bool smooth = false)
        {
            if (target == null)
            {
                return;
            }

            var go = target.gameObject;
            if (go.GetComponent<CanvasGroup>() == null)
            {
                go.AddComponent<CanvasGroup>();
            }

            Blink(target, interval, onAlpha, offAlpha, smooth);
        }

        private static void ApplyChromaticGlow(Component target, float distance = 2f, float alpha = 0.7f)
        {
            if (target == null)
            {
                return;
            }

            var go = target.gameObject;

            var cyanShadow = go.AddComponent<Shadow>();
            cyanShadow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, alpha);
            cyanShadow.effectDistance = new Vector2(-distance, 0f);

            var magentaShadow = go.AddComponent<Shadow>();
            magentaShadow.effectColor = new Color(Magenta.r, Magenta.g, Magenta.b, alpha);
            magentaShadow.effectDistance = new Vector2(distance, 0f);
        }

        private static Color TeamColor(int index)
        {
            switch (index)
            {
                case 1:
                    return Cyan;
                case 2:
                    return Green;
                case 3:
                    return Yellow;
                default:
                    return Magenta;
            }
        }

        private static string ModeDescription(string mode)
        {
            switch (mode)
            {
                case "HD":
                    return "HIDDEN";
                case "HR":
                    return "HARD ROCK";
                case "DT":
                    return "DOUBLE TIME";
                default:
                    return "NO MOD";
            }
        }

        private static Color C(byte r, byte g, byte b, float a = 1f)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
