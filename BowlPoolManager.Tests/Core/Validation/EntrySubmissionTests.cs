using FluentAssertions;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Validation;

namespace BowlPoolManager.Tests.Core.Validation
{
    /// <summary>Issue 128 (football pick checks) and issue 161 (IsAdminEntered).</summary>
    public class EntrySubmissionTests
    {
        private static BowlGame Game(string id, string home, string away, int hour, string? next = null, bool playoff = true) => new()
        {
            Id = id, BowlName = id.ToUpperInvariant(), TeamHome = home, TeamAway = away, NextGameId = next, IsPlayoff = playoff,
            StartTime = new DateTime(2026, 12, 20, hour, 0, 0, DateTimeKind.Utc)
        };

        private static List<BowlGame> Bracket() => new()
        {
            Game("r1a", "Miami", "Texas A&M", 12, "qf"),
            Game("r1b", "Oregon", "James Madison", 13, "qf"),
            Game("qf", "Winner of R1A", "Winner of R1B", 18),
            Game("bowl", "Toledo", "Pitt", 14, null, playoff: false)
        };

        private static readonly string[] PoolIds = { "r1a", "r1b", "qf", "bowl" };

        private static BracketEntry Entry(params (string game, string team)[] picks) => new()
        {
            Id = Guid.NewGuid().ToString(), PlayerName = "Bracket",
            Picks = picks.ToDictionary(p => p.game, p => p.team)
        };

        private static ValidationResult Check(BracketEntry submitted, BracketEntry? existing = null, List<BowlGame>? games = null) =>
            EntrySubmission.Validate(submitted, existing, PoolIds, games ?? Bracket());

        // ── #128: server rejects picks that cannot be made ──────────────────────

        [Fact]
        public void Validate_ShouldAccept_WhenEveryPickIsReachable()
        {
            Check(Entry(("r1a", "Miami"), ("r1b", "Oregon"), ("qf", "Oregon"), ("bowl", "Pitt"))).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldAccept_WhenPicksAreIncomplete()
        {
            Check(Entry(("r1a", "Miami"))).IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("Winner of R1B")]
        [InlineData("TBD")]
        public void Validate_ShouldRefuse_WhenPickIsPlaceholderText(string pick)
        {
            var result = Check(Entry(("r1a", "Miami"), ("qf", pick)));

            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Contain("QF");
        }

        [Fact]
        public void Validate_ShouldRefuse_WhenPickIsATeamTheEntrysEarlierPicksDoNotReach()
        {
            // Miami was not picked to win r1a, so Miami cannot be picked in the quarterfinal.
            var result = Check(Entry(("r1a", "Texas A&M"), ("r1b", "Oregon"), ("qf", "Miami")));

            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_ShouldRefuse_WhenALaterPickHasNoFeederPick()
        {
            Check(Entry(("qf", "Oregon"))).IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_ShouldRefuse_WhenPickIsForAGameOutsideThePool()
        {
            Check(Entry(("elsewhere", "Miami"))).IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_ShouldRefuse_WhenPickIsNotATeamInThatGame()
        {
            Check(Entry(("bowl", "Miami"))).IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_ShouldAllowAStoredPickThroughUnchanged_WhenTheEntryIsOldAndStale()
        {
            // The stored entry already has the stale quarterfinal pick; saving it again (a rename) must still work.
            var stored = Entry(("r1a", "Texas A&M"), ("r1b", "Oregon"), ("qf", "Miami"));
            var submitted = Entry(("r1a", "Texas A&M"), ("r1b", "Oregon"), ("qf", "Miami"));

            Check(submitted, stored).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldRefuse_WhenAStoredStalePickIsChangedToAnotherUnreachableTeam()
        {
            var stored = Entry(("r1a", "Texas A&M"), ("r1b", "Oregon"), ("qf", "Miami"));
            var submitted = Entry(("r1a", "Texas A&M"), ("r1b", "Oregon"), ("qf", "Winner of R1A"));

            Check(submitted, stored).IsValid.Should().BeFalse();
        }

        [Fact]
        public void Validate_ShouldAllowAStoredPickForAGameNowOutsideThePool_WhenUnchanged()
        {
            var stored = Entry(("old-game", "Somebody"));
            var submitted = Entry(("old-game", "Somebody"), ("r1a", "Miami"));

            Check(submitted, stored).IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldSkipTheReachabilityCheck_WhenNoBracketGamesAreGiven()
        {
            // Hoops passes no football games; only the pool-membership check applies.
            var result = EntrySubmission.Validate(Entry(("r1a", "anything")), null, PoolIds);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldIgnoreEmptyPicks()
        {
            Check(Entry(("r1a", ""), ("r1b", "Oregon"))).IsValid.Should().BeTrue();
        }

        // ── #161: IsAdminEntered ────────────────────────────────────────────────

        private static BracketEntry Build(BracketEntry submitted, BracketEntry? existing, bool isAdmin, bool assignToMe = false, string caller = "admin-1") =>
            EntrySubmission.Build(submitted, existing, "pool-1", "season-1", Constants.DocumentTypes.BracketEntry, caller, isAdmin, assignToMe);

        [Fact]
        public void Build_ShouldSetIsAdminEntered_WhenAnAdminCreatesAnEntryForSomeoneElse()
        {
            var submitted = Entry(); submitted.UserId = "player-1";

            var built = Build(submitted, null, isAdmin: true);

            built.UserId.Should().Be("player-1");
            built.IsAdminEntered.Should().BeTrue();
        }

        [Fact]
        public void Build_ShouldNotSetIsAdminEntered_WhenAnAdminCreatesAnEntryForThemselves()
        {
            var submitted = Entry(); submitted.UserId = "admin-1";

            Build(submitted, null, isAdmin: true).IsAdminEntered.Should().BeFalse();
        }

        [Fact]
        public void Build_ShouldNotSetIsAdminEntered_WhenAnAdminUsesAssignToMe()
        {
            var submitted = Entry(); submitted.UserId = "player-1";

            var built = Build(submitted, null, isAdmin: true, assignToMe: true);

            built.UserId.Should().Be("admin-1");
            built.IsAdminEntered.Should().BeFalse();
        }

        [Fact]
        public void Build_ShouldNotSetIsAdminEntered_WhenAnAdminSendsNoOwner()
        {
            Build(Entry(), null, isAdmin: true).IsAdminEntered.Should().BeFalse();
        }

        [Fact]
        public void Build_ShouldNotSetIsAdminEntered_WhenAPlayerCreatesAnEntry()
        {
            var submitted = Entry(); submitted.UserId = "someone-else"; submitted.IsAdminEntered = true;

            var built = Build(submitted, null, isAdmin: false, caller: "player-1");

            built.UserId.Should().Be("player-1");
            built.IsAdminEntered.Should().BeFalse();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Build_ShouldKeepTheStoredValue_WhenAnAdminEditsAnExistingEntry(bool stored)
        {
            var existing = Entry(); existing.UserId = "player-1"; existing.IsAdminEntered = stored;
            var submitted = Entry(); submitted.Id = existing.Id; submitted.UserId = "player-2";

            Build(submitted, existing, isAdmin: true).IsAdminEntered.Should().Be(stored);
        }
    }
}
