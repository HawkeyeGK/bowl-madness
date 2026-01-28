using System;
using System.Collections.Generic;

namespace BowlPoolManager.Core.Dtos
{
    public class MigrationAnalysisRequest
    {
        public string SourceSeasonId { get; set; }
    }

    public class MigrationAnalysisResult
    {
        public List<LegacyGameDto> LegacyGames { get; set; } = new();
        public List<string> LegacyTeamNames { get; set; } = new();
        public List<string> LegacyPoolIds { get; set; } = new(); // NEW
        public int LegacyEntryCount { get; set; }
    }

    public class LegacyGameDto
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }
        public DateTime StartTime { get; set; }
    }

    public class MigrationExecutionRequest
    {
        public string TargetPoolId { get; set; }
        public string TargetSeasonId { get; set; }
        public Dictionary<string, string> GameMapping { get; set; } = new(); // OldGameId -> NewGameId
        public Dictionary<string, string> TeamMapping { get; set; } = new(); // OldTeamName -> NewTeamName
        public Dictionary<string, string> PoolMapping { get; set; } = new(); // LegacyPoolId -> NewPoolId
    }
}
