using System.Collections.Generic;

namespace BowlPoolManager.Core.Domain
{
    public class LeaderboardRow
    {
        public int Rank { get; set; }
        public BracketEntry Entry { get; set; } = new();
        public int Score { get; set; }
        public int MaxPossible { get; set; }
        public int CorrectPicks { get; set; }
        public int? TieBreakerDelta { get; set; } // Lower is better
        public Dictionary<PlayoffRound, int> RoundScores { get; set; } = new();
    }
}
