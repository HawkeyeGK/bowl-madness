using System;
using System.Collections.Generic;

namespace BowlPoolManager.Core.Dtos
{
    public class MigrationAnalysisRequest
    {
        public string SourceSeasonId { get; set; } = default!;
    }

    public class MigrationAnalysisResult
    {
        public List<LegacyGameDto> LegacyGames { get; set; } = new();
        public List<string> LegacyTeamNames { get; set; } = new();
        public List<string> LegacySeasonIds { get; set; } = new(); // NEW: Seasons found
        public List<LegacyPoolDto> LegacyPools { get; set; } = new(); // NEW: Pools found (Id + Season)
        public string DebugInfo { get; set; } = ""; // NEW: Raw JSON of first item for debugging
        public int LegacyEntryCount { get; set; }
    }

    public class LegacyGameDto
    {
        public string Id { get; set; } = default!;
        public string Description { get; set; } = default!;
        public string HomeTeam { get; set; } = default!;
        public string AwayTeam { get; set; } = default!;
        public DateTime StartTime { get; set; }
        public string SeasonId { get; set; } = default!; // NEW
    }

    public class LegacyPoolDto
    {
        public string PoolId { get; set; } = default!;
        public string PoolName { get; set; } = default!; // NEW: For display (captures legacy 'season' property)
        public string SeasonId { get; set; } = default!;
    }

    public class MigrationExecutionRequest
    {
        // Defaults/Globals (Optional fallback)
        public string TargetPoolId { get; set; } = default!;
        public string TargetSeasonId { get; set; } = default!;
        
        // Mappings
        public Dictionary<string, string> SeasonMapping { get; set; } = new(); // LegacySeasonId -> TargetSeasonId
        public Dictionary<string, string> PoolMapping { get; set; } = new();   // LegacyPoolId -> TargetPoolId
        public Dictionary<string, string> GameMapping { get; set; } = new();   // LegacyGameId -> NewGameId
        public Dictionary<string, string> TeamMapping { get; set; } = new();   // OldTeamName -> NewTeamName
    }
}
