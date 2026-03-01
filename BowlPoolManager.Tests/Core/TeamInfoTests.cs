using Xunit;
using FluentAssertions;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Core
{
    public class TeamInfoTests
    {
        // ── DisplayName ─────────────────────────────────────────────────────────

        [Fact]
        public void DisplayName_ShouldCombineSchoolAndMascot()
        {
            var team = new TeamInfo { School = "Ohio State", Mascot = "Buckeyes" };
            team.DisplayName.Should().Be("Ohio State Buckeyes");
        }

        [Fact]
        public void DisplayName_ShouldHandleEmptyMascot()
        {
            var team = new TeamInfo { School = "Navy", Mascot = "" };
            team.DisplayName.Should().Be("Navy ");
        }

        // ── PrimaryLogoUrl ───────────────────────────────────────────────────────

        [Fact]
        public void PrimaryLogoUrl_ShouldReturnNonDarkLogo_WhenBothTypesAvailable()
        {
            var team = new TeamInfo
            {
                Logos = new List<string>
                {
                    "https://cdn.example.com/ohio-state-dark.png",
                    "https://cdn.example.com/ohio-state.png"
                }
            };

            team.PrimaryLogoUrl.Should().Be("https://cdn.example.com/ohio-state.png");
        }

        [Fact]
        public void PrimaryLogoUrl_ShouldFallbackToFirstLogo_WhenAllAreDark()
        {
            var team = new TeamInfo
            {
                Logos = new List<string>
                {
                    "https://cdn.example.com/logo-dark-1.png",
                    "https://cdn.example.com/logo-dark-2.png"
                }
            };

            team.PrimaryLogoUrl.Should().Be("https://cdn.example.com/logo-dark-1.png");
        }

        [Fact]
        public void PrimaryLogoUrl_ShouldReturnEmpty_WhenLogosIsNull()
        {
            var team = new TeamInfo { Logos = null };
            team.PrimaryLogoUrl.Should().BeEmpty();
        }

        [Fact]
        public void PrimaryLogoUrl_ShouldReturnEmpty_WhenLogosIsEmpty()
        {
            var team = new TeamInfo { Logos = new List<string>() };
            team.PrimaryLogoUrl.Should().BeEmpty();
        }

        [Fact]
        public void PrimaryLogoUrl_ShouldReturnExplicitValue_WhenSetDirectly()
        {
            // When the backing field is set (e.g. from a denormalized object), it takes priority over Logos.
            var team = new TeamInfo
            {
                Logos = new List<string> { "https://cdn.example.com/from-logos.png" },
                PrimaryLogoUrl = "https://cdn.example.com/explicit.png"
            };

            team.PrimaryLogoUrl.Should().Be("https://cdn.example.com/explicit.png");
        }

        // ── DarkLogoUrl ──────────────────────────────────────────────────────────

        [Fact]
        public void DarkLogoUrl_ShouldReturnDarkLogo_WhenAvailable()
        {
            var team = new TeamInfo
            {
                Logos = new List<string>
                {
                    "https://cdn.example.com/ohio-state.png",
                    "https://cdn.example.com/ohio-state-dark.png"
                }
            };

            team.DarkLogoUrl.Should().Be("https://cdn.example.com/ohio-state-dark.png");
        }

        [Fact]
        public void DarkLogoUrl_ShouldReturnNull_WhenNoDarkLogoExists()
        {
            var team = new TeamInfo
            {
                Logos = new List<string> { "https://cdn.example.com/ohio-state.png" }
            };

            team.DarkLogoUrl.Should().BeNull();
        }

        [Fact]
        public void DarkLogoUrl_ShouldReturnNull_WhenLogosIsNull()
        {
            var team = new TeamInfo { Logos = null };
            team.DarkLogoUrl.Should().BeNull();
        }

        [Fact]
        public void DarkLogoUrl_ShouldReturnExplicitValue_WhenSetDirectly()
        {
            var team = new TeamInfo
            {
                Logos = new List<string> { "https://cdn.example.com/logo-dark.png" },
                DarkLogoUrl = "https://cdn.example.com/explicit-dark.png"
            };

            team.DarkLogoUrl.Should().Be("https://cdn.example.com/explicit-dark.png");
        }

        // ── ToDenormalized ───────────────────────────────────────────────────────

        [Fact]
        public void ToDenormalized_ShouldCaptureComputedLogoUrls_AndNullLogos()
        {
            var team = new TeamInfo
            {
                School = "Alabama",
                Mascot = "Crimson Tide",
                Logos = new List<string>
                {
                    "https://cdn.example.com/alabama.png",
                    "https://cdn.example.com/alabama-dark.png"
                }
            };

            var denorm = team.ToDenormalized();

            denorm.PrimaryLogoUrl.Should().Be("https://cdn.example.com/alabama.png");
            denorm.DarkLogoUrl.Should().Be("https://cdn.example.com/alabama-dark.png");
            denorm.Logos.Should().BeNull(); // Source array stripped
        }

        [Fact]
        public void ToDenormalized_ShouldCopyAllScalarProperties()
        {
            var team = new TeamInfo
            {
                SchoolId = 42,
                School = "Clemson",
                Mascot = "Tigers",
                Abbreviation = "CLEM",
                Conference = "ACC",
                Color = "#F56600",
                AltColor = "#522D80",
                Logos = new List<string> { "https://cdn.example.com/clemson.png" }
            };

            var denorm = team.ToDenormalized();

            denorm.SchoolId.Should().Be(42);
            denorm.School.Should().Be("Clemson");
            denorm.Mascot.Should().Be("Tigers");
            denorm.Abbreviation.Should().Be("CLEM");
            denorm.Conference.Should().Be("ACC");
            denorm.Color.Should().Be("#F56600");
            denorm.AltColor.Should().Be("#522D80");
        }
    }
}
