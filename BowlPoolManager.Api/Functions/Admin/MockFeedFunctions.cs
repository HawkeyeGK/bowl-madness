using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Functions.Admin
{
    /// <summary>
    /// Admin tools for the mock score feed. They work on any site, so every endpoint checks the caller (SuperAdmin)
    /// on the server first, before the input is read and before anything is read or written.
    /// </summary>
    public class MockFeedFunctions
    {
        private const int MaxNameLength = 100;
        private const int MaxNotesLength = 200;
        private const int MaxScore = 200;

        private readonly ILogger _logger;
        private readonly IUserRepository _userRepo;
        private readonly IMockFeedStore _store;
        private readonly IMockFeedStartingSet _startingSet;
        private readonly IScoreFeedSettingsStore _settings;
        private readonly IMockFeedResetService _reset;

        public MockFeedFunctions(
            ILoggerFactory loggerFactory,
            IUserRepository userRepo,
            IMockFeedStore store,
            IMockFeedStartingSet startingSet,
            IScoreFeedSettingsStore settings,
            IMockFeedResetService reset)
        {
            _logger = loggerFactory.CreateLogger<MockFeedFunctions>();
            _userRepo = userRepo;
            _store = store;
            _startingSet = startingSet;
            _settings = settings;
            _reset = reset;
        }

        // ── Mode ─────────────────────────────────────────────────────────────

        [Function("GetMockFeedMode")]
        public async Task<HttpResponseData> GetMockFeedMode(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "MockFeed/Mode")] HttpRequestData req)
        {
            var refused = await CheckAsync(req);
            if (refused != null) return refused;

            var mode = await _settings.GetModeAsync();
            return await JsonResponseAsync(req, new { mode = mode.ToString() });
        }

        // Body: { "mode": "Live" | "Mock", "resetConfirm": <games shown in the preview> (optional) }.
        // resetConfirm is only valid when switching from Mock to Live: the stored mode must be Mock, the preview
        // count must still match, then the mode goes to Live FIRST (so no refresh applies mock scores) and the
        // games are reset. There is no stand-alone reset: it cannot tell mock results from real ones, so it only
        // runs as part of leaving Mock.
        [Function("SetMockFeedMode")]
        public async Task<HttpResponseData> SetMockFeedMode(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "MockFeed/Mode")] HttpRequestData req)
        {
            var refused = await CheckAsync(req);
            if (refused != null) return refused;

            var body = await ReadModeBodyAsync(req);
            if (body == null) return await TextResponseAsync(req, HttpStatusCode.BadRequest, "Mode must be Live or Mock, and resetConfirm a whole number.");
            var (mode, resetConfirm) = body.Value;

            if (resetConfirm == null)
            {
                await _settings.SetModeAsync(mode);
                _logger.LogInformation("Score feed mode set to {Mode}.", mode);
                return await JsonResponseAsync(req, new { mode = mode.ToString() });
            }

            if (mode != ScoreFeedMode.Live)
                return await TextResponseAsync(req, HttpStatusCode.BadRequest, "A reset only goes with switching to Live.");

            if (await _settings.GetModeAsync() != ScoreFeedMode.Mock)
                return await TextResponseAsync(req, HttpStatusCode.Conflict, "A reset is only allowed while the mock feed is on.");

            var preview = await _reset.PreviewAsync();
            if (preview.Status == MockFeedResetStatus.NoSeason)
                return await TextResponseAsync(req, HttpStatusCode.BadRequest, "No current season is set.");
            if (preview.Summary!.GamesToReset != resetConfirm.Value)
                return await TextResponseAsync(req, HttpStatusCode.Conflict, "The number of games changed since the preview. Preview again. Nothing was changed.");

            await _settings.SetModeAsync(ScoreFeedMode.Live);
            _logger.LogInformation("Score feed mode set to Live with a reset of {Count} games.", resetConfirm.Value);

            MockFeedResetResult result;
            try
            {
                result = await _reset.ApplyAsync(resetConfirm.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mock feed reset failed at {FailedUtc} after the feed was set to Live; the reset may be incomplete.", DateTime.UtcNow);
                return await TextResponseAsync(req, HttpStatusCode.InternalServerError,
                    "The feed is now Live, but the reset did not finish. Some games may be reset. Switch to Mock again, then back to Live with a reset.");
            }

            if (result.Status != MockFeedResetStatus.Ok)
                return await TextResponseAsync(req, HttpStatusCode.Conflict, "The feed is now Live, but the reset did not run because the games changed. Nothing was reset.");

            return await JsonResponseAsync(req, new { mode = "Live", reset = result.Summary });
        }

        // Exactly "Live" or "Mock"; resetConfirm, when present, a whole number of 0 or more. Anything else, a missing
        // body or bad JSON gives null.
        private static async Task<(ScoreFeedMode Mode, int? ResetConfirm)?> ReadModeBodyAsync(HttpRequestData req)
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(req.Body);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("mode", out var value) ||
                    value.ValueKind != JsonValueKind.String) return null;

                ScoreFeedMode mode;
                switch (value.GetString())
                {
                    case "Live": mode = ScoreFeedMode.Live; break;
                    case "Mock": mode = ScoreFeedMode.Mock; break;
                    default: return null;
                }

                int? confirm = null;
                if (root.TryGetProperty("resetConfirm", out var c) && c.ValueKind != JsonValueKind.Null)
                {
                    if (c.ValueKind != JsonValueKind.Number || !c.TryGetInt32(out var n) || n < 0) return null;
                    confirm = n;
                }
                return (mode, confirm);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // ── Reset preview (read only; the reset itself runs inside SetMockFeedMode) ──

        [Function("PreviewMockFeedReset")]
        public async Task<HttpResponseData> PreviewMockFeedReset(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "MockFeed/ResetPreview")] HttpRequestData req)
        {
            var refused = await CheckAsync(req);
            if (refused != null) return refused;

            var result = await _reset.PreviewAsync();
            return await ResetResponseAsync(req, result);
        }

        private static async Task<HttpResponseData> ResetResponseAsync(HttpRequestData req, MockFeedResetResult result) => result.Status switch
        {
            MockFeedResetStatus.NoSeason => await TextResponseAsync(req, HttpStatusCode.BadRequest, "No current season is set."),
            _ => await JsonResponseAsync(req, result.Summary!)
        };

        // ── Games ────────────────────────────────────────────────────────────

        [Function("GetMockFeedGames")]
        public async Task<HttpResponseData> GetMockFeedGames(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "MockFeed/Games")] HttpRequestData req)
        {
            var refused = await CheckAsync(req);
            if (refused != null) return refused;

            var games = (await _store.GetAllAsync()).Select(m => m.Game).OrderBy(g => g.ExternalId, StringComparer.Ordinal).ToList();
            return await JsonResponseAsync(req, games);
        }

        [Function("SaveMockFeedGame")]
        public async Task<HttpResponseData> SaveMockFeedGame(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "MockFeed/Games")] HttpRequestData req)
        {
            var refused = await CheckAsync(req);
            if (refused != null) return refused;

            FeedGame? game;
            try { game = await JsonSerializer.DeserializeAsync<FeedGame>(req.Body); }
            catch (JsonException) { game = null; }

            var problem = Validate(game);
            if (problem != null) return await TextResponseAsync(req, HttpStatusCode.BadRequest, problem);

            await _store.UpsertAsync(new MockFeedGame { Game = game! });
            _logger.LogInformation("Mock feed game {ExternalId} saved.", game!.ExternalId);
            return await JsonResponseAsync(req, game);
        }

        [Function("DeleteMockFeedGame")]
        public async Task<HttpResponseData> DeleteMockFeedGame(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "MockFeed/Games/{externalId}")] HttpRequestData req,
            string externalId)
        {
            var refused = await CheckAsync(req);
            if (refused != null) return refused;

            if (string.IsNullOrWhiteSpace(externalId))
                return await TextResponseAsync(req, HttpStatusCode.BadRequest, "Game id is required.");

            await _store.DeleteAsync(externalId);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [Function("LoadMockFeedStartingSet")]
        public async Task<HttpResponseData> LoadMockFeedStartingSet(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "MockFeed/StartingSet")] HttpRequestData req)
        {
            var refused = await CheckAsync(req);
            if (refused != null) return refused;

            var games = _startingSet.Load();
            var stored = await _store.ReplaceAllAsync(games);
            _logger.LogInformation("Mock feed starting set loaded: {Count} games.", stored);
            return await JsonResponseAsync(req, new { loaded = stored });
        }

        // The caller check, on the server, first. Returns the refusal, or null when the caller is a SuperAdmin.
        private async Task<HttpResponseData?> CheckAsync(HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            return authResult.IsValid ? null : authResult.ErrorResponse!;
        }

        // Deliberately permissive about scores and names: the page exists to create bad feed data (a Final with
        // no scores, a tie, unmatched names) for issue 133. It only bounds sizes and requires an id.
        public static string? Validate(FeedGame? game)
        {
            if (game == null) return "A game is required.";
            if (string.IsNullOrWhiteSpace(game.ExternalId) || game.ExternalId.Length > 20) return "Game id is required (20 characters at most).";
            // The id becomes the Cosmos document id, which cannot hold / \ ? # and the like.
            if (!game.ExternalId.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_')) return "Game id may only use letters, digits, - and _.";
            if (game.Home == null || game.Away == null) return "Home and away teams are required.";
            if (!Enum.IsDefined(game.Status)) return "Unknown status.";
            if ((game.Home.Name?.Length ?? 0) > MaxNameLength || (game.Away.Name?.Length ?? 0) > MaxNameLength) return "Team name is too long.";
            if ((game.Notes?.Length ?? 0) > MaxNotesLength) return "Notes are too long.";
            if ((game.Clock?.Length ?? 0) > 10) return "Clock is too long.";
            if (game.Period is < 0 or > 20) return "Period must be 0 to 20.";
            foreach (var points in new[] { game.Home.Points, game.Away.Points })
                if (points is < 0 or > MaxScore) return $"Scores must be 0 to {MaxScore}.";
            return null;
        }

        private static async Task<HttpResponseData> TextResponseAsync(HttpRequestData req, HttpStatusCode status, string text)
        {
            var response = req.CreateResponse(status);
            await response.WriteStringAsync(text);
            return response;
        }

        private static async Task<HttpResponseData> JsonResponseAsync(HttpRequestData req, object body)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(body));
            return response;
        }
    }
}
