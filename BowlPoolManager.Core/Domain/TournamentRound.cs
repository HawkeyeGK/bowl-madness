namespace BowlPoolManager.Core.Domain
{
    public enum TournamentRound
    {
        // Football (CFP) rounds — values match legacy PlayoffRound for Cosmos compatibility
        Standard = 0,
        Round1 = 1,
        QuarterFinal = 2,
        SemiFinal = 3,
        Championship = 4,

        // Basketball (NCAA Tournament) rounds
        FirstFour = 10,
        RoundOf64 = 11,
        RoundOf32 = 12,
        Sweet16 = 13,
        Elite8 = 14,
        FinalFour = 15,
        NationalChampionship = 16
    }
}
