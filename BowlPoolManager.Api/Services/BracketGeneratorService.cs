using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Services
{
    /// <summary>
    /// Generates a complete 67-game NCAA tournament bracket with correct NextGameId wiring.
    ///
    /// Structure per region (16 teams):
    ///   Round of 64 — 8 games (slots: 1v16, 8v9, 5v12, 4v13, 6v11, 3v14, 7v10, 2v15)
    ///   Round of 32 — 4 games
    ///   Sweet 16    — 2 games
    ///   Elite 8     — 1 game (regional final)
    ///
    /// Cross-region:
    ///   Final Four  — 2 games  (based on admin-supplied pairings)
    ///   Championship — 1 game
    ///
    /// First Four (4 play-in games, explicitly configured):
    ///   Each entry in request.FirstFourGames specifies the region and seed the winner assumes.
    ///   The generator wires the play-in game to the R64 slot whose SeedMatchup contains that seed.
    ///   Home seed = lower number (better team); Away seed = higher number in each R64 matchup.
    ///   Seeds on R64 and First Four games are auto-populated during generation.
    ///
    /// Total: 4 + 32 + 16 + 8 + 4 + 2 + 1 = 67 games.
    /// </summary>
    public class BracketGeneratorService : IBracketGeneratorService
    {
        // R64 slot index → R32 bucket (two slots share each R32 game)
        private static readonly int[] R32Map = { 0, 0, 1, 1, 2, 2, 3, 3 };

        // SeedMatchup labels in slot order for R64 games (home seed v away seed)
        private static readonly string[] R64SeedMatchups =
            { "1v16", "8v9", "5v12", "4v13", "6v11", "3v14", "7v10", "2v15" };

        // Maps any seed number (1–16) to the R64 SeedMatchup it belongs to
        private static readonly Dictionary<int, string> SeedToR64Matchup = new()
        {
            { 1, "1v16" }, { 16, "1v16" },
            { 8, "8v9"  }, { 9,  "8v9"  },
            { 5, "5v12" }, { 12, "5v12" },
            { 4, "4v13" }, { 13, "4v13" },
            { 6, "6v11" }, { 11, "6v11" },
            { 3, "3v14" }, { 14, "3v14" },
            { 7, "7v10" }, { 10, "7v10" },
            { 2, "2v15" }, { 15, "2v15" },
        };

        public List<HoopsGame> GenerateBracket(BracketGenerationRequest request)
        {
            Validate(request);

            var games = new List<HoopsGame>(67);
            var sid = request.SeasonId;

            // ── 1. National Championship ──────────────────────────────────────────
            var championship = Make(sid, TournamentRound.NationalChampionship, null, null);
            games.Add(championship);

            // ── 2. Final Four (2 games) ───────────────────────────────────────────
            var ff = new[] {
                Make(sid, TournamentRound.FinalFour, null, championship.Id),
                Make(sid, TournamentRound.FinalFour, null, championship.Id)
            };
            games.AddRange(ff);

            // Map each region name → its Final Four game
            var regionToFF = new Dictionary<string, HoopsGame>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < 2; i++)
                foreach (var r in request.FinalFourPairings[i])
                    regionToFF[r] = ff[i];

            // ── 3. Elite 8 (1 per region) ─────────────────────────────────────────
            var e8 = new Dictionary<string, HoopsGame>(StringComparer.OrdinalIgnoreCase);
            foreach (var region in request.Regions)
            {
                var g = Make(sid, TournamentRound.Elite8, region, regionToFF[region].Id);
                games.Add(g);
                e8[region] = g;
            }

            // ── 4. Sweet 16 (2 per region) ───────────────────────────────────────
            // [0] = top half, [1] = bottom half
            var s16 = new Dictionary<string, HoopsGame[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var region in request.Regions)
            {
                var top = Make(sid, TournamentRound.Sweet16, region, e8[region].Id);
                var bot = Make(sid, TournamentRound.Sweet16, region, e8[region].Id);
                games.Add(top);
                games.Add(bot);
                s16[region] = new[] { top, bot };
            }

            // ── 5. Round of 32 (4 per region) ────────────────────────────────────
            // [0,1] → S16 top half;  [2,3] → S16 bottom half
            var r32 = new Dictionary<string, HoopsGame[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var region in request.Regions)
            {
                var arr = new HoopsGame[4];
                arr[0] = Make(sid, TournamentRound.RoundOf32, region, s16[region][0].Id);
                arr[1] = Make(sid, TournamentRound.RoundOf32, region, s16[region][0].Id);
                arr[2] = Make(sid, TournamentRound.RoundOf32, region, s16[region][1].Id);
                arr[3] = Make(sid, TournamentRound.RoundOf32, region, s16[region][1].Id);
                games.AddRange(arr);
                r32[region] = arr;
            }

            // ── 6. Round of 64 (8 per region) ────────────────────────────────────
            // Slot order: 1v16, 8v9, 5v12, 4v13, 6v11, 3v14, 7v10, 2v15
            // Home seed = lower number (better team); Away seed = higher number.
            var r64 = new Dictionary<string, HoopsGame[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var region in request.Regions)
            {
                var arr = new HoopsGame[8];
                for (int i = 0; i < 8; i++)
                {
                    arr[i] = Make(sid, TournamentRound.RoundOf64, region, r32[region][R32Map[i]].Id);
                    arr[i].SeedMatchup = R64SeedMatchups[i];

                    // Auto-set seeds: "XvY" → Home=X (better/lower number), Away=Y (weaker/higher number)
                    var parts = R64SeedMatchups[i].Split('v');
                    arr[i].TeamHomeSeed = int.Parse(parts[0]);
                    arr[i].TeamAwaySeed = int.Parse(parts[1]);
                }
                games.AddRange(arr);
                r64[region] = arr;
            }

            // ── 7. First Four (4 play-in games, explicitly configured) ────────────
            // Each entry specifies the region and seed the winner assumes in R64.
            // Wire to the R64 game in that region whose SeedMatchup contains the seed.
            foreach (var entry in request.FirstFourGames)
            {
                var targetMatchup = SeedToR64Matchup[entry.Seed];
                var targetR64 = r64[entry.Region].First(g =>
                    string.Equals(g.SeedMatchup, targetMatchup, StringComparison.OrdinalIgnoreCase));

                var g = Make(sid, TournamentRound.FirstFour, entry.Region, targetR64.Id);
                g.SeedMatchup = $"{entry.Seed}v{entry.Seed}";
                g.TeamHomeSeed = entry.Seed;
                g.TeamAwaySeed = entry.Seed;
                games.Add(g);
            }

            return games; // exactly 67
        }

        private static void Validate(BracketGenerationRequest req)
        {
            if (req.Regions == null || req.Regions.Count != 4)
                throw new ArgumentException("Exactly 4 regions are required.");

            if (req.FinalFourPairings == null ||
                req.FinalFourPairings.Count != 2 ||
                req.FinalFourPairings[0]?.Count != 2 ||
                req.FinalFourPairings[1]?.Count != 2)
                throw new ArgumentException("Exactly 2 Final Four pairings, each containing 2 regions, are required.");

            var pairedRegions = req.FinalFourPairings.SelectMany(p => p).ToList();

            if (pairedRegions.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
                throw new ArgumentException("Each region must appear in exactly one Final Four pairing.");

            var allRegions = req.Regions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var r in pairedRegions)
            {
                if (!allRegions.Contains(r))
                    throw new ArgumentException($"Region '{r}' in Final Four pairings is not in the Regions list.");
            }

            if (req.FirstFourGames == null || req.FirstFourGames.Count != 4)
                throw new ArgumentException("Exactly 4 First Four play-in games are required.");

            foreach (var entry in req.FirstFourGames)
            {
                if (!SeedToR64Matchup.ContainsKey(entry.Seed))
                    throw new ArgumentException($"Seed {entry.Seed} is not a valid tournament seed (must be 1–16).");

                if (!allRegions.Contains(entry.Region))
                    throw new ArgumentException($"First Four region '{entry.Region}' is not in the Regions list.");
            }

            var firstFourKeys = req.FirstFourGames
                .Select(f => $"{f.Region.ToUpperInvariant()}:{f.Seed}")
                .ToList();
            if (firstFourKeys.Distinct().Count() != 4)
                throw new ArgumentException("Each First Four play-in game must have a unique region/seed combination.");
        }

        private static HoopsGame Make(string seasonId, TournamentRound round, string? region, string? nextGameId) =>
            new HoopsGame
            {
                Id = Guid.NewGuid().ToString(),
                SeasonId = seasonId,
                Round = round,
                Region = region,
                NextGameId = nextGameId,
                Status = GameStatus.Scheduled,
                PointValue = 0
            };
    }
}
