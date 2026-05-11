using UnityEngine;

namespace BossRaid
{
    public enum BossRaidUiColorRole
    {
        None,
        White,
        Muted,
        Red,
        Green,
        Cyan,
        Gold,
        CurrentTeam,
        Team,
        Screen,
        Difficulty,
        Result,
        Context
    }

    public enum BossRaidUiBindingSource
    {
        None,
        Team,
        Mode,
        Difficulty,
        SelectedModeMap,
        BurgerMap,
        ResultStat,
        Spectator
    }

    public sealed class BossRaidUiBinding : MonoBehaviour
    {
        public string key;
        public BossRaidUiBindingSource source = BossRaidUiBindingSource.None;
        public int index = -1;
        public BossRaidUiColorRole colorRole = BossRaidUiColorRole.None;
        public bool hideWhenEmpty;
    }

}
