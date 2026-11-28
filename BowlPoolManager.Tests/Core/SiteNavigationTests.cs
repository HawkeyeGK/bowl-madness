using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;
using FluentAssertions;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>Release 2 group A: the four bottom tabs and the menu links, per site.</summary>
    public class SiteNavigationTests
    {
        [Theory]
        [InlineData(Sport.Football)]
        [InlineData(Sport.Basketball)]
        public void Tabs_ShouldBeLeaderboardScoresWhatIfMyPicks_OnBothSites(Sport sport)
        {
            SiteNavigation.Tabs(sport).Select(t => t.Label)
                .Should().Equal("Leaderboard", "Scores", "What If", "My Picks");
        }

        [Theory]
        [InlineData(Sport.Football, "mypicks")]
        [InlineData(Sport.Basketball, "hoops/mypicks")]
        public void Tabs_ShouldSayEnterPicksAtTheSameAddress_WhenThePlayerNeedsPicks(Sport sport, string href)
        {
            var tabs = SiteNavigation.Tabs(sport, needsPicks: true);

            tabs.Select(t => t.Label).Should().Equal("Leaderboard", "Scores", "What If", "Enter Picks");
            tabs.Last().Href.Should().Be(href);
            tabs.Last().Key.Should().Be("picks");
        }

        [Fact]
        public void Tabs_ShouldPointAtFootballPages_WhenFootball()
        {
            SiteNavigation.Tabs(Sport.Football).Select(t => t.Href)
                .Should().Equal("", "results", "what-if", "mypicks");
        }

        [Fact]
        public void Tabs_ShouldPointAtHoopsPages_WhenBasketball()
        {
            SiteNavigation.Tabs(Sport.Basketball).Select(t => t.Href)
                .Should().Equal("hoops/leaderboard", "hoops/scoreboard", "hoops/what-if", "hoops/mypicks");
        }

        [Fact]
        public void Tabs_ShouldMatchHomeExactly_WhenFootballLeaderboard()
        {
            SiteNavigation.Tabs(Sport.Football).First().MatchExact.Should().BeTrue();
            SiteNavigation.Tabs(Sport.Football).Skip(1).Should().OnlyContain(t => !t.MatchExact);
        }

        [Fact]
        public void MenuLinks_ShouldIncludeRulesOnlyForFootball_PerSport()
        {
            // Hoops gets its own rules in Phase 3; until then the hoops site has no Rules link (issue 34).
            SiteNavigation.MenuLinks(Sport.Football).Select(t => (t.Label, t.Href))
                .Should().Equal(("History", "archives"), ("Print Bracket", "print-bracket"), ("Rules", "rules"));
            SiteNavigation.MenuLinks(Sport.Basketball).Select(t => (t.Label, t.Href))
                .Should().Equal(("History", "hoops/archives"), ("Print Bracket", "hoops/print-bracket"));
        }

        [Fact]
        public void Keys_ShouldBeUniqueWithinTabsAndMenu()
        {
            var all = SiteNavigation.Tabs(Sport.Football).Concat(SiteNavigation.MenuLinks(Sport.Football));
            all.Select(i => i.Key).Should().OnlyHaveUniqueItems();
        }

        [Theory]
        [InlineData(Sport.Football, "Bowl Madness")]
        [InlineData(Sport.Basketball, "Hoops Madness")]
        public void SiteName_ShouldNameTheSite(Sport sport, string expected)
        {
            SiteNavigation.SiteName(sport).Should().Be(expected);
        }
    }
}
