using FluentAssertions;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Tests.Core
{
    public class BracketGeneratorTests
    {
        // ── Shared fixture ─────────────────────────────────────────────────────

        private static BracketGenerationRequest StandardRequest() => new()
        {
            PoolId = "pool-1",
            SeasonId = "season-2026",
            Regions = new List<string> { "East", "West", "South", "Midwest" },
            FinalFourPairings = new List<List<string>>
            {
                new() { "East", "West" },
                new() { "South", "Midwest" }
            }
        };

        private static BracketGeneratorService Sut() => new();

        // ── Total game count ────────────────────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldReturnExactly67Games_ForValidRequest()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Should().HaveCount(67);
        }

        // ── Per-round game counts ──────────────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldReturnExactly1Championship()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Count(g => g.Round == TournamentRound.NationalChampionship).Should().Be(1);
        }

        [Fact]
        public void GenerateBracket_ShouldReturnExactly2FinalFourGames()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Count(g => g.Round == TournamentRound.FinalFour).Should().Be(2);
        }

        [Fact]
        public void GenerateBracket_ShouldReturnExactly4Elite8Games()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Count(g => g.Round == TournamentRound.Elite8).Should().Be(4);
        }

        [Fact]
        public void GenerateBracket_ShouldReturnExactly8Sweet16Games()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Count(g => g.Round == TournamentRound.Sweet16).Should().Be(8);
        }

        [Fact]
        public void GenerateBracket_ShouldReturnExactly16RoundOf32Games()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Count(g => g.Round == TournamentRound.RoundOf32).Should().Be(16);
        }

        [Fact]
        public void GenerateBracket_ShouldReturnExactly32RoundOf64Games()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Count(g => g.Round == TournamentRound.RoundOf64).Should().Be(32);
        }

        [Fact]
        public void GenerateBracket_ShouldReturnExactly4FirstFourGames()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Count(g => g.Round == TournamentRound.FirstFour).Should().Be(4);
        }

        // ── Game identity ──────────────────────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldAssignUniqueIds_ToEveryGame()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Select(g => g.Id).Distinct().Should().HaveCount(67);
        }

        [Fact]
        public void GenerateBracket_ShouldSetSeasonId_OnEveryGame()
        {
            var request = StandardRequest();
            var result = Sut().GenerateBracket(request);

            result.Should().OnlyContain(g => g.SeasonId == "season-2026");
        }

        [Fact]
        public void GenerateBracket_ShouldSetStatusToScheduled_OnEveryGame()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Should().OnlyContain(g => g.Status == GameStatus.Scheduled);
        }

        [Fact]
        public void GenerateBracket_ShouldSetPointValueToZero_OnEveryGame()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Should().OnlyContain(g => g.PointValue == 0);
        }

        // ── Championship NextGameId ─────────────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldSetChampionshipNextGameIdToNull()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            var championship = result.Single(g => g.Round == TournamentRound.NationalChampionship);
            championship.NextGameId.Should().BeNull();
        }

        // ── NextGameId wiring — every non-championship game has a valid target ──

        [Fact]
        public void GenerateBracket_ShouldWireNextGameId_ForEveryNonChampionshipGame()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var ids = result.Select(g => g.Id).ToHashSet();

            var nonChampionship = result.Where(g => g.Round != TournamentRound.NationalChampionship);
            nonChampionship.Should().OnlyContain(g => g.NextGameId != null && ids.Contains(g.NextGameId!));
        }

        // ── FinalFour → Championship wiring ────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldWireBothFinalFourGamesToChampionship()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            var championship = result.Single(g => g.Round == TournamentRound.NationalChampionship);
            var ffGames = result.Where(g => g.Round == TournamentRound.FinalFour).ToList();

            ffGames.Should().HaveCount(2);
            ffGames.Should().OnlyContain(g => g.NextGameId == championship.Id);
        }

        // ── Elite 8 → FinalFour pairing wiring ─────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldWireElite8ToCorrectFinalFourGame_ForPairing0()
        {
            var request = StandardRequest();
            var result = Sut().GenerateBracket(request);

            // pairing[0] = East, West → must both point to the same FF game (ff[0])
            var ffGames = result.Where(g => g.Round == TournamentRound.FinalFour).ToList();
            var e8East = result.Single(g => g.Round == TournamentRound.Elite8 && g.Region == "East");
            var e8West = result.Single(g => g.Round == TournamentRound.Elite8 && g.Region == "West");

            e8East.NextGameId.Should().Be(e8West.NextGameId);
            ffGames.Should().Contain(g => g.Id == e8East.NextGameId);
        }

        [Fact]
        public void GenerateBracket_ShouldWireElite8ToCorrectFinalFourGame_ForPairing1()
        {
            var request = StandardRequest();
            var result = Sut().GenerateBracket(request);

            // pairing[1] = South, Midwest → must both point to the same FF game (ff[1])
            var ffGames = result.Where(g => g.Round == TournamentRound.FinalFour).ToList();
            var e8South = result.Single(g => g.Round == TournamentRound.Elite8 && g.Region == "South");
            var e8Midwest = result.Single(g => g.Round == TournamentRound.Elite8 && g.Region == "Midwest");

            e8South.NextGameId.Should().Be(e8Midwest.NextGameId);
            ffGames.Should().Contain(g => g.Id == e8South.NextGameId);
        }

        [Fact]
        public void GenerateBracket_ShouldWireElite8ToDifferentFinalFourGames_ForDifferentPairings()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            var e8East = result.Single(g => g.Round == TournamentRound.Elite8 && g.Region == "East");
            var e8South = result.Single(g => g.Round == TournamentRound.Elite8 && g.Region == "South");

            // The two pairings point to different FF games
            e8East.NextGameId.Should().NotBe(e8South.NextGameId);
        }

        // ── Per-round region counts ─────────────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldHaveExactly1Elite8GamePerRegion()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var e8 = result.Where(g => g.Round == TournamentRound.Elite8).ToList();

            foreach (var region in new[] { "East", "West", "South", "Midwest" })
                e8.Count(g => g.Region == region).Should().Be(1, $"Elite 8 should have 1 game for region {region}");
        }

        [Fact]
        public void GenerateBracket_ShouldHaveExactly2Sweet16GamesPerRegion()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var s16 = result.Where(g => g.Round == TournamentRound.Sweet16).ToList();

            foreach (var region in new[] { "East", "West", "South", "Midwest" })
                s16.Count(g => g.Region == region).Should().Be(2, $"Sweet 16 should have 2 games for region {region}");
        }

        [Fact]
        public void GenerateBracket_ShouldHaveExactly4RoundOf32GamesPerRegion()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var r32 = result.Where(g => g.Round == TournamentRound.RoundOf32).ToList();

            foreach (var region in new[] { "East", "West", "South", "Midwest" })
                r32.Count(g => g.Region == region).Should().Be(4, $"Round of 32 should have 4 games for region {region}");
        }

        [Fact]
        public void GenerateBracket_ShouldHaveExactly8RoundOf64GamesPerRegion()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var r64 = result.Where(g => g.Round == TournamentRound.RoundOf64).ToList();

            foreach (var region in new[] { "East", "West", "South", "Midwest" })
                r64.Count(g => g.Region == region).Should().Be(8, $"Round of 64 should have 8 games for region {region}");
        }

        [Fact]
        public void GenerateBracket_ShouldHaveExactly1FirstFourGamePerRegion()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var ff4 = result.Where(g => g.Round == TournamentRound.FirstFour).ToList();

            foreach (var region in new[] { "East", "West", "South", "Midwest" })
                ff4.Count(g => g.Region == region).Should().Be(1, $"First Four should have 1 game for region {region}");
        }

        // ── Round-to-round NextGameId chain correctness ─────────────────────────

        [Fact]
        public void GenerateBracket_ShouldWireFirstFourToRoundOf64_NotDirectlyToRoundOf32()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var idToGame = result.ToDictionary(g => g.Id);

            var firstFourGames = result.Where(g => g.Round == TournamentRound.FirstFour);
            foreach (var ff4 in firstFourGames)
            {
                ff4.NextGameId.Should().NotBeNull();
                var target = idToGame[ff4.NextGameId!];
                target.Round.Should().Be(TournamentRound.RoundOf64,
                    $"First Four game in {ff4.Region} should point to a Round of 64 game");
            }
        }

        [Fact]
        public void GenerateBracket_ShouldWireRoundOf64ToRoundOf32()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var idToGame = result.ToDictionary(g => g.Id);

            foreach (var game in result.Where(g => g.Round == TournamentRound.RoundOf64))
            {
                var target = idToGame[game.NextGameId!];
                target.Round.Should().Be(TournamentRound.RoundOf32,
                    $"Round of 64 game {game.Id} in {game.Region} should point to a Round of 32 game");
            }
        }

        [Fact]
        public void GenerateBracket_ShouldWireRoundOf32ToSweet16()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var idToGame = result.ToDictionary(g => g.Id);

            foreach (var game in result.Where(g => g.Round == TournamentRound.RoundOf32))
            {
                var target = idToGame[game.NextGameId!];
                target.Round.Should().Be(TournamentRound.Sweet16,
                    $"Round of 32 game {game.Id} in {game.Region} should point to a Sweet 16 game");
            }
        }

        [Fact]
        public void GenerateBracket_ShouldWireSweet16ToElite8()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var idToGame = result.ToDictionary(g => g.Id);

            foreach (var game in result.Where(g => g.Round == TournamentRound.Sweet16))
            {
                var target = idToGame[game.NextGameId!];
                target.Round.Should().Be(TournamentRound.Elite8,
                    $"Sweet 16 game {game.Id} in {game.Region} should point to an Elite 8 game");
            }
        }

        [Fact]
        public void GenerateBracket_ShouldWireElite8ToFinalFour()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var idToGame = result.ToDictionary(g => g.Id);

            foreach (var game in result.Where(g => g.Round == TournamentRound.Elite8))
            {
                var target = idToGame[game.NextGameId!];
                target.Round.Should().Be(TournamentRound.FinalFour,
                    $"Elite 8 game {game.Id} in {game.Region} should point to a Final Four game");
            }
        }

        [Fact]
        public void GenerateBracket_ShouldWireFinalFourToChampionship()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var idToGame = result.ToDictionary(g => g.Id);

            foreach (var game in result.Where(g => g.Round == TournamentRound.FinalFour))
            {
                var target = idToGame[game.NextGameId!];
                target.Round.Should().Be(TournamentRound.NationalChampionship,
                    $"Final Four game {game.Id} should point to the Championship game");
            }
        }

        // ── Same-region chain integrity ─────────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldWireGamesToSameRegion_ThroughoutRegionalChain()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var idToGame = result.ToDictionary(g => g.Id);

            // For each regional round (R64→R32→S16→E8), the NextGameId must point to a game
            // in the same region.
            var regionalRounds = new[]
            {
                TournamentRound.RoundOf64,
                TournamentRound.RoundOf32,
                TournamentRound.Sweet16
            };

            foreach (var game in result.Where(g => regionalRounds.Contains(g.Round)))
            {
                var target = idToGame[game.NextGameId!];
                target.Region.Should().Be(game.Region,
                    $"{game.Round} game in {game.Region} should advance within the same region");
            }
        }

        // ── FirstFour wires to correct slot in correct region ───────────────────

        [Fact]
        public void GenerateBracket_ShouldWireFirstFourToSameRegion_AsTheirTarget()
        {
            var result = Sut().GenerateBracket(StandardRequest());
            var idToGame = result.ToDictionary(g => g.Id);

            foreach (var ff4 in result.Where(g => g.Round == TournamentRound.FirstFour))
            {
                var target = idToGame[ff4.NextGameId!];
                target.Region.Should().Be(ff4.Region,
                    $"First Four game in {ff4.Region} should point to a Round of 64 game in the same region");
            }
        }

        // ── R32 fan-in: each R32 game receives exactly 2 feeders ───────────────

        [Fact]
        public void GenerateBracket_ShouldHaveExactly2RoundOf64GamesFeedingEachRoundOf32Game()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            var r32Games = result.Where(g => g.Round == TournamentRound.RoundOf32).ToList();
            var r64Games = result.Where(g => g.Round == TournamentRound.RoundOf64).ToList();

            foreach (var r32Game in r32Games)
            {
                var feeders = r64Games.Count(g => g.NextGameId == r32Game.Id);
                feeders.Should().Be(2, $"R32 game {r32Game.Id} in {r32Game.Region} should be fed by exactly 2 R64 games");
            }
        }

        // ── S16 fan-in: each S16 game receives exactly 2 R32 feeders ───────────

        [Fact]
        public void GenerateBracket_ShouldHaveExactly2RoundOf32GamesFeedingEachSweet16Game()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            var s16Games = result.Where(g => g.Round == TournamentRound.Sweet16).ToList();
            var r32Games = result.Where(g => g.Round == TournamentRound.RoundOf32).ToList();

            foreach (var s16Game in s16Games)
            {
                var feeders = r32Games.Count(g => g.NextGameId == s16Game.Id);
                feeders.Should().Be(2, $"S16 game {s16Game.Id} in {s16Game.Region} should be fed by exactly 2 R32 games");
            }
        }

        // ── Validation — too few regions ───────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenFewerThan4RegionsProvided()
        {
            var request = StandardRequest();
            request.Regions = new List<string> { "East", "West", "South" };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*4 regions*");
        }

        // ── Validation — too many regions ──────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenMoreThan4RegionsProvided()
        {
            var request = StandardRequest();
            request.Regions = new List<string> { "East", "West", "South", "Midwest", "Extra" };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*4 regions*");
        }

        // ── Validation — null regions ──────────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenRegionsIsNull()
        {
            var request = StandardRequest();
            request.Regions = null!;

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>();
        }

        // ── Validation — wrong pairing count ──────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenOnlyOneFinalFourPairingProvided()
        {
            var request = StandardRequest();
            request.FinalFourPairings = new List<List<string>>
            {
                new() { "East", "West" }
            };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*2 Final Four pairings*");
        }

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenFinalFourPairingsIsNull()
        {
            var request = StandardRequest();
            request.FinalFourPairings = null!;

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>();
        }

        // ── Validation — pairing with wrong inner count ────────────────────────

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenAPairingContainsOnlyOneRegion()
        {
            var request = StandardRequest();
            request.FinalFourPairings = new List<List<string>>
            {
                new() { "East" },           // only 1 region
                new() { "South", "Midwest" }
            };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*2 regions*");
        }

        // ── Validation — duplicate region in pairings ──────────────────────────

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenRegionAppearsInBothPairings()
        {
            var request = StandardRequest();
            request.FinalFourPairings = new List<List<string>>
            {
                new() { "East", "West" },
                new() { "East", "Midwest" }   // East duplicated
            };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*exactly one Final Four pairing*");
        }

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenRegionAppearsTwiceInSamePairing()
        {
            var request = StandardRequest();
            request.FinalFourPairings = new List<List<string>>
            {
                new() { "East", "East" },
                new() { "South", "Midwest" }
            };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*exactly one Final Four pairing*");
        }

        // ── Validation — region in pairings not in Regions list ────────────────

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenPairingRegionNotInRegionsList()
        {
            var request = StandardRequest();
            request.FinalFourPairings = new List<List<string>>
            {
                new() { "East", "West" },
                new() { "South", "Pacific" }   // Pacific not in Regions
            };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*Pacific*");
        }

        // ── Case-insensitivity ─────────────────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldAcceptRegionNamesWithDifferentCasing()
        {
            var request = new BracketGenerationRequest
            {
                PoolId = "pool-1",
                SeasonId = "season-2026",
                Regions = new List<string> { "east", "WEST", "South", "midwest" },
                FinalFourPairings = new List<List<string>>
                {
                    new() { "EAST", "west" },
                    new() { "SOUTH", "MIDWEST" }
                }
            };

            var act = () => Sut().GenerateBracket(request);

            act.Should().NotThrow();
        }

        [Fact]
        public void GenerateBracket_ShouldReturn67Games_WhenRegionNamesUseDifferentCasing()
        {
            var request = new BracketGenerationRequest
            {
                PoolId = "pool-1",
                SeasonId = "season-2026",
                Regions = new List<string> { "east", "WEST", "South", "midwest" },
                FinalFourPairings = new List<List<string>>
                {
                    new() { "EAST", "west" },
                    new() { "SOUTH", "MIDWEST" }
                }
            };

            var result = Sut().GenerateBracket(request);

            result.Should().HaveCount(67);
        }

        // ── SeasonId propagation ───────────────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldPropagateCustomSeasonId_ToAllGames()
        {
            var request = StandardRequest();
            request.SeasonId = "season-custom-99";

            var result = Sut().GenerateBracket(request);

            result.Should().OnlyContain(g => g.SeasonId == "season-custom-99");
        }

        // ── Idempotency — two calls produce independent game sets ───────────────

        [Fact]
        public void GenerateBracket_ShouldProduceDifferentIds_OnEachCall()
        {
            var sut = Sut();
            var first = sut.GenerateBracket(StandardRequest()).Select(g => g.Id).ToHashSet();
            var second = sut.GenerateBracket(StandardRequest()).Select(g => g.Id).ToHashSet();

            first.Overlaps(second).Should().BeFalse("each call should generate fresh GUIDs");
        }
    }
}
