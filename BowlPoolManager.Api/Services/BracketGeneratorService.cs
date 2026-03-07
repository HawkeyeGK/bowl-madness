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
    /// First Four (4 play-in games, 1 per region):
    ///   Pairing[0][0] and Pairing[0][1] each get a 16-seed play-in (feeds the 1v16 slot)
    ///   Pairing[1][0] and Pairing[1][1] each get an 11-seed play-in (feeds the 6v11 slot)
    ///
    /// Total: 4 + 32 + 16 + 8 + 4 + 2 + 1 = 67 games.
    /// </summary>
    public class BracketGeneratorService : IBracketGeneratorService
    {
        // R64 slot index → R32 bucket (two slots share each R32 game)
        private static readonly int[] R32Map = { 0, 0, 1, 1, 2, 2, 3, 3 };

        // R64 slot indexes used for First Four play-in targets
        private const int Slot16Seed = 0; // 1 vs 16
        private const int Slot11Seed = 4; // 6 vs 11

        // SeedMatchup labels in slot order for R64 games
        private static readonly string[] R64SeedMatchups =
            { "1v16", "8v9", "5v12", "4v13", "6v11", "3v14", "7v10", "2v15" };

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
            var r64 = new Dictionary<string, HoopsGame[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var region in request.Regions)
            {
                var arr = new HoopsGame[8];
                for (int i = 0; i < 8; i++)
                {
                    arr[i] = Make(sid, TournamentRound.RoundOf64, region, r32[region][R32Map[i]].Id);
                    arr[i].SeedMatchup = R64SeedMatchups[i];
                }
                games.AddRange(arr);
                r64[region] = arr;
            }

            // ── 7. First Four (4 play-in games) ──────────────────────────────────
            // Pairing[0] regions each get a 16-seed play-in → feeds that region's 1v16 slot
            // Pairing[1] regions each get an 11-seed play-in → feeds that region's 6v11 slot
            var firstFourSlots = new[]
            {
                (Region: request.FinalFourPairings[0][0], R64Slot: Slot16Seed, Matchup: "16v16"),
                (Region: request.FinalFourPairings[0][1], R64Slot: Slot16Seed, Matchup: "16v16"),
                (Region: request.FinalFourPairings[1][0], R64Slot: Slot11Seed, Matchup: "11v11"),
                (Region: request.FinalFourPairings[1][1], R64Slot: Slot11Seed, Matchup: "11v11"),
            };
            foreach (var (region, slot, matchup) in firstFourSlots)
            {
                var g = Make(sid, TournamentRound.FirstFour, region, r64[region][slot].Id);
                g.SeedMatchup = matchup;
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

            var pairedRegions = req.FinalFourPairings.SelectMany(p => p)
                .ToList();

            if (pairedRegions.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
                throw new ArgumentException("Each region must appear in exactly one Final Four pairing.");

            var allRegions = req.Regions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var r in pairedRegions)
            {
                if (!allRegions.Contains(r))
                    throw new ArgumentException($"Region '{r}' in Final Four pairings is not in the Regions list.");
            }
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
