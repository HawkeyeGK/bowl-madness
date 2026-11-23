using BowlPoolManager.Core.Dtos;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Services.ScoreFeed
{
    /// <summary>Converts between the College Football Data DTO (also the browser wire type) and the shared feed types.</summary>
    public static class CfbdFeedMapping
    {
        public static FeedGame ToFeedGame(CfbdGameDto dto) => new()
        {
            ExternalId = dto.Id.ToString(),
            Status = ToStatus(dto),
            Period = dto.Period,
            Clock = dto.Clock,
            Home = new FeedTeam { Name = dto.HomeTeam, Id = dto.HomeId, Points = dto.HomePoints },
            Away = new FeedTeam { Name = dto.AwayTeam, Id = dto.AwayId, Points = dto.AwayPoints },
            Notes = dto.Notes
        };

        // Same rule the scoring service used on the DTO: Completed or "completed" is final, "in_progress" is live.
        public static FeedGameStatus ToStatus(CfbdGameDto dto)
        {
            if (dto.Completed || string.Equals(dto.StatusRaw, "completed", StringComparison.OrdinalIgnoreCase))
                return FeedGameStatus.Final;
            if (string.Equals(dto.StatusRaw, "in_progress", StringComparison.OrdinalIgnoreCase))
                return FeedGameStatus.InProgress;
            return FeedGameStatus.Scheduled;
        }

        public static CfbdGameDto ToCfbdDto(FeedGame game) => new()
        {
            Id = int.TryParse(game.ExternalId, out var id) ? id : 0,
            Completed = game.Status == FeedGameStatus.Final,
            StatusRaw = game.Status switch
            {
                FeedGameStatus.Final => "completed",
                FeedGameStatus.InProgress => "in_progress",
                _ => "scheduled"
            },
            Period = game.Period,
            Clock = game.Clock,
            HomeRaw = TeamDictionary(game.Home),
            HomePointsRoot = game.Home.Points,
            AwayRaw = TeamDictionary(game.Away),
            AwayPointsRoot = game.Away.Points,
            Notes = game.Notes
        };

        private static Dictionary<string, object> TeamDictionary(FeedTeam team)
        {
            var d = new Dictionary<string, object>();
            if (team.Name != null) d["name"] = team.Name;
            if (team.Id.HasValue) d["id"] = team.Id.Value;
            if (team.Points.HasValue) d["points"] = team.Points.Value;
            return d;
        }
    }
}
