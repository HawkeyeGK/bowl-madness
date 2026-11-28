using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;
using FluentAssertions;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>The admin season setup checklist: eight steps in order, each with a state read from existing data.</summary>
    public class SeasonSetupTests
    {
        private static readonly DateTime Now = new(2026, 12, 6, 18, 0, 0, DateTimeKind.Utc);

        private static Season CurrentSeason() => new() { Id = "s1", SeasonId = "2026", Name = "2026-2027 Season", IsCurrent = true };

        private static BowlGame Game(string id, string? externalId = null) =>
            new() { Id = id, SeasonId = "s1", BowlName = id, ExternalId = externalId };

        private static BowlPool Pool(string id, DateTime lockDate, params string[] gameIds) =>
            new() { Id = id, SeasonId = "s1", Name = id, InviteCode = id.ToUpperInvariant(), LockDate = lockDate, GameIds = gameIds.ToList() };

        private static BracketEntry Entry(string poolId, bool paid = false, bool adminEntered = false) =>
            new() { PoolId = poolId, SeasonId = "s1", IsPaid = paid, IsAdminEntered = adminEntered };

        private static IReadOnlyList<SetupStep> Build(Season? season = null, IEnumerable<BowlGame>? games = null,
            IEnumerable<BowlPool>? pools = null, IEnumerable<BracketEntry>? entries = null) =>
            SeasonSetup.Build(season, (games ?? []).ToList(), (pools ?? []).ToList(), (entries ?? []).ToList(), Now);

        private static SetupStep Step(IReadOnlyList<SetupStep> steps, SetupStepKey key) => steps.Single(s => s.Key == key);

        [Fact]
        public void Build_ShouldListTheEightStepsInOrderWithTheirPages_WhenCalled()
        {
            var steps = Build();

            steps.Select(s => s.Key).Should().Equal(
                SetupStepKey.CreateSeason, SetupStepKey.LoadGames, SetupStepKey.LinkFeed, SetupStepKey.SetPoolGames,
                SetupStepKey.OpenPool, SetupStepKey.PrintBlankForms, SetupStepKey.KeyPaperEntries, SetupStepKey.MarkPayments);
            steps.Select(s => s.Href).Should().Equal(
                "admin/seasons", "admin/games", "admin/linker", "admin/pools",
                "admin/pools", "print-bracket", "admin/entry", "admin/payments");
            steps.Select(s => s.LinkText).Should().Equal(
                "Seasons", "Games", "Link games", "Pools", "Pools", "Blank form", "New entry", "Payments");
            steps.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Title) && !string.IsNullOrWhiteSpace(s.Detail));
        }

        [Fact]
        public void Build_ShouldMarkEveryDataStepNotStarted_WhenThereIsNoData()
        {
            var steps = Build();

            Step(steps, SetupStepKey.CreateSeason).State.Should().Be(SetupStepState.NotStarted);
            Step(steps, SetupStepKey.LoadGames).State.Should().Be(SetupStepState.NotStarted);
            Step(steps, SetupStepKey.LinkFeed).State.Should().Be(SetupStepState.NotStarted);
            Step(steps, SetupStepKey.SetPoolGames).State.Should().Be(SetupStepState.NotStarted);
            Step(steps, SetupStepKey.OpenPool).State.Should().Be(SetupStepState.NotStarted);
            Step(steps, SetupStepKey.MarkPayments).State.Should().Be(SetupStepState.NotStarted);
        }

        [Fact]
        public void Build_ShouldMarkPrintAndPaperEntryAsInfo_WhenDataExists()
        {
            var steps = Build(CurrentSeason(), entries: [Entry("family", adminEntered: true)], pools: [Pool("family", Now.AddDays(5), "g1")]);

            Step(steps, SetupStepKey.PrintBlankForms).State.Should().Be(SetupStepState.Info);
            Step(steps, SetupStepKey.KeyPaperEntries).State.Should().Be(SetupStepState.Info);
        }

        [Fact]
        public void Build_ShouldMarkTheSeasonDone_WhenItIsMarkedCurrent()
        {
            var step = Step(Build(CurrentSeason()), SetupStepKey.CreateSeason);

            step.State.Should().Be(SetupStepState.Done);
            step.Detail.Should().Contain("2026-2027 Season");
        }

        [Fact]
        public void Build_ShouldMarkTheSeasonInProgress_WhenTheSeasonShownIsNotMarkedCurrent()
        {
            // SeasonService falls back to the newest season when none is marked current.
            var season = CurrentSeason();
            season.IsCurrent = false;

            Step(Build(season), SetupStepKey.CreateSeason).State.Should().Be(SetupStepState.InProgress);
        }

        [Fact]
        public void Build_ShouldCountTheGames_WhenGamesAreLoaded()
        {
            var step = Step(Build(CurrentSeason(), [Game("g1"), Game("g2"), Game("g3")]), SetupStepKey.LoadGames);

            step.State.Should().Be(SetupStepState.Done);
            step.Detail.Should().Be("3 games loaded");
        }

        [Fact]
        public void Build_ShouldSayOneGame_WhenOneGameIsLoaded()
        {
            Step(Build(CurrentSeason(), [Game("g1")]), SetupStepKey.LoadGames).Detail.Should().Be("1 game loaded");
        }

        [Fact]
        public void Build_ShouldMarkTheFeedInProgress_WhenSomeGamesAreLinked()
        {
            var step = Step(Build(CurrentSeason(), [Game("g1", "401"), Game("g2"), Game("g3", " ")]), SetupStepKey.LinkFeed);

            step.State.Should().Be(SetupStepState.InProgress);
            step.Detail.Should().Be("1 of 3 games linked");
        }

        [Fact]
        public void Build_ShouldMarkTheFeedDone_WhenEveryGameIsLinked()
        {
            var step = Step(Build(CurrentSeason(), [Game("g1", "401"), Game("g2", "402")]), SetupStepKey.LinkFeed);

            step.State.Should().Be(SetupStepState.Done);
            step.Detail.Should().Be("All 2 games linked");
        }

        [Fact]
        public void Build_ShouldMarkTheFeedNotStarted_WhenGamesExistButNoneIsLinked()
        {
            Step(Build(CurrentSeason(), [Game("g1")]), SetupStepKey.LinkFeed).State.Should().Be(SetupStepState.NotStarted);
        }

        [Fact]
        public void Build_ShouldNameThePoolsWithoutGames_WhenSomePoolsHaveNone()
        {
            var steps = Build(CurrentSeason(), pools: [Pool("family", Now.AddDays(5), "g1"), Pool("office", Now.AddDays(5))]);
            var step = Step(steps, SetupStepKey.SetPoolGames);

            step.State.Should().Be(SetupStepState.InProgress);
            step.Detail.Should().Be("No games yet: office");
        }

        [Fact]
        public void Build_ShouldMarkPoolGamesDone_WhenEveryPoolHasGames()
        {
            var steps = Build(CurrentSeason(), pools: [Pool("family", Now.AddDays(5), "g1", "g2"), Pool("office", Now.AddDays(5), "g1")]);
            var step = Step(steps, SetupStepKey.SetPoolGames);

            step.State.Should().Be(SetupStepState.Done);
            step.Detail.Should().Be("2 pools have their games");
        }

        [Fact]
        public void Build_ShouldMarkPoolGamesNotStarted_WhenNoPoolHasGames()
        {
            Step(Build(CurrentSeason(), pools: [Pool("family", Now.AddDays(5))]), SetupStepKey.SetPoolGames)
                .State.Should().Be(SetupStepState.NotStarted);
        }

        [Fact]
        public void Build_ShouldIgnorePoolAndEntries_WhenPoolIsArchived()
        {
            var archived = Pool("old", Now.AddDays(-300));
            archived.IsArchived = true;

            var steps = Build(CurrentSeason(), pools: [archived], entries: [Entry("old", paid: false)]);

            Step(steps, SetupStepKey.SetPoolGames).State.Should().Be(SetupStepState.NotStarted);
            Step(steps, SetupStepKey.MarkPayments).State.Should().Be(SetupStepState.NotStarted);
        }

        [Fact]
        public void Build_ShouldMarkThePoolOpen_WhenAPoolWithGamesLocksLater()
        {
            var step = Step(Build(CurrentSeason(), pools: [Pool("family", Now.AddDays(5), "g1")]), SetupStepKey.OpenPool);

            step.State.Should().Be(SetupStepState.Done);
            // Now + 5 days is Fri Dec 11 2026, 18:00 UTC = 12:00 PM Central (CST).
            step.Detail.Should().Be("Open: family (code FAMILY), locks Fri Dec 11, 12:00 PM CT");
        }

        [Fact]
        public void Build_ShouldCountPoolAsOpen_WhenNowIsExactlyTheLockDate()
        {
            // BUSINESS-RULES section 3: entries are allowed up to and including the lock date.
            Step(Build(CurrentSeason(), pools: [Pool("family", Now, "g1")]), SetupStepKey.OpenPool)
                .Detail.Should().StartWith("Open: family");
        }

        [Fact]
        public void Build_ShouldMarkThePoolStepDone_WhenThePoolHasAlreadyLocked()
        {
            var step = Step(Build(CurrentSeason(), pools: [Pool("family", Now.AddDays(-1), "g1")]), SetupStepKey.OpenPool);

            step.State.Should().Be(SetupStepState.Done);
            step.Detail.Should().Be("No pool is open: each has locked or is concluded");
        }

        [Fact]
        public void Build_ShouldNotCountPoolAsOpen_WhenPoolHasNoGames()
        {
            var step = Step(Build(CurrentSeason(), pools: [Pool("family", Now.AddDays(5))]), SetupStepKey.OpenPool);

            step.State.Should().Be(SetupStepState.NotStarted);
        }

        [Fact]
        public void Build_ShouldNotCountPoolAsOpen_WhenPoolIsConcluded()
        {
            var concluded = Pool("family", Now.AddDays(5), "g1");
            concluded.IsConcluded = true;

            Step(Build(CurrentSeason(), pools: [concluded]), SetupStepKey.OpenPool).Detail.Should().Be("No pool is open: each has locked or is concluded");
        }

        [Fact]
        public void Build_ShouldCountAdminKeyedEntries_WhenActivePoolsHaveEntries()
        {
            var entries = new[] { Entry("family", adminEntered: true), Entry("family", adminEntered: true), Entry("family") };

            Step(Build(CurrentSeason(), pools: [Pool("family", Now.AddDays(5), "g1")], entries: entries), SetupStepKey.KeyPaperEntries)
                .Detail.Should().Be("2 entries keyed by an admin so far");
        }

        [Fact]
        public void Build_ShouldCountPaidEntries_WhenSomeAreUnpaid()
        {
            var entries = new[] { Entry("family", paid: true), Entry("family"), Entry("family") };
            var step = Step(Build(CurrentSeason(), pools: [Pool("family", Now.AddDays(5), "g1")], entries: entries), SetupStepKey.MarkPayments);

            step.State.Should().Be(SetupStepState.InProgress);
            step.Detail.Should().Be("1 of 3 entries paid");
        }

        [Fact]
        public void Build_ShouldMarkPaymentsDone_WhenEveryEntryIsPaid()
        {
            var entries = new[] { Entry("family", paid: true), Entry("family", paid: true) };
            var step = Step(Build(CurrentSeason(), pools: [Pool("family", Now.AddDays(5), "g1")], entries: entries), SetupStepKey.MarkPayments);

            step.State.Should().Be(SetupStepState.Done);
            step.Detail.Should().Be("All 2 entries paid");
        }

        [Fact]
        public void Build_ShouldMarkPaymentsNotStarted_WhenEntriesExistAndNoneIsPaid()
        {
            var step = Step(Build(CurrentSeason(), pools: [Pool("family", Now.AddDays(5), "g1")], entries: [Entry("family")]), SetupStepKey.MarkPayments);

            step.State.Should().Be(SetupStepState.NotStarted);
            step.Detail.Should().Be("0 of 1 entry paid");
        }
    }
}
