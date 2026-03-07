using FluentAssertions;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Tests.Core
{
    public class BracketGeneratorTests
    {
        // ── Shared fixture ─────────────────────────────────────────────────────
        //
        // Mirrors a realistic bracket: South has two First Four games (16-seed and 11-seed),
        // East has one (16-seed), Midwest has one (11-seed), West has none.
        // Final Four: South vs West (semifinal 1), East vs Midwest (semifinal 2).

        private static BracketGenerationRequest StandardRequest() => new()
        {
            PoolId = "pool-1",
            SeasonId = "season-2026",
            Regions = new List<string> { "East", "West", "South", "Midwest" },
            FinalFourPairings = new List<List<string>>
            {
                new() { "South", "West" },
                new() { "East", "Midwest" }
            },
            FirstFourGames = new List<FirstFourEntry>
            {
                new() { Region = "South",   Seed = 16 },
                new() { Region = "East",    Seed = 16 },
                new() { Region = "South",   Seed = 11 },
                new() { Region = "Midwest", Seed = 11 },
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

            // pairing[0] = South, West → must both point to the same FF game
            var ffGames = result.Where(g => g.Round == TournamentRound.FinalFour).ToList();
            var e8South = result.Single(g => g.Round == TournamentRound.Elite8 && g.Region == "South");
            var e8West = result.Single(g => g.Round == TournamentRound.Elite8 && g.Region == "West");

            e8South.NextGameId.Should().Be(e8West.NextGameId);
            ffGames.Should().Contain(g => g.Id == e8South.NextGameId);
        }

        [Fact]
        public void GenerateBracket_ShouldWireElite8ToCorrectFinalFourGame_ForPairing1()
        {
            var request = StandardRequest();
            var result = Sut().GenerateBracket(request);

            // pairing[1] = East, Midwest → must both point to the same FF game
            var ffGames = result.Where(g => g.Round == TournamentRound.FinalFour).ToList();
            var e8East = result.Single(g => g.Round == TournamentRound.Elite8 && g.Region == "East");
            var e8Midwest = result.Single(g => g.Round == TournamentRound.Elite8 && g.Region == "Midwest");

            e8East.NextGameId.Should().Be(e8Midwest.NextGameId);
            ffGames.Should().Contain(g => g.Id == e8East.NextGameId);
        }

        [Fact]
        public void GenerateBracket_ShouldWireElite8ToDifferentFinalFourGames_ForDifferentPairings()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            var e8South = result.Single(g => g.Round == TournamentRound.Elite8 && g.Region == "South");
            var e8East = result.Single(g => g.Round == TournamentRound.Elite8 && g.Region == "East");

            e8South.NextGameId.Should().NotBe(e8East.NextGameId);
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

        // ── First Four counts match explicit config ─────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldHaveFirstFourGameCounts_MatchingExplicitConfig()
        {
            var request = StandardRequest();
            // South: 2 (16-seed + 11-seed), East: 1 (16-seed), Midwest: 1 (11-seed), West: 0
            var result = Sut().GenerateBracket(request);
            var ff4 = result.Where(g => g.Round == TournamentRound.FirstFour).ToList();

            ff4.Count(g => g.Region == "South").Should().Be(2,   "South has 2 First Four entries in the config");
            ff4.Count(g => g.Region == "East").Should().Be(1,    "East has 1 First Four entry in the config");
            ff4.Count(g => g.Region == "Midwest").Should().Be(1, "Midwest has 1 First Four entry in the config");
            ff4.Count(g => g.Region == "West").Should().Be(0,    "West has no First Four entries in the config");
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

        // ── First Four wires to correct region ──────────────────────────────────

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

        // ── First Four wires to the correct R64 slot based on configured seed ───

        [Fact]
        public void GenerateBracket_ShouldWireFirstFourGame_ToR64SlotMatchingConfiguredSeed()
        {
            var request = StandardRequest();
            var result = Sut().GenerateBracket(request);
            var idToGame = result.ToDictionary(g => g.Id);

            // Seed 16 → must feed the "1v16" R64 slot
            var south16 = result.Single(g => g.Round == TournamentRound.FirstFour
                                             && g.Region == "South" && g.TeamHomeSeed == 16);
            idToGame[south16.NextGameId!].SeedMatchup.Should().Be("1v16",
                "a seed-16 First Four game must feed the '1v16' R64 slot");

            // Seed 11 → must feed the "6v11" R64 slot
            var south11 = result.Single(g => g.Round == TournamentRound.FirstFour
                                             && g.Region == "South" && g.TeamHomeSeed == 11);
            idToGame[south11.NextGameId!].SeedMatchup.Should().Be("6v11",
                "a seed-11 First Four game must feed the '6v11' R64 slot");

            var east16 = result.Single(g => g.Round == TournamentRound.FirstFour
                                            && g.Region == "East" && g.TeamHomeSeed == 16);
            idToGame[east16.NextGameId!].SeedMatchup.Should().Be("1v16",
                "East seed-16 First Four game must feed the '1v16' R64 slot");

            var midwest11 = result.Single(g => g.Round == TournamentRound.FirstFour
                                               && g.Region == "Midwest" && g.TeamHomeSeed == 11);
            idToGame[midwest11.NextGameId!].SeedMatchup.Should().Be("6v11",
                "Midwest seed-11 First Four game must feed the '6v11' R64 slot");
        }

        [Fact]
        public void GenerateBracket_ShouldWireFirstFourGame_ToArbitrarySeedSlot()
        {
            // Verify generality: a seed-9 First Four game must feed the "8v9" R64 slot
            var request = new BracketGenerationRequest
            {
                PoolId = "pool-1",
                SeasonId = "season-2026",
                Regions = new List<string> { "East", "West", "South", "Midwest" },
                FinalFourPairings = new List<List<string>>
                {
                    new() { "South", "West" },
                    new() { "East", "Midwest" }
                },
                FirstFourGames = new List<FirstFourEntry>
                {
                    new() { Region = "East",    Seed = 9  },
                    new() { Region = "West",    Seed = 16 },
                    new() { Region = "South",   Seed = 11 },
                    new() { Region = "Midwest", Seed = 12 },
                }
            };

            var result = Sut().GenerateBracket(request);
            var idToGame = result.ToDictionary(g => g.Id);

            var east9 = result.Single(g => g.Round == TournamentRound.FirstFour && g.Region == "East");
            idToGame[east9.NextGameId!].SeedMatchup.Should().Be("8v9",
                "a seed-9 First Four game must feed the '8v9' R64 slot");

            var midwest12 = result.Single(g => g.Round == TournamentRound.FirstFour && g.Region == "Midwest");
            idToGame[midwest12.NextGameId!].SeedMatchup.Should().Be("5v12",
                "a seed-12 First Four game must feed the '5v12' R64 slot");
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

        // ── E8 fan-in: each E8 game receives exactly 2 S16 feeders ────────────

        [Fact]
        public void GenerateBracket_ShouldHaveExactly2Sweet16GamesFeedingEachElite8Game()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            var e8Games = result.Where(g => g.Round == TournamentRound.Elite8).ToList();
            var s16Games = result.Where(g => g.Round == TournamentRound.Sweet16).ToList();

            foreach (var e8Game in e8Games)
            {
                var feeders = s16Games.Count(g => g.NextGameId == e8Game.Id);
                feeders.Should().Be(2, $"E8 game {e8Game.Id} in {e8Game.Region} should be fed by exactly 2 S16 games");
            }
        }

        // ── Championship fan-in: championship receives exactly 2 FF feeders ────

        [Fact]
        public void GenerateBracket_ShouldHaveExactly2FinalFourGamesFeedingChampionship()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            var championship = result.Single(g => g.Round == TournamentRound.NationalChampionship);
            var feeders = result.Count(g => g.Round == TournamentRound.FinalFour
                                           && g.NextGameId == championship.Id);

            feeders.Should().Be(2, "the Championship game should be fed by exactly 2 Final Four games");
        }

        // ── Region is null for FinalFour and Championship games ─────────────────

        [Fact]
        public void GenerateBracket_ShouldSetNullRegion_OnFinalFourGames()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Where(g => g.Round == TournamentRound.FinalFour)
                  .Should().OnlyContain(g => g.Region == null,
                      "Final Four games are cross-regional and should have a null Region");
        }

        [Fact]
        public void GenerateBracket_ShouldSetNullRegion_OnChampionshipGame()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Single(g => g.Round == TournamentRound.NationalChampionship)
                  .Region.Should().BeNull("the Championship game is cross-regional and should have a null Region");
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
                new() { "South", "West" }
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
                new() { "South" },                   // only 1 region
                new() { "East", "Midwest" }
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
                new() { "South", "West" },
                new() { "South", "Midwest" }   // South duplicated
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
                new() { "South", "South" },
                new() { "East", "Midwest" }
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
                new() { "South", "West" },
                new() { "East", "Pacific" }   // Pacific not in Regions
            };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*Pacific*");
        }

        // ── Validation — FirstFourGames null / wrong count ─────────────────────

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenFirstFourGamesIsNull()
        {
            var request = StandardRequest();
            request.FirstFourGames = null!;

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*4 First Four*");
        }

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenFewerThan4FirstFourGamesProvided()
        {
            var request = StandardRequest();
            request.FirstFourGames = new List<FirstFourEntry>
            {
                new() { Region = "South", Seed = 16 },
                new() { Region = "East",  Seed = 16 },
                new() { Region = "South", Seed = 11 },
            };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*4 First Four*");
        }

        // ── Validation — invalid seed in FirstFourGames ────────────────────────

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenFirstFourSeedIsOutOfRange()
        {
            var request = StandardRequest();
            request.FirstFourGames[0] = new FirstFourEntry { Region = "East", Seed = 17 };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*valid tournament seed*");
        }

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenFirstFourSeedIsZero()
        {
            var request = StandardRequest();
            request.FirstFourGames[0] = new FirstFourEntry { Region = "East", Seed = 0 };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*valid tournament seed*");
        }

        // ── Validation — too many FirstFourGames ───────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenMoreThan4FirstFourGamesProvided()
        {
            var request = StandardRequest();
            request.FirstFourGames = new List<FirstFourEntry>
            {
                new() { Region = "South",   Seed = 16 },
                new() { Region = "East",    Seed = 16 },
                new() { Region = "South",   Seed = 11 },
                new() { Region = "Midwest", Seed = 11 },
                new() { Region = "West",    Seed = 12 },
            };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*4 First Four*");
        }

        // ── Validation — FirstFour region not in Regions list ─────────────────

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenFirstFourRegionNotInRegionsList()
        {
            var request = StandardRequest();
            request.FirstFourGames[0] = new FirstFourEntry { Region = "Pacific", Seed = 16 };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*Pacific*");
        }

        // ── Validation — duplicate Region/Seed in FirstFourGames ──────────────

        [Fact]
        public void GenerateBracket_ShouldThrowArgumentException_WhenFirstFourHasDuplicateRegionSeedPair()
        {
            var request = StandardRequest();
            // Replace the last entry so South/16 appears twice
            request.FirstFourGames[3] = new FirstFourEntry { Region = "South", Seed = 16 };

            var act = () => Sut().GenerateBracket(request);

            act.Should().Throw<ArgumentException>().WithMessage("*unique region/seed*");
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
                    new() { "SOUTH", "west" },
                    new() { "EAST", "MIDWEST" }
                },
                FirstFourGames = new List<FirstFourEntry>
                {
                    new() { Region = "SOUTH",   Seed = 16 },
                    new() { Region = "east",    Seed = 16 },
                    new() { Region = "South",   Seed = 11 },
                    new() { Region = "MIDWEST", Seed = 11 },
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
                    new() { "SOUTH", "west" },
                    new() { "EAST", "MIDWEST" }
                },
                FirstFourGames = new List<FirstFourEntry>
                {
                    new() { Region = "SOUTH",   Seed = 16 },
                    new() { Region = "east",    Seed = 16 },
                    new() { Region = "South",   Seed = 11 },
                    new() { Region = "MIDWEST", Seed = 11 },
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

        // ── SeedMatchup — R64 games ────────────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldSetNonNullSeedMatchup_OnAllRoundOf64Games()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Where(g => g.Round == TournamentRound.RoundOf64)
                  .Should().OnlyContain(g => g.SeedMatchup != null,
                      "every R64 game must have a SeedMatchup label");
        }

        [Fact]
        public void GenerateBracket_ShouldSetNonNullSeedMatchup_OnAllFirstFourGames()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Where(g => g.Round == TournamentRound.FirstFour)
                  .Should().OnlyContain(g => g.SeedMatchup != null,
                      "every First Four game must have a SeedMatchup label");
        }

        [Fact]
        public void GenerateBracket_ShouldSetNullSeedMatchup_OnAllRoundOf32Games()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Where(g => g.Round == TournamentRound.RoundOf32)
                  .Should().OnlyContain(g => g.SeedMatchup == null,
                      "R32 games should not have a SeedMatchup label");
        }

        [Fact]
        public void GenerateBracket_ShouldSetNullSeedMatchup_OnAllSweet16Games()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Where(g => g.Round == TournamentRound.Sweet16)
                  .Should().OnlyContain(g => g.SeedMatchup == null,
                      "Sweet 16 games should not have a SeedMatchup label");
        }

        [Fact]
        public void GenerateBracket_ShouldSetNullSeedMatchup_OnAllElite8Games()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Where(g => g.Round == TournamentRound.Elite8)
                  .Should().OnlyContain(g => g.SeedMatchup == null,
                      "Elite 8 games should not have a SeedMatchup label");
        }

        [Fact]
        public void GenerateBracket_ShouldSetNullSeedMatchup_OnAllFinalFourGames()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Where(g => g.Round == TournamentRound.FinalFour)
                  .Should().OnlyContain(g => g.SeedMatchup == null,
                      "Final Four games should not have a SeedMatchup label");
        }

        [Fact]
        public void GenerateBracket_ShouldSetNullSeedMatchup_OnChampionshipGame()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Single(g => g.Round == TournamentRound.NationalChampionship)
                  .SeedMatchup.Should().BeNull("the Championship game should not have a SeedMatchup label");
        }

        // ── SeedMatchup — R64 label set correctness ────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldProduceExactlyTheExpectedR64SeedMatchupLabels()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            var expectedLabels = new[] { "1v16", "8v9", "5v12", "4v13", "6v11", "3v14", "7v10", "2v15" };
            var r64Matchups = result
                .Where(g => g.Round == TournamentRound.RoundOf64)
                .Select(g => g.SeedMatchup!)
                .ToList();

            r64Matchups.Should().HaveCount(32);
            foreach (var label in expectedLabels)
                r64Matchups.Count(m => m == label).Should().Be(4,
                    $"label '{label}' should appear exactly once per region (4 total)");
        }

        [Fact]
        public void GenerateBracket_ShouldHaveExactlyOne1v16R64Game_PerRegion()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            foreach (var region in new[] { "East", "West", "South", "Midwest" })
            {
                result.Count(g => g.Round == TournamentRound.RoundOf64
                                  && g.Region == region
                                  && g.SeedMatchup == "1v16")
                      .Should().Be(1, $"region '{region}' should have exactly one '1v16' R64 game");
            }
        }

        [Fact]
        public void GenerateBracket_ShouldHaveExactlyOne6v11R64Game_PerRegion()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            foreach (var region in new[] { "East", "West", "South", "Midwest" })
            {
                result.Count(g => g.Round == TournamentRound.RoundOf64
                                  && g.Region == region
                                  && g.SeedMatchup == "6v11")
                      .Should().Be(1, $"region '{region}' should have exactly one '6v11' R64 game");
            }
        }

        // ── SeedMatchup — First Four label is "XvX" matching configured seed ───

        [Fact]
        public void GenerateBracket_ShouldAssignXvXSeedMatchup_ToFirstFourGames_MatchingConfiguredSeed()
        {
            var request = StandardRequest();
            var result = Sut().GenerateBracket(request);

            // Each First Four game's SeedMatchup should be "XvX" where X is the configured seed
            foreach (var entry in request.FirstFourGames)
            {
                var ff4 = result.Single(g =>
                    g.Round == TournamentRound.FirstFour &&
                    string.Equals(g.Region, entry.Region, StringComparison.OrdinalIgnoreCase) &&
                    g.TeamHomeSeed == entry.Seed);

                ff4.SeedMatchup.Should().Be($"{entry.Seed}v{entry.Seed}",
                    $"First Four for seed {entry.Seed} in {entry.Region} should have SeedMatchup '{entry.Seed}v{entry.Seed}'");
            }
        }

        // ── Auto-seeding — R64 games ───────────────────────────────────────────

        [Fact]
        public void GenerateBracket_ShouldAutoSetHomeSeed_OnR64Games_FromSeedMatchup()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            var r64 = result.Where(g => g.Round == TournamentRound.RoundOf64).ToList();
            r64.Should().OnlyContain(g => g.TeamHomeSeed.HasValue,
                "all R64 games should have TeamHomeSeed auto-populated");

            // Spot-check: "1v16" → home seed 1, "8v9" → home seed 8
            result.Where(g => g.Round == TournamentRound.RoundOf64 && g.SeedMatchup == "1v16")
                  .Should().OnlyContain(g => g.TeamHomeSeed == 1);
            result.Where(g => g.Round == TournamentRound.RoundOf64 && g.SeedMatchup == "8v9")
                  .Should().OnlyContain(g => g.TeamHomeSeed == 8);
            result.Where(g => g.Round == TournamentRound.RoundOf64 && g.SeedMatchup == "2v15")
                  .Should().OnlyContain(g => g.TeamHomeSeed == 2);
        }

        [Fact]
        public void GenerateBracket_ShouldAutoSetAwaySeed_OnR64Games_FromSeedMatchup()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            result.Where(g => g.Round == TournamentRound.RoundOf64)
                  .Should().OnlyContain(g => g.TeamAwaySeed.HasValue,
                      "all R64 games should have TeamAwaySeed auto-populated");

            // Spot-check: "1v16" → away seed 16, "6v11" → away seed 11
            result.Where(g => g.Round == TournamentRound.RoundOf64 && g.SeedMatchup == "1v16")
                  .Should().OnlyContain(g => g.TeamAwaySeed == 16);
            result.Where(g => g.Round == TournamentRound.RoundOf64 && g.SeedMatchup == "6v11")
                  .Should().OnlyContain(g => g.TeamAwaySeed == 11);
        }

        [Fact]
        public void GenerateBracket_ShouldAutoSetBothSeeds_OnFirstFourGames_EqualToConfiguredSeed()
        {
            var request = StandardRequest();
            var result = Sut().GenerateBracket(request);

            var ff4Games = result.Where(g => g.Round == TournamentRound.FirstFour).ToList();

            ff4Games.Should().OnlyContain(g =>
                g.TeamHomeSeed.HasValue && g.TeamAwaySeed.HasValue,
                "all First Four games should have both seeds set");

            ff4Games.Should().OnlyContain(g => g.TeamHomeSeed == g.TeamAwaySeed,
                "both participants in a First Four game have the same seed");

            // South seed-16 play-in: both teams are 16-seeds
            var south16 = result.Single(g => g.Round == TournamentRound.FirstFour
                                             && g.Region == "South" && g.TeamHomeSeed == 16);
            south16.TeamAwaySeed.Should().Be(16);

            // Midwest seed-11 play-in: both teams are 11-seeds
            var midwest11 = result.Single(g => g.Round == TournamentRound.FirstFour
                                               && g.Region == "Midwest" && g.TeamHomeSeed == 11);
            midwest11.TeamAwaySeed.Should().Be(11);
        }

        [Fact]
        public void GenerateBracket_ShouldNotAutoSetSeeds_OnLaterRoundGames()
        {
            var result = Sut().GenerateBracket(StandardRequest());

            var laterRounds = result.Where(g =>
                g.Round == TournamentRound.RoundOf32 ||
                g.Round == TournamentRound.Sweet16 ||
                g.Round == TournamentRound.Elite8 ||
                g.Round == TournamentRound.FinalFour ||
                g.Round == TournamentRound.NationalChampionship);

            laterRounds.Should().OnlyContain(g => g.TeamHomeSeed == null && g.TeamAwaySeed == null,
                "seeds are only set for R64 and First Four games; later rounds are determined by propagation");
        }
    }
}
