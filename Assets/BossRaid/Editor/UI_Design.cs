// =============================================================================
// UI_Design.cs
// -----------------------------------------------------------------------------
// v3 디자인 (Docs/design-mockup-v3.html) 그대로를 유니티 UGUI 프리팹으로 만든다.
// 메뉴: Boss Raid/UI_Design v3/Generate Prefabs
// 출력: Assets/BossRaid/Resources/BossRaidUi_v3/*.prefab (9개)
//
// 기존 BossRaidUiPrefabGenerator.cs, OverlayView 등과 완전히 독립.
// 모든 좌표는 1920x1080 디자인 해상도 기준 픽셀.
// =============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BossRaid.UIDesign.Editor
{
    public static class UI_Design
    {
        // ----- 출력 경로 & 디자인 해상도 -------------------------------------------------
        private const string OutputFolder = "Assets/BossRaid/Resources/BossRaidUi_v3";
        private static readonly Vector2 Design = new Vector2(1920f, 1080f);

        // ----- v3 컬러 팔레트 -----------------------------------------------------------
        private static readonly Color BgScreen = Hex("0d0322");
        private static readonly Color BgPanel  = Hex("1a0738");
        private static readonly Color BgCard   = Hex("240a44");
        private static readonly Color BgDarker = Hex("050018");

        private static readonly Color Line     = Hex("5e2d8f");

        private static readonly Color Magenta  = Hex("ff2bd6");
        private static readonly Color Cyan     = Hex("00f0ff");
        private static readonly Color Yellow   = Hex("ffe600");
        private static readonly Color Green    = Hex("00ff88");
        private static readonly Color Red      = Hex("ff3060");

        private static readonly Color Text      = Hex("f8eef8");
        private static readonly Color TextSoft  = Hex("d3bce8");
        private static readonly Color TextMuted = Hex("8a6fc4");
        private static readonly Color TextFaint = Hex("5e4a82");

        private static readonly Color Team1 = Magenta;
        private static readonly Color Team2 = Cyan;
        private static readonly Color Team3 = Green;
        private static readonly Color Team4 = Yellow;

        // ----- 메뉴 진입점 --------------------------------------------------------------
        [MenuItem("Boss Raid/UI_Design v3/Generate Prefabs")]
        public static void Generate()
        {
            if (Directory.Exists(OutputFolder))
            {
                FileUtil.DeleteFileOrDirectory(OutputFolder);
                FileUtil.DeleteFileOrDirectory(OutputFolder + ".meta");
            }
            Directory.CreateDirectory(OutputFolder);

            SavePrefab(BuildStandby(),        OutputFolder + "/StartScreen.prefab");
            SavePrefab(BuildBurgerReveal(),   OutputFolder + "/BurgerMapSelectScreen.prefab");
            SavePrefab(BuildDifficulty(),     OutputFolder + "/DifficultySelectScreen.prefab");
            SavePrefab(BuildModeRoulette(),   OutputFolder + "/ModeSelectScreen.prefab");
            SavePrefab(BuildMapRoulette(),    OutputFolder + "/MapSelectScreen.prefab");
            SavePrefab(BuildMapReady(),       OutputFolder + "/MapReadyScreen.prefab");
            SavePrefab(BuildInGame(),         OutputFolder + "/InGameScreen.prefab");
            SavePrefab(BuildResult(true),     OutputFolder + "/SuccessResultScreen.prefab");
            SavePrefab(BuildResult(false),    OutputFolder + "/FailResultScreen.prefab");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UI_Design v3] 9 prefabs generated at " + OutputFolder);
        }

        // =============================================================================
        // 1. STANDBY
        // =============================================================================
        private static GameObject BuildStandby()
        {
            var root = ScreenRoot("StartScreen");

            // INSERT COIN TO BEGIN
            var insertCoin = TopText(root.transform, "InsertCoin",
                "◆ INSERT COIN TO BEGIN ◆", 18, Cyan, true,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -200), new Vector2(600, 40));

            // BOSS RAID title (chrome → 단일 magenta 컬러로 근사)
            TitleText(root.transform, "Title", "BOSS RAID", 220, Magenta, true,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 80), new Vector2(1700, 260));

            // CO-OP RAID OVERLAY
            TitleText(root.transform, "Subtitle", "> CO-OP RAID OVERLAY _", 38, Cyan, true,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -70), new Vector2(1200, 60));

            // VersionBar 4개 칩
            var versionBar = CenteredRect("VersionBar", root.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -150), new Vector2(1100, 44));
            HLayoutAuto(versionBar.gameObject, 18, false, false);
            VerChip(versionBar, "SYSTEM ONLINE", Green, true);
            VerChip(versionBar, "v0.9.4",        TextMuted, false);
            VerChip(versionBar, "4 TEAMS LOADED",TextMuted, false);
            VerChip(versionBar, "24 MAPS READY", TextMuted, false);

            // 팀 카드 4개 (1640 wide)
            var teams = CenteredRect("Teams", root.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -340), new Vector2(1640, 200));
            HLayoutAuto(teams.gameObject, 22, true, true);

            TeamCard(teams, "PLAYER 01", "삼랄부마스터",
                "YIRIRU · MOON2DOOR · AXDVGN\nLEZHIC · VIICHAN · A",
                Team1, "▶ READY", Green);
            TeamCard(teams, "PLAYER 02", "TEAM B",
                "- ROSTER PENDING -", Team2, "— STANDBY —", TextFaint);
            TeamCard(teams, "PLAYER 03", "TEAM C",
                "- ROSTER PENDING -", Team3, "— STANDBY —", TextFaint);
            TeamCard(teams, "PLAYER 04", "TEAM D",
                "- ROSTER PENDING -", Team4, "— STANDBY —", TextFaint);

            // Footer
            TopText(root.transform, "Footer",
                "▼   PRESS ANY KEY TO START   ▼", 16, Yellow, true,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 36), new Vector2(900, 30));

            return root;
        }

        private static void TeamCard(RectTransform parent, string ribbon, string name,
            string roster, Color borderColor, string readyText, Color readyColor)
        {
            var card = Panel("TeamCard_" + ribbon, parent.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                BgCard, borderColor);
            card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Ribbon
            var ribbonRect = NewRect("Ribbon", card.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(-2, 0), new Vector2(130, 26));
            ribbonRect.gameObject.AddComponent<Image>().color = borderColor;
            BoxText(ribbonRect, "RibbonText", ribbon, 13, BgScreen);

            // Name
            BodyText(card.transform, "Name", name, 36, borderColor, true,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(22, -50), new Vector2(-22, -22));

            // Roster
            BodyText(card.transform, "Roster", roster, 24, TextSoft, false,
                new Vector2(0f, 0.18f), new Vector2(1f, 0.65f),
                new Vector2(22, 0), new Vector2(-22, 0));

            // ReadyTag
            BodyText(card.transform, "Ready", readyText, 13, readyColor, true,
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-150, 12), new Vector2(-14, 30));
        }

        private static void VerChip(RectTransform parent, string text, Color color, bool alive)
        {
            var chip = NewRect("Chip_" + text, parent.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            chip.gameObject.AddComponent<LayoutElement>().preferredWidth = 220;
            chip.gameObject.AddComponent<Image>().color = BgScreen;
            OutlineBorder(chip.gameObject, alive ? Green : Line, 2f);
            BoxText(chip, "Text", text, 14, color);
        }

        // =============================================================================
        // 2. BURGER REVEAL
        // =============================================================================
        private static GameObject BuildBurgerReveal()
        {
            var root = ScreenRoot("BurgerMapSelectScreen");

            // 타이틀
            TopText(root.transform, "Small", "▼ BONUS LOTTERY ▼", 16, TextMuted, false,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -50), new Vector2(700, 26));
            TitleText(root.transform, "Ttl", "BURGER LOCKDOWN", 70, Yellow, true,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -110), new Vector2(1500, 90));
            TopText(root.transform, "Sub", "▶ EIGHT MAPS CARRY VIEWER BURGER STAKES ◀", 30, Cyan, true,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -180), new Vector2(1500, 40));

            // ReelFrame
            var reel = Panel("ReelFrame", root.transform,
                new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(60, 130), new Vector2(-60, -230),
                BgDarker, Magenta);

            CapTab(reel, "CapL", "BONUS LOTTERY", Magenta, true);
            CapTab(reel, "CapR", "8 / 24 PICKED", Yellow, false);

            // 4행 그리드
            var grid = NewRect("Rows", reel.transform,
                Vector2.zero, Vector2.one,
                new Vector2(26, 26), new Vector2(-26, -26));
            VLayoutAuto(grid.gameObject, 18, true, true);

            string[] modes = { "NM", "HD", "HR", "DT" };
            int[,] burgerMap = new int[,] {
                { 0, 1, 0, 1, 0, 0 }, // NM
                { 1, 0, 0, 0, 1, 0 }, // HD
                { 0, 0, 1, 0, 0, 1 }, // HR
                { 0, 0, 0, 1, 0, 1 }  // DT
            };

            for (int r = 0; r < 4; r++)
            {
                var row = NewRect("Row_" + modes[r], grid.transform,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                row.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
                HLayoutAuto(row.gameObject, 14, true, true);

                // Mode tag (88px width)
                var modeTag = NewRect("ModeTag", row.transform,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                modeTag.gameObject.AddComponent<LayoutElement>().preferredWidth = 88;
                modeTag.gameObject.AddComponent<Image>().color = BgCard;
                OutlineBorder(modeTag.gameObject, Cyan, 2f);
                BodyText(modeTag.transform, "Lbl", modes[r], 36, Cyan, true,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                    TextAnchor.MiddleCenter);

                // 6 map cells
                for (int c = 0; c < 6; c++)
                {
                    bool burger = burgerMap[r, c] == 1;
                    var cell = Panel("Cell_" + modes[r] + (c + 1), row.transform,
                        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                        BgCard, burger ? Yellow : Line);
                    cell.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

                    BodyText(cell.transform, "Id", modes[r] + (c + 1), 17,
                        burger ? Yellow : Cyan, true,
                        new Vector2(0, 1), new Vector2(1, 1),
                        new Vector2(14, -30), new Vector2(-14, -8),
                        TextAnchor.UpperLeft);

                    BodyText(cell.transform, "Title", "Initial", 26, Text, false,
                        new Vector2(0, 0.32f), new Vector2(1, 0.72f),
                        new Vector2(14, 0), new Vector2(-14, 0),
                        TextAnchor.MiddleLeft);

                    BodyText(cell.transform, "Meta", "CHASER01", 22, TextMuted, false,
                        new Vector2(0, 0), new Vector2(1, 0.32f),
                        new Vector2(14, 6), new Vector2(-14, 0),
                        TextAnchor.LowerLeft);

                    if (burger)
                    {
                        // 🍔 (이모지 - 폰트에 따라 보일 수도 안 보일 수도 있음)
                        BodyText(cell.transform, "Burger", "🍔", 28, Yellow, true,
                            new Vector2(1, 1), new Vector2(1, 1),
                            new Vector2(-40, -38), new Vector2(-6, -6),
                            TextAnchor.MiddleCenter);

                        // JACKPOT stamp
                        var stamp = NewRect("Stamp", cell.transform,
                            new Vector2(1, 0), new Vector2(1, 0),
                            new Vector2(-100, 8), new Vector2(-12, 30));
                        stamp.gameObject.AddComponent<Image>().color = Yellow;
                        BodyText(stamp.transform, "T", "JACKPOT", 13, BgScreen, false,
                            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                            TextAnchor.MiddleCenter);
                    }
                }
            }

            // Footer
            TopText(root.transform, "Footer",
                "PRESS [ SPACE ] TO REROLL · 8/24 LOCKED", 16, TextMuted, false,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 50), new Vector2(800, 30));

            return root;
        }

        // =============================================================================
        // 3. DIFFICULTY
        // =============================================================================
        private static GameObject BuildDifficulty()
        {
            var root = ScreenRoot("DifficultySelectScreen");

            TopText(root.transform, "Small",
                "<<<  STAGE 03 / 08 · 삼랄부마스터  >>>", 16, Magenta, true,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -80), new Vector2(900, 30));

            TitleText(root.transform, "Ttl", "SELECT STAGE", 96, Magenta, true,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -180), new Vector2(1500, 120));

            TopText(root.transform, "Desc",
                "> CHOOSE THE BOSS DIFFICULTY", 32, TextSoft, false,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -280), new Vector2(1200, 40));

            // 3 cards
            var cards = NewRect("Cards", root.transform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(80, -900), new Vector2(-80, -360));
            HLayoutAuto(cards.gameObject, 36, true, true);

            DiffCard(cards, "EASY",   "▽", "1,000K", "3,000₩", "STAGE I · EASY",   Green,   false);
            DiffCard(cards, "NORMAL", "◇", "1,400K", "5,000₩", "STAGE II · NORMAL", Yellow, true);
            DiffCard(cards, "HARD",   "△", "2,000K", "15,000₩","STAGE III · HARD",  Magenta, false);

            // Footer (간단한 텍스트만)
            TopText(root.transform, "Footer",
                "[1][2][3] · [E][N][H] SELECT  ◆  [ENTER] CONFIRM", 16, TextMuted, false,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 50), new Vector2(1100, 30));

            return root;
        }

        private static void DiffCard(RectTransform parent, string name, string icon,
            string hp, string prize, string ribbon, Color accent, bool selected)
        {
            var card = Panel("Diff_" + name, parent.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                BgCard, selected ? accent : Line);
            card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // selected 카드는 살짝 위로 (anchoredPosition은 LayoutGroup이 덮으므로 padding으로 처리하기엔 복잡)
            // 대신 selected만 ribbon 색깔과 두꺼운 outline으로 강조

            // Ribbon (상단 중앙)
            var rib = NewRect("Ribbon", card.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-130, -2), new Vector2(130, 30));
            rib.gameObject.AddComponent<Image>().color = accent;
            BoxText(rib, "Text", ribbon, 14, name == "HARD" ? Text : BgScreen);

            // SELECTED 마크
            if (selected)
            {
                var pick = NewRect("Pick", card.transform,
                    new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-150, -50), new Vector2(-18, -22));
                pick.gameObject.AddComponent<Image>().color = BgScreen;
                OutlineBorder(pick.gameObject, accent, 2f);
                BoxText(pick, "T", "◆ SELECTED ◆", 13, accent);
            }

            // Icon
            BodyText(card.transform, "Icon", icon, 80, accent, true,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -210), new Vector2(0, -80),
                TextAnchor.MiddleCenter);

            // Name
            BodyText(card.transform, "Name", name, 64, accent, true,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -300), new Vector2(0, -210),
                TextAnchor.MiddleCenter);

            // Divider
            var divider = NewRect("Divider", card.transform,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(32, 220), new Vector2(-32, 222));
            divider.gameObject.AddComponent<Image>().color = Line;

            // HP row
            BodyText(card.transform, "HpLbl", "BOSS HP", 14, TextMuted, false,
                new Vector2(0, 0), new Vector2(0.5f, 0),
                new Vector2(32, 160), new Vector2(0, 200),
                TextAnchor.MiddleLeft);
            BodyText(card.transform, "HpVal", hp, 48, selected ? accent : Text, true,
                new Vector2(0.5f, 0), new Vector2(1, 0),
                new Vector2(0, 150), new Vector2(-32, 210),
                TextAnchor.MiddleRight);

            // PRIZE row
            BodyText(card.transform, "PrLbl", "PRIZE", 14, TextMuted, false,
                new Vector2(0, 0), new Vector2(0.5f, 0),
                new Vector2(32, 90), new Vector2(0, 130),
                TextAnchor.MiddleLeft);
            BodyText(card.transform, "PrVal", prize, 48, Cyan, true,
                new Vector2(0.5f, 0), new Vector2(1, 0),
                new Vector2(0, 80), new Vector2(-32, 140),
                TextAnchor.MiddleRight);
        }

        // =============================================================================
        // 4. MODE ROULETTE
        // =============================================================================
        private static GameObject BuildModeRoulette()
        {
            var root = ScreenRoot("ModeSelectScreen");

            TopText(root.transform, "Small",
                "▶ TEAM 삼랄부마스터 · BOSS NORMAL · STAGE 03/08 ◀", 16, TextMuted, false,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -80), new Vector2(1200, 30));
            TitleText(root.transform, "Ttl", "MODE LOTTERY", 96, Magenta, true,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -190), new Vector2(1600, 120));

            // pointer up (위 노란 삼각형)
            var pUp = NewRect("PointerUp", root.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-22, -295 - 28), new Vector2(22, -295));
            pUp.gameObject.AddComponent<Image>().color = Yellow;

            // ReelFrame
            var reel = Panel("ReelFrame", root.transform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(80, -860), new Vector2(-80, -320),
                BgDarker, Cyan);

            var inner = NewRect("Inner", reel.transform,
                Vector2.zero, Vector2.one,
                new Vector2(30, 30), new Vector2(-30, -30));
            HLayoutAuto(inner.gameObject, 24, true, true);

            ModeTile(inner, "NM", "NO MOD",      false);
            ModeTile(inner, "HD", "HIDDEN",      false);
            ModeTile(inner, "HR", "HARD ROCK",   true);
            ModeTile(inner, "DT", "DOUBLE TIME", false);

            // pointer down
            var pDn = NewRect("PointerDown", root.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-22, -864), new Vector2(22, -836));
            pDn.gameObject.AddComponent<Image>().color = Yellow;

            // Footer
            TopText(root.transform, "Footer",
                "PRESS [ SPACE ] TO SPIN THE WHEEL", 16, TextMuted, false,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 60), new Vector2(800, 30));

            return root;
        }

        private static void ModeTile(RectTransform parent, string label, string desc, bool active)
        {
            var tile = Panel("Mode_" + label, parent.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                active ? BgPanel : BgCard, active ? Magenta : Line);
            tile.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // NOW SPINNING 태그
            if (active)
            {
                var tag = NewRect("Spin", tile.transform,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-150, -2), new Vector2(150, 30));
                tag.gameObject.AddComponent<Image>().color = Magenta;
                BoxText(tag, "T", "▶ NOW SPINNING ◀", 14, BgScreen);
            }

            BodyText(tile.transform, "Label", label, 156,
                active ? Magenta : Text, true,
                new Vector2(0, 0.32f), new Vector2(1, 0.85f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

            BodyText(tile.transform, "Desc", desc, 16, TextMuted, false,
                new Vector2(0, 0.16f), new Vector2(1, 0.28f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

            BodyText(tile.transform, "Count", "6 MAPS", 32, Cyan, true,
                new Vector2(0, 0.03f), new Vector2(1, 0.15f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        }

        // =============================================================================
        // 5. MAP ROULETTE
        // =============================================================================
        private static GameObject BuildMapRoulette()
        {
            var root = ScreenRoot("MapSelectScreen");

            TopText(root.transform, "Small",
                "▶ STAGE 03 / 08 · MODE LOCKED ◀", 15, TextMuted, false,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -60), new Vector2(900, 30));

            // 타이틀: "HR" + "MAP LOTTERY" 좌우 분리
            var titleRow = CenteredRect("TitleRow", root.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -150), new Vector2(1500, 100));
            HLayoutAuto(titleRow.gameObject, 16, false, true);
            BodyText(titleRow.transform, "HR", "HR", 76, Magenta, true,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter);
            BodyText(titleRow.transform, "Lottery", "MAP LOTTERY", 76, Magenta, true,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter);

            // 3x2 grid
            var grid = NewRect("Grid", root.transform,
                new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(60, 130), new Vector2(-60, -240));
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 3;
            gl.spacing = new Vector2(26, 26);
            gl.cellSize = new Vector2((1920 - 60 - 60 - 26 * 2) / 3f, (1080 - 130 - 240 - 26) / 2f);
            gl.childAlignment = TextAnchor.MiddleCenter;

            // 6 cells
            MapCell2(grid, "HR1", false, false, false);
            MapCell2(grid, "HR2", true,  false, false);
            MapCell2(grid, "HR3", false, true,  false);
            MapCell2(grid, "HR4", false, false, true);
            MapCell2(grid, "HR5", false, false, false);
            MapCell2(grid, "HR6", true,  false, false);

            // Footer
            TopText(root.transform, "Footer",
                "PRESS [ SPACE ] TO DRAW THE MAP", 16, TextMuted, false,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 40), new Vector2(800, 30));

            return root;
        }

        private static void MapCell2(RectTransform parent, string id, bool burger, bool active, bool played)
        {
            Color border = active ? Magenta : (burger ? Yellow : Line);
            Color bg = active ? BgPanel : BgCard;

            var cell = Panel("Cell_" + id, parent.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, bg, border);

            // Head row (id-tag + mode-tag)
            var idTag = NewRect("IdTag", cell.transform,
                new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(22, -50), new Vector2(100, -22));
            idTag.gameObject.AddComponent<Image>().color = BgDarker;
            OutlineBorder(idTag.gameObject, burger ? Yellow : Cyan, 2f);
            BoxText(idTag, "T", id, 16, burger ? Yellow : Cyan);

            var modeTag = NewRect("ModeTag", cell.transform,
                new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(110, -50), new Vector2(180, -22));
            modeTag.gameObject.AddComponent<Image>().color = BgDarker;
            OutlineBorder(modeTag.gameObject, Yellow, 2f);
            BoxText(modeTag, "T", "HR", 16, Yellow);

            // Title
            BodyText(cell.transform, "Title", "Initial", 36,
                active ? Magenta : Text, true,
                new Vector2(0, 0.42f), new Vector2(1, 0.66f),
                new Vector2(28, 0), new Vector2(-28, 0),
                TextAnchor.MiddleLeft);

            // Diff line
            BodyText(cell.transform, "Diff", "EVEN NOW, I'M SEARCHING FOR YOU.", 26,
                Cyan, true,
                new Vector2(0, 0.26f), new Vector2(1, 0.42f),
                new Vector2(28, 0), new Vector2(-28, 0),
                TextAnchor.MiddleLeft);

            // Creator
            BodyText(cell.transform, "Creator", "BY  POPPIN'PARTY · CHASER01", 22,
                TextMuted, false,
                new Vector2(0, 0), new Vector2(1, 0.22f),
                new Vector2(28, 10), new Vector2(-28, 0),
                TextAnchor.MiddleLeft);

            // 🍔 emoji (burger only)
            if (burger)
            {
                BodyText(cell.transform, "Burger", "🍔", 32, Yellow, true,
                    new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-50, -50), new Vector2(-14, -14),
                    TextAnchor.MiddleCenter);
            }

            // ACTIVE 태그
            if (active)
            {
                var tag = NewRect("Spin", cell.transform,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-150, -2), new Vector2(150, 30));
                tag.gameObject.AddComponent<Image>().color = Magenta;
                BoxText(tag, "T", "▶ NOW SPINNING ◀", 14, BgScreen);
            }

            // PLAYED 오버레이
            if (played)
            {
                var cg = cell.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0.35f;

                var stamp = NewRect("Played", cell.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-150, -34), new Vector2(150, 34));
                stamp.gameObject.AddComponent<Image>().color = BgScreen;
                OutlineBorder(stamp.gameObject, TextMuted, 4f);
                BoxText(stamp, "T", "PLAYED", 38, TextMuted);
                stamp.localRotation = Quaternion.Euler(0, 0, 15);
            }
        }

        // =============================================================================
        // 6. MAP READY
        // =============================================================================
        private static GameObject BuildMapReady()
        {
            var root = ScreenRoot("MapReadyScreen");
            BuildHud(root.transform, "MAP READY", Magenta, "[HR3]  Initial — Even now, I'm searching for you.",
                     new[] { ("STAGE","3/8",Cyan), ("PRIZE","25K",Yellow), ("BURGER","x1",Magenta), ("RECORD","2-0",Green) });

            // Map Banner (top: y=-200 ~ -420)
            var banner = Panel("MapBanner", root.transform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(60, -420), new Vector2(-60, -200),
                BgPanel, Magenta);

            CapTab(banner, "Cap", "STAGE 03 / 08 ▶", Cyan, true);

            // Burger stamp (right top)
            var stamp = NewRect("BurgerStamp", banner.transform,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-260, -2), new Vector2(-60, 30));
            stamp.gameObject.AddComponent<Image>().color = Yellow;
            BoxText(stamp, "T", "🍔 JACKPOT TARGET", 15, BgScreen);

            // Left side
            BodyText(banner.transform, "Eyebrow", "▼ SELECTED MAP ▼", 15, Cyan, true,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(36, -50), new Vector2(-420, -22),
                TextAnchor.MiddleLeft);
            BodyText(banner.transform, "Title", "Initial", 72, Text, true,
                new Vector2(0, 0.30f), new Vector2(1, 0.85f),
                new Vector2(36, 0), new Vector2(-420, 0),
                TextAnchor.MiddleLeft);
            BodyText(banner.transform, "Subline",
                "ARTIST  POPPIN'PARTY  ◆  MAPPER  CHASER01  ◆  DIFF  EVEN NOW, I'M SEARCHING FOR YOU.",
                22, TextSoft, false,
                new Vector2(0, 0), new Vector2(1, 0.32f),
                new Vector2(36, 16), new Vector2(-420, 0),
                TextAnchor.MiddleLeft);

            // Right side (Mode/Boss/HP)
            var divider = NewRect("Divider", banner.transform,
                new Vector2(1, 0), new Vector2(1, 1),
                new Vector2(-380, 18), new Vector2(-378, -18));
            divider.gameObject.AddComponent<Image>().color = Magenta;

            BannerRow(banner, "MODE",  "HR",        Cyan,    0);
            BannerRow(banner, "BOSS",  "NORMAL",    Yellow,  1);
            BannerRow(banner, "HP",    "1,400,000", Magenta, 2);

            // 3 Spectators (top: y=-460 ~ -840, right: 460)
            var spec = NewRect("Spectators", root.transform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(60, -840), new Vector2(-460, -460));
            HLayoutAuto(spec.gameObject, 18, true, true);
            SpectatorSlot(spec, "P1 · YIRIRU",    "- osu! tourney -", false);
            SpectatorSlot(spec, "P2 · MOON2DOOR", "- osu! tourney -", false);
            SpectatorSlot(spec, "P3 · AXDVGN",    "- osu! tourney -", false);

            // Side panel (right) - chat
            var side = Panel("SidePanel", root.transform,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-440, -1000), new Vector2(-60, -460),
                BgPanel, Cyan);

            BodyText(side.transform, "H3", "▶ ROOM CHAT", 16, Cyan, true,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(22, -52), new Vector2(-22, -16),
                TextAnchor.MiddleLeft);
            var chatLine = NewRect("Underline", side.transform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(22, -56), new Vector2(-22, -54));
            chatLine.gameObject.AddComponent<Image>().color = Cyan;

            // Chat box
            var chat = Panel("Chat", side.transform,
                Vector2.zero, Vector2.one,
                new Vector2(22, 18), new Vector2(-22, -70),
                BgDarker, Magenta);
            var lines = new (string who, string msg, Color color)[]
            {
                ("BANCHO",    "BEATMAP CHANGED TO INITIAL [HR]", Yellow),
                ("YIRIRU",    "가보자고 ㄱㄱ",                   Magenta),
                ("MOON2DOOR", "레디",                            Magenta),
                ("LEZHIC",    "ㄱ",                              Magenta),
                ("BANCHO",    "!MP TIMER 90 — 1:30 LEFT",        Yellow),
            };
            for (int i = 0; i < lines.Length; i++)
            {
                float top = 0.96f - i * 0.18f;
                BodyText(chat.transform, "Who" + i, lines[i].who, 14,
                    lines[i].color, true,
                    new Vector2(0, top - 0.18f), new Vector2(0.30f, top),
                    new Vector2(14, 0), new Vector2(0, 0),
                    TextAnchor.MiddleLeft);
                BodyText(chat.transform, "Msg" + i, lines[i].msg, 22, TextSoft, false,
                    new Vector2(0.30f, top - 0.18f), new Vector2(1, top),
                    new Vector2(8, 0), new Vector2(-14, 0),
                    TextAnchor.MiddleLeft);
            }

            return root;
        }

        private static void BannerRow(RectTransform parent, string lbl, string val, Color valColor, int row)
        {
            // row 0,1,2 → 위에서부터
            float yTop = 1f - row * 0.33f;
            float yBot = yTop - 0.33f;

            BodyText(parent.transform, "L_" + lbl, lbl, 14, TextMuted, false,
                new Vector2(1, yBot), new Vector2(1, yTop),
                new Vector2(-370, 0), new Vector2(-270, 0),
                TextAnchor.MiddleLeft);
            BodyText(parent.transform, "V_" + lbl, val, 36, valColor, true,
                new Vector2(1, yBot), new Vector2(1, yTop),
                new Vector2(-260, 0), new Vector2(-30, 0),
                TextAnchor.MiddleRight);
        }

        private static void SpectatorSlot(RectTransform parent, string slotTag, string placeholder, bool ready)
        {
            var slot = Panel("Slot_" + slotTag, parent.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                BgDarker, ready ? Green : Line);
            slot.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var tag = NewRect("Tag", slot.transform,
                new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(16, -42), new Vector2(220, -14));
            tag.gameObject.AddComponent<Image>().color = BgScreen;
            OutlineBorder(tag.gameObject, Cyan, 2f);
            BoxText(tag, "T", slotTag, 14, Cyan);

            BodyText(slot.transform, "Ph", placeholder, 18, TextFaint, false,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter);
        }

        // =============================================================================
        // 7. INGAME
        // =============================================================================
        private static GameObject BuildInGame()
        {
            var root = ScreenRoot("InGameScreen");
            BuildHud(root.transform, "RAID LIVE", Magenta, "[HR3]  Initial — Even now, I'm searching for you.",
                     new[] { ("STAGE","3/8",Cyan), ("PRIZE","25K",Yellow), ("BURGER","x1",Magenta), ("RECORD","2-0",Green) });

            // Spectators (top: -200 ~ -800)
            var spec = NewRect("Spectators", root.transform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(60, -800), new Vector2(-60, -200));
            HLayoutAuto(spec.gameObject, 18, true, true);
            InGameSlot(spec, "P1", "YIRIRU");
            InGameSlot(spec, "P2", "MOON2DOOR");
            InGameSlot(spec, "P3", "AXDVGN");

            // Boss Bar Area (bottom: y=30, height=220)
            var bossArea = Panel("BossBarArea", root.transform,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(60, 30), new Vector2(-60, 250),
                BgPanel, Magenta);

            CapTab(bossArea, "CapL", "BOSS BATTLE ▶", Magenta, true);
            CapTab(bossArea, "CapR", "STAGE 03",     Cyan,    false);

            // Header row
            BodyText(bossArea.transform, "Skull", "☠", 50, Magenta, true,
                new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(30, -70), new Vector2(90, -18),
                TextAnchor.MiddleCenter);

            BodyText(bossArea.transform, "Meta", "BOSS HP   — NORMAL —", 24, Text, false,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(100, -70), new Vector2(-300, -18),
                TextAnchor.MiddleLeft);

            BodyText(bossArea.transform, "Dmg",
                "896,420  / 1,400,000", 36, Yellow, true,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-380, -70), new Vector2(-30, -18),
                TextAnchor.MiddleRight);

            // HP bar
            var track = Panel("Track", bossArea.transform,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(30, -154), new Vector2(-30, -90),
                BgDarker, Cyan);

            var fill = NewRect("Fill", track.transform,
                new Vector2(0, 0), new Vector2(0.64f, 1),
                Vector2.zero, Vector2.zero);
            fill.gameObject.AddComponent<Image>().color = Magenta;

            var cap = NewRect("FillCap", track.transform,
                new Vector2(0.64f, 0), new Vector2(0.64f, 1),
                new Vector2(-4, 0), Vector2.zero);
            cap.gameObject.AddComponent<Image>().color = Yellow;

            BodyText(track.transform, "Pct", "64.0%", 28, Text, true,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter);

            // Player tiles row (3개)
            var row = NewRect("PlayerRow", bossArea.transform,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(30, 16), new Vector2(-30, 64));
            HLayoutAuto(row.gameObject, 16, true, true);
            PlayerTile(row, "P1", "YIRIRU");
            PlayerTile(row, "P2", "MOON2DOOR");
            PlayerTile(row, "P3", "AXDVGN");

            return root;
        }

        private static void InGameSlot(RectTransform parent, string p, string name)
        {
            var slot = Panel("Slot_" + p, parent.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                BgDarker, Line);
            slot.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var tag = NewRect("Tag", slot.transform,
                new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(16, -42), new Vector2(80, -14));
            tag.gameObject.AddComponent<Image>().color = BgScreen;
            OutlineBorder(tag.gameObject, Cyan, 2f);
            BoxText(tag, "T", p, 14, Cyan);

            // LIVE 빨강 태그
            var live = NewRect("Live", slot.transform,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-90, -42), new Vector2(-16, -14));
            live.gameObject.AddComponent<Image>().color = Red;
            BoxText(live, "T", "● LIVE", 14, Text);

            BodyText(slot.transform, "Ph", "in-game capture", 18, TextFaint, false,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter);

            BodyText(slot.transform, "Name", name, 28, Cyan, true,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(18, 14), new Vector2(-18, 50),
                TextAnchor.MiddleLeft);
        }

        private static void PlayerTile(RectTransform parent, string idx, string name)
        {
            var tile = Panel("Tile_" + idx, parent.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                BgDarker, Line);
            tile.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // 좌측 4px 마젠타 액센트 라인
            var accent = NewRect("Accent", tile.transform,
                new Vector2(0, 0), new Vector2(0, 1),
                Vector2.zero, new Vector2(4, 0));
            accent.gameObject.AddComponent<Image>().color = Magenta;

            BodyText(tile.transform, "Idx", idx, 14, Magenta, true,
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(18, 0), new Vector2(60, 0),
                TextAnchor.MiddleCenter);

            BodyText(tile.transform, "Name", name, 24, Text, false,
                new Vector2(0, 0), new Vector2(0.65f, 1),
                new Vector2(68, 0), new Vector2(0, 0),
                TextAnchor.MiddleLeft);

            BodyText(tile.transform, "Score", "-- TOSU --", 18, TextFaint, false,
                new Vector2(0.65f, 0), new Vector2(1, 1),
                new Vector2(0, 0), new Vector2(-18, 0),
                TextAnchor.MiddleRight);
        }

        // =============================================================================
        // 8 & 9. RESULT
        // =============================================================================
        private static GameObject BuildResult(bool clear)
        {
            string name   = clear ? "SuccessResultScreen" : "FailResultScreen";
            string label  = clear ? "STAGE CLEAR" : "GAME OVER";
            Color labelColor = clear ? Green : Red;
            string verdict = clear ? "VICTORY" : "DEFEAT";
            string crest   = clear ? "★ ★ ★" : "☠ ☠ ☠";
            string message = clear ? "BOSS DEFEATED. PRIZE POOL INCREASED."
                                   : "BOSS SURVIVED. FAILURE COUNT INCREASED.";

            var root = ScreenRoot(name);
            BuildHud(root.transform, label, labelColor,
                clear ? "[HR3]  Initial · NORMAL · BURGER TARGET"
                      : "[HR3]  Initial · NORMAL",
                new[] {
                    ("STAGE","3/8",Cyan),
                    ("PRIZE", clear ? "30K" : "25K", Yellow),
                    ("BURGER","x1",Magenta),
                    ("RECORD", clear ? "3-0" : "2-1", clear ? Green : Red)
                });

            // Center mass
            BodyText(root.transform, "Crest", crest, 56, labelColor, true,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-400, -260), new Vector2(400, -200),
                TextAnchor.MiddleCenter);

            TitleText(root.transform, "Verdict", verdict, 220, labelColor, true,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -480), new Vector2(1600, 240));

            BodyTextCentered(root.transform, "Message", "> " + message, 40, Text, false,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -540), new Vector2(1700, 50));

            BodyTextCentered(root.transform, "MapInfo",
                "MAP  INITIAL  ◆  MODE  HR  ◆  BOSS  NORMAL · 1,400,000 HP",
                28, TextMuted, false,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -590), new Vector2(1700, 40));

            // Extra coin
            string extra = clear ? "+5,000₩ ADDED TO PRIZE POOL" : "INSERT COIN TO CONTINUE";
            BodyTextCentered(root.transform, "Extra", extra, 20, clear ? Yellow : Red, true,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 290), new Vector2(1000, 30),
                TextAnchor.MiddleCenter);

            // Boss bar (results)
            var bossBar = Panel("BossBar", root.transform,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(200, 320), new Vector2(-200, 410),
                BgDarker, Cyan);

            var bfill = NewRect("Fill", bossBar.transform,
                new Vector2(0, 0), new Vector2(clear ? 1f : 0.66f, 1),
                Vector2.zero, Vector2.zero);
            bfill.gameObject.AddComponent<Image>().color = clear ? Green : Red;

            BodyText(bossBar.transform, "Lbl",
                clear ? "100% · BOSS DEFEATED" : "66.0% · BOSS SURVIVED",
                28, Text, true,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter);

            // Stats row (4 stat cards)
            var stats = NewRect("Stats", root.transform,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(60, 60), new Vector2(-60, 260));
            HLayoutAuto(stats.gameObject, 24, true, true);

            StatCard(stats, "PRIZE POOL", clear ? "30,000" : "25,000",
                clear ? "KRW · +5,000" : "KRW · UNCHANGED", Yellow);
            StatCard(stats, "BURGERS",     "x1", "earned",  Magenta);
            StatCard(stats, "BURGER MISS", "x0", "missed",  Red);
            StatCard(stats, "RECORD", clear ? "3-0" : "2-1", "clear / fail",
                clear ? Green : Red);

            return root;
        }

        private static void StatCard(RectTransform parent, string lbl, string val, string unit, Color accent)
        {
            var card = Panel("Stat_" + lbl, parent.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                BgCard, accent);
            card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            BodyText(card.transform, "Lbl", lbl, 15, TextMuted, false,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -50), new Vector2(0, -22),
                TextAnchor.MiddleCenter);

            BodyText(card.transform, "Val", val, 80, accent, true,
                new Vector2(0, 0.18f), new Vector2(1, 0.82f),
                Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter);

            BodyText(card.transform, "Unit", unit, 22, TextMuted, false,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 12), new Vector2(0, 38),
                TextAnchor.MiddleCenter);
        }

        // =============================================================================
        // SHARED HUD HEADER
        // =============================================================================
        private static void BuildHud(Transform root, string screenLabel, Color labelColor,
            string mapTitle, (string lbl, string val, Color color)[] stats)
        {
            // 3-col grid: 540 / 1fr / 580
            // top=24, height=124, sides=24
            // L panel
            var l = Panel("Hud_L", root,
                new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(24, -148), new Vector2(564, -24),
                BgPanel, Cyan);
            CapTab(l, "Cap", "EVENT", Cyan, true);
            BodyText(l.transform, "Event", "BOSS RAID", 36, Magenta, true,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(20, -50), new Vector2(-20, -16),
                TextAnchor.MiddleLeft);
            // badge
            var badge = NewRect("Badge", l.transform,
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(20, 18), new Vector2(34, 50));
            badge.gameObject.AddComponent<Image>().color = Magenta;
            BodyText(l.transform, "Role", "CURRENT TEAM", 13, TextFaint, false,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(48, 40), new Vector2(-20, 60),
                TextAnchor.MiddleLeft);
            BodyText(l.transform, "Name", "삼랄부마스터", 28, Magenta, true,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(48, 14), new Vector2(-20, 40),
                TextAnchor.MiddleLeft);

            // Center panel
            var c = Panel("Hud_C", root,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(580, -148), new Vector2(-596, -24),
                BgPanel, Magenta);
            CapTab(c, "Cap", "▼ STATUS ▼", Magenta, true);
            BodyText(c.transform, "Eyebrow", "NOW SHOWING", 14, TextFaint, false,
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -42), new Vector2(0, -16),
                TextAnchor.MiddleCenter);
            BodyText(c.transform, "Label", screenLabel, 36, labelColor, true,
                new Vector2(0, 0.30f), new Vector2(1, 0.78f),
                Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter);
            BodyText(c.transform, "Map", mapTitle, 26, TextSoft, false,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(20, 12), new Vector2(-20, 38),
                TextAnchor.MiddleCenter);

            // Right panel (4 stats)
            var r = Panel("Hud_R", root,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-580, -148), new Vector2(-24, -24),
                BgPanel, Yellow);
            CapTab(r, "Cap", "SCOREBOARD", Yellow, true);
            var grid = NewRect("Grid", r.transform,
                Vector2.zero, Vector2.one,
                new Vector2(12, 8), new Vector2(-12, -8));
            HLayoutAuto(grid.gameObject, 10, true, true);
            for (int i = 0; i < stats.Length; i++)
                HudStat(grid, stats[i].lbl, stats[i].val, stats[i].color);

            // Connection
            BodyText(root, "Conn", "● BRIDGE ONLINE", 13, Green, true,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-260, -180), new Vector2(-24, -150),
                TextAnchor.MiddleRight);
        }

        private static void HudStat(RectTransform parent, string lbl, string val, Color valColor)
        {
            var tile = NewRect("Stat_" + lbl, parent.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            tile.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            tile.gameObject.AddComponent<Image>().color = BgDarker;
            OutlineBorder(tile.gameObject, Line, 2f);

            BodyText(tile.transform, "Lbl", lbl, 13, TextMuted, false,
                new Vector2(0, 0.55f), new Vector2(1, 1),
                new Vector2(0, 0), new Vector2(0, -6),
                TextAnchor.MiddleCenter);
            BodyText(tile.transform, "Val", val, 28, valColor, true,
                new Vector2(0, 0), new Vector2(1, 0.55f),
                new Vector2(0, 4), new Vector2(0, 0),
                TextAnchor.MiddleCenter);
        }

        // =============================================================================
        // BUILDERS - SCREEN ROOT
        // =============================================================================
        private static GameObject ScreenRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Design;

            // 배경
            var bg = root.AddComponent<Image>();
            bg.color = BgScreen;
            bg.raycastTarget = false;
            return root;
        }

        // =============================================================================
        // BUILDERS - PRIMITIVES
        // =============================================================================
        private static RectTransform NewRect(string name, Transform parent,
            Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
            return rt;
        }

        private static RectTransform CenteredRect(string name, Transform parent,
            Vector2 aMin, Vector2 aMax, Vector2 anchoredCenter, Vector2 sizeDelta)
        {
            return NewRect(name, parent, aMin, aMax,
                anchoredCenter - sizeDelta * 0.5f,
                anchoredCenter + sizeDelta * 0.5f);
        }

        private static RectTransform Panel(string name, Transform parent,
            Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax,
            Color fill, Color border)
        {
            var rt = NewRect(name, parent, aMin, aMax, oMin, oMax);
            rt.gameObject.AddComponent<Image>().color = fill;
            OutlineBorder(rt.gameObject, border, 2f);
            return rt;
        }

        /// <summary>
        /// 패널 보더(2px 균일). Outline 컴포넌트로 4방향 외곽선을 만든다.
        /// 더 두꺼운 시각효과가 필요하면 distance를 키운다.
        /// </summary>
        private static void OutlineBorder(GameObject go, Color color, float distance)
        {
            var ol = go.AddComponent<Outline>();
            ol.effectColor = color;
            ol.effectDistance = new Vector2(distance, -distance);
            ol.useGraphicAlpha = true;
        }

        /// <summary>
        /// 상단/타이틀 등 큰 텍스트. 단순 텍스트 + 같은색 Shadow(글로우 근사).
        /// </summary>
        private static Text TitleText(Transform parent, string name, string str,
            int size, Color color, bool glow,
            Vector2 aMin, Vector2 aMax, Vector2 anchoredCenter, Vector2 sizeDelta)
        {
            // anchoredCenter+sizeDelta 형식으로 위치/크기 지정
            var rt = CenteredRect(name, parent, aMin, aMax, anchoredCenter, sizeDelta);
            var t = rt.gameObject.AddComponent<Text>();
            t.text = str;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(10, size / 3);
            t.resizeTextMaxSize = size;
            if (glow) AddGlow(rt.gameObject, color);
            return t;
        }

        private static Text BodyTextCentered(Transform parent, string name, string str,
            int size, Color color, bool glow,
            Vector2 aMin, Vector2 aMax, Vector2 anchoredCenter, Vector2 sizeDelta,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            return BodyText(parent, name, str, size, color, glow, aMin, aMax,
                anchoredCenter - sizeDelta * 0.5f,
                anchoredCenter + sizeDelta * 0.5f,
                anchor);
        }

        /// <summary>
        /// 작은 보조 텍스트. anchoredCenter/sizeDelta 형식.
        /// </summary>
        private static Text TopText(Transform parent, string name, string str,
            int size, Color color, bool glow,
            Vector2 aMin, Vector2 aMax, Vector2 anchoredCenter, Vector2 sizeDelta)
        {
            return TitleText(parent, name, str, size, color, glow, aMin, aMax, anchoredCenter, sizeDelta);
        }

        /// <summary>
        /// 일반 본문 텍스트. anchor/offset 직접 지정. 텍스트 anchor 별도.
        /// </summary>
        private static Text BodyText(Transform parent, string name, string str,
            int size, Color color, bool glow,
            Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var rt = NewRect(name, parent, aMin, aMax, oMin, oMax);
            var t = rt.gameObject.AddComponent<Text>();
            t.text = str;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(8, size / 3);
            t.resizeTextMaxSize = size;
            if (glow) AddGlow(rt.gameObject, color);
            return t;
        }

        /// <summary>
        /// 조그만 박스 안의 중앙 텍스트(Cap, Stamp 등).
        /// </summary>
        private static Text BoxText(RectTransform parent, string name, string str, int size, Color color)
        {
            var rt = NewRect(name, parent.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var t = rt.gameObject.AddComponent<Text>();
            t.text = str;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>
        /// 패널 좌상단에 살짝 걸치는 라벨 캡(v3의 `.cap`).
        /// </summary>
        private static void CapTab(RectTransform parent, string name, string str, Color color, bool left)
        {
            var cap = NewRect(name, parent.transform,
                left ? new Vector2(0, 1) : new Vector2(1, 1),
                left ? new Vector2(0, 1) : new Vector2(1, 1),
                left ? new Vector2(16, -2) : new Vector2(-(str.Length * 10 + 24), -2),
                left ? new Vector2(16 + str.Length * 10 + 24, 18) : new Vector2(-16, 18));
            cap.gameObject.AddComponent<Image>().color = BgScreen;
            BoxText(cap, "T", str, 13, color);
        }

        /// <summary>
        /// 텍스트 글로우: 동일색 Outline 1단(부드러운 효과 흉내).
        /// </summary>
        private static void AddGlow(GameObject go, Color color)
        {
            var glow = go.AddComponent<Outline>();
            var c = color;
            c.a = 0.55f;
            glow.effectColor = c;
            glow.effectDistance = new Vector2(1.5f, -1.5f);
            glow.useGraphicAlpha = true;
        }

        // =============================================================================
        // LAYOUT HELPERS
        // =============================================================================
        private static void HLayoutAuto(GameObject go, float spacing, bool ctrlW, bool ctrlH)
        {
            var l = go.AddComponent<HorizontalLayoutGroup>();
            l.spacing = spacing;
            l.childForceExpandWidth = ctrlW;
            l.childForceExpandHeight = ctrlH;
            l.childControlWidth = ctrlW;
            l.childControlHeight = ctrlH;
            l.childAlignment = TextAnchor.MiddleCenter;
        }

        private static void VLayoutAuto(GameObject go, float spacing, bool ctrlW, bool ctrlH)
        {
            var l = go.AddComponent<VerticalLayoutGroup>();
            l.spacing = spacing;
            l.childForceExpandWidth = ctrlW;
            l.childForceExpandHeight = ctrlH;
            l.childControlWidth = ctrlW;
            l.childControlHeight = ctrlH;
            l.childAlignment = TextAnchor.MiddleCenter;
        }

        // =============================================================================
        // UTILITIES
        // =============================================================================
        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out var c)) return c;
            return Color.magenta;
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
