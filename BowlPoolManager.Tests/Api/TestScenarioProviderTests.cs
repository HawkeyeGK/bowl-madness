using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// The scenario fixtures are test-site seed data. These tests check that each one loads, that its
    /// documents are valid models, that the ids line up (pools to games, entries to pools and games,
    /// bracket links) and that dates are relative to the load time so a scenario never goes stale.
    /// </summary>
    public class TestScenarioProviderTests
    {
        private static readonly DateTime FixedNow = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

        private static TestScenario Load(string name) =>
            new EmbeddedTestScenarioProvider(() => FixedNow).Get(name)!;

        private static List<T> Docs<T>(TestScenario s, string container, string type) =>
            s.Documents
                .Where(d => d.Container == container && d.Body.Value<string>("type") == type)
                .Select(d => d.Body.ToObject<T>(JsonSerializer.CreateDefault())!)
                .ToList();

        [Fact]
        public void ScenarioNames_ShouldListTheThreeScenarios_WhenCreated()
        {
            new EmbeddedTestScenarioProvider(() => FixedNow).ScenarioNames
                .Should().BeEquivalentTo("before-selection-day", "entry-window", "mid-season");
        }

        [Theory]
        [InlineData("no-such-scenario")]
        [InlineData("")]
        [InlineData("../entry-window")]
        [InlineData("Entry-Window")]
        public void Get_ShouldReturnNull_WhenNameIsNotAListedScenario(string name)
        {
            new EmbeddedTestScenarioProvider(() => FixedNow).Get(name).Should().BeNull();
        }

        [Theory]
        [InlineData("before-selection-day")]
        [InlineData("entry-window")]
        [InlineData("mid-season")]
        public void Get_ShouldReturnOnlyAllowedContainersWithMatchingPartitionKeys_ForEveryScenario(string name)
        {
            var s = Load(name);

            s.Name.Should().Be(name);
            s.Documents.Should().NotBeEmpty();
            s.Documents.Should().OnlyContain(d =>
                d.Container == Constants.Database.SeasonsContainer ||
                d.Container == Constants.Database.PicksContainer ||
                d.Container == Constants.Database.ConfigurationContainer);

            foreach (var d in s.Documents)
            {
                d.Body.Value<string>("id").Should().NotBeNullOrEmpty();
                var expectedKey = d.Container == Constants.Database.ConfigurationContainer
                    ? d.Body.Value<string>("id")
                    : d.Container == Constants.Database.SeasonsContainer && d.Body.Value<string>("type") == "Season"
                        ? d.Body.Value<string>("seasonId")
                        : d.Body.Value<string>("seasonId");
                d.PartitionKey.Should().Be(expectedKey);
            }

            s.Documents.Select(d => d.Container + "/" + d.Body.Value<string>("id"))
                .Should().OnlyHaveUniqueItems();
        }

        [Theory]
        [InlineData("before-selection-day")]
        [InlineData("entry-window")]
        [InlineData("mid-season")]
        public void Get_ShouldIncludeCurrentSeasonAndTeamLists_ForEveryScenario(string name)
        {
            var s = Load(name);

            Docs<Season>(s, Constants.Database.SeasonsContainer, "Season").Should().ContainSingle(x => x.IsCurrent);
            var teams = Docs<TeamConfig>(s, Constants.Database.ConfigurationContainer, Constants.DocumentTypes.TeamConfig);
            teams.Should().ContainSingle(t => t.Id == Constants.ConfigDocumentIds.FbsTeamConfig);
            teams.Single().Teams.Count.Should().BeGreaterThanOrEqualTo(40);
        }

        [Fact]
        public void Get_ShouldHaveNoPoolsGamesOrEntries_WhenScenarioIsBeforeSelectionDay()
        {
            var s = Load("before-selection-day");

            Docs<BowlPool>(s, Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlPool).Should().BeEmpty();
            Docs<BowlGame>(s, Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlGame).Should().BeEmpty();
            s.Documents.Should().NotContain(d => d.Container == Constants.Database.PicksContainer);
        }

        [Theory]
        [InlineData("entry-window")]
        [InlineData("mid-season")]
        public void Get_ShouldHaveFamilyAndOfficePoolsAndTheFullGameList_WhenScenarioHasPools(string name)
        {
            var s = Load(name);
            var season = Docs<Season>(s, Constants.Database.SeasonsContainer, "Season").Single();
            var pools = Docs<BowlPool>(s, Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlPool);
            var games = Docs<BowlGame>(s, Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlGame);

            pools.Select(p => p.Name).Should().BeEquivalentTo("Family", "Office");
            pools.Should().OnlyContain(p => p.SeasonId == season.Id && !string.IsNullOrEmpty(p.InviteCode));

            games.Should().HaveCount(26);
            games.Should().OnlyContain(g => g.SeasonId == season.Id);
            games.Count(g => g.Round == TournamentRound.Standard).Should().Be(15);
            games.Count(g => g.Round == TournamentRound.Round1).Should().Be(4);
            games.Count(g => g.Round == TournamentRound.QuarterFinal).Should().Be(4);
            games.Count(g => g.Round == TournamentRound.SemiFinal).Should().Be(2);
            games.Count(g => g.Round == TournamentRound.Championship).Should().Be(1);
            games.Where(g => g.Round == TournamentRound.Standard).Should().OnlyContain(g => !g.IsPlayoff);
            games.Where(g => g.Round != TournamentRound.Standard).Should().OnlyContain(g => g.IsPlayoff);

            var ids = games.Select(g => g.Id).ToHashSet();
            foreach (var p in pools)
            {
                p.GameIds.Should().BeEquivalentTo(ids);
                ids.Should().Contain(p.TieBreakerGameId);
            }
        }

        [Theory]
        [InlineData("entry-window")]
        [InlineData("mid-season")]
        public void Get_ShouldLinkBracketGamesToTheNextRound_WhenScenarioHasPools(string name)
        {
            var games = Docs<BowlGame>(Load(name), Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlGame);
            var byId = games.ToDictionary(g => g.Id);

            foreach (var g in games.Where(g => g.Round == TournamentRound.Standard))
                g.NextGameId.Should().BeNull();

            games.Single(g => g.Round == TournamentRound.Championship).NextGameId.Should().BeNull();

            foreach (var g in games.Where(g => g.IsPlayoff && g.Round != TournamentRound.Championship))
            {
                g.NextGameId.Should().NotBeNullOrEmpty();
                byId.Should().ContainKey(g.NextGameId!);
                ((int)byId[g.NextGameId!].Round).Should().Be((int)g.Round + 1);
            }

            games.Where(g => g.NextGameId == games.Single(x => x.Round == TournamentRound.Championship).Id)
                .Should().HaveCount(2);
        }

        [Theory]
        [InlineData("entry-window")]
        [InlineData("mid-season")]
        public void Get_ShouldGiveEachPairOfFeederGamesDifferentStartTimes_WhenScenarioHasPools(string name)
        {
            // Winner slots (home, away) are assigned by feeder start time, so ties would make them unpredictable.
            var games = Docs<BowlGame>(Load(name), Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlGame);

            foreach (var next in games.Where(g => g.IsPlayoff && g.Round >= TournamentRound.SemiFinal))
                games.Where(g => g.NextGameId == next.Id).Select(g => g.StartTime).Should().OnlyHaveUniqueItems();
            foreach (var next in games.Where(g => g.Round == TournamentRound.QuarterFinal))
                games.Where(g => g.NextGameId == next.Id).Select(g => g.StartTime).Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void Get_ShouldUsePlaceholderTextTheScoringServiceWrites_WhenAQuarterfinalAwaitsAFirstRoundWinner()
        {
            var games = Docs<BowlGame>(Load("entry-window"), Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlGame);

            foreach (var qf in games.Where(g => g.Round == TournamentRound.QuarterFinal))
            {
                var feeder = games.Single(g => g.NextGameId == qf.Id);
                qf.TeamAway.Should().Be($"Winner of {feeder.BowlName}");
            }
        }

        [Fact]
        public void Get_ShouldHaveNoFinalGamesAndAllEntriesBeforeLock_WhenScenarioIsEntryWindow()
        {
            var s = Load("entry-window");
            var games = Docs<BowlGame>(s, Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlGame);
            var pools = Docs<BowlPool>(s, Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlPool);

            games.Should().OnlyContain(g => g.Status == GameStatus.Scheduled && g.TeamHomeScore == null && g.TeamAwayScore == null);
            pools.Should().OnlyContain(p => p.LockDate > FixedNow);
            games.Should().OnlyContain(g => g.StartTime > pools.Max(p => p.LockDate));
        }

        [Fact]
        public void Get_ShouldHaveAboutHalfTheGamesFinalAndOneInProgress_WhenScenarioIsMidSeason()
        {
            var s = Load("mid-season");
            var games = Docs<BowlGame>(s, Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlGame);
            var pools = Docs<BowlPool>(s, Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlPool);

            games.Count(g => g.Status == GameStatus.Final).Should().BeInRange(11, 15);
            games.Count(g => g.Status == GameStatus.InProgress).Should().Be(1);
            games.Where(g => g.Status == GameStatus.Final).Should().OnlyContain(g =>
                g.TeamHomeScore != null && g.TeamAwayScore != null && g.TeamHomeScore != g.TeamAwayScore && g.StartTime < FixedNow);
            games.Where(g => g.Status == GameStatus.Scheduled).Should().OnlyContain(g => g.StartTime > FixedNow);
            pools.Should().OnlyContain(p => p.LockDate < FixedNow);
        }

        [Theory]
        [InlineData("entry-window")]
        [InlineData("mid-season")]
        public void Get_ShouldHaveAboutThirtyEntriesWithSomeIncomplete_WhenScenarioHasPools(string name)
        {
            var s = Load(name);
            var season = Docs<Season>(s, Constants.Database.SeasonsContainer, "Season").Single();
            var pools = Docs<BowlPool>(s, Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlPool);
            var games = Docs<BowlGame>(s, Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlGame);
            var entries = Docs<BracketEntry>(s, Constants.Database.PicksContainer, Constants.DocumentTypes.BracketEntry);

            entries.Count.Should().BeInRange(28, 32);
            entries.Should().OnlyContain(e => e.SeasonId == season.Id && !string.IsNullOrEmpty(e.PlayerName));
            entries.Select(e => e.PoolId).Distinct().Should().BeEquivalentTo(pools.Select(p => p.Id));

            var gameIds = games.Select(g => g.Id).ToHashSet();
            foreach (var e in entries)
                e.Picks!.Keys.Should().OnlyContain(k => gameIds.Contains(k));

            entries.Count(e => e.Picks!.Count < games.Count).Should().BeInRange(3, 10);
            entries.Count(e => e.Picks!.Count == games.Count).Should().BeGreaterThan(15);

            foreach (var pool in pools)
                entries.Where(e => e.PoolId == pool.Id).Select(e => e.PlayerName.ToLowerInvariant())
                    .Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void Get_ShouldPickOneOfTheTwoTeams_WhenAnEntryPicksAStandardBowl()
        {
            var s = Load("entry-window");
            var games = Docs<BowlGame>(s, Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlGame)
                .Where(g => g.Round == TournamentRound.Standard).ToList();
            var entries = Docs<BracketEntry>(s, Constants.Database.PicksContainer, Constants.DocumentTypes.BracketEntry);

            foreach (var e in entries)
                foreach (var g in games.Where(g => e.Picks!.ContainsKey(g.Id)))
                    new[] { g.TeamHome, g.TeamAway }.Should().Contain(e.Picks![g.Id]);
        }

        [Fact]
        public void Get_ShouldMakeDatesRelativeToTheClock_WhenLoadedAtDifferentTimes()
        {
            var early = new EmbeddedTestScenarioProvider(() => FixedNow).Get("entry-window")!;
            var late = new EmbeddedTestScenarioProvider(() => FixedNow.AddDays(30)).Get("entry-window")!;

            DateTime Lock(TestScenario s) => Docs<BowlPool>(s, Constants.Database.SeasonsContainer, Constants.DocumentTypes.BowlPool).Min(p => p.LockDate);

            (Lock(late) - Lock(early)).Should().Be(TimeSpan.FromDays(30));
        }

        [Theory]
        [InlineData("before-selection-day")]
        [InlineData("entry-window")]
        [InlineData("mid-season")]
        public void Get_ShouldLeaveNoUnexpandedDateTokens_ForEveryScenario(string name)
        {
            var text = string.Join("\n", Load(name).Documents.Select(d => d.Body.ToString()));
            text.Should().NotContain("{now");
        }

        [Fact]
        public void Get_ShouldUseMadeUpNamesOnly_WhenLoadingAnyScenario()
        {
            // The scenarios are seeded from fixtures in the repository, never copied from production data.
            // Production ids from the sample files must not appear.
            var all = string.Join("\n", new[] { "entry-window", "mid-season" }
                .SelectMany(n => Load(n).Documents.Select(d => d.Body.ToString())));
            all.Should().NotContain("827bbcf2d0f54e04b07e332218981f14");
            all.Should().NotContain("9e050446-cba0-4bb8-a19e-e52d88b794cd");
        }
    }
}
