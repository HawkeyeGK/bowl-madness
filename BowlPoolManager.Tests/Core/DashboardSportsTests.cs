using BowlPoolManager.Core.Helpers;
using FluentAssertions;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>Which sports' sections the admin dashboard shows: the site's own sport on the two real domains, both elsewhere.</summary>
    public class DashboardSportsTests
    {
        [Theory]
        [InlineData("bowl-madness.com")]
        [InlineData("www.bowl-madness.com")]
        [InlineData("WWW.Bowl-Madness.com")]
        public void ForHost_ShouldShowFootballOnly_WhenHostIsTheFootballSite(string host)
        {
            DashboardSports.ForHost(host).Should().Be(new DashboardSports(Football: true, Hoops: false));
        }

        [Theory]
        [InlineData("hoops-madness.com")]
        [InlineData("www.hoops-madness.com")]
        [InlineData("hoops-madness.localhost")]
        public void ForHost_ShouldShowHoopsOnly_WhenHostIsTheHoopsSite(string host)
        {
            DashboardSports.ForHost(host).Should().Be(new DashboardSports(Football: false, Hoops: true));
        }

        [Theory]
        [InlineData("zealous-wave-0d1fa5110.4.azurestaticapps.net")]
        [InlineData("zealous-wave-0d1fa5110-190.centralus.4.azurestaticapps.net")]
        [InlineData("localhost")]
        [InlineData("")]
        public void ForHost_ShouldShowBothSports_WhenHostIsNeitherSite(string host)
        {
            DashboardSports.ForHost(host).Should().Be(new DashboardSports(Football: true, Hoops: true));
        }
    }
}
