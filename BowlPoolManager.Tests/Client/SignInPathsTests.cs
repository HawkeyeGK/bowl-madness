using FluentAssertions;
using System.Text.Json;

namespace BowlPoolManager.Tests.Client
{
    /// <summary>
    /// Release 2 group A: every sign-in path ends at the chooser page (/login), never straight at Google.
    /// Reads the real routing file, App.razor and the chooser page from the repository.
    /// </summary>
    public class SignInPathsTests
    {
        private static string RepoFile(params string[] parts)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "BowlPoolManager.sln")))
                dir = dir.Parent;
            dir.Should().NotBeNull("the repository root holds BowlPoolManager.sln");
            return Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
        }

        private static string ConfigText() =>
            File.ReadAllText(RepoFile("BowlPoolManager.Client", "wwwroot", "staticwebapp.config.json"));

        [Fact]
        public void RoutingFile_ShouldNotRewriteLoginToGoogle()
        {
            var routes = JsonDocument.Parse(ConfigText()).RootElement.GetProperty("routes").EnumerateArray();

            routes.Where(r => r.GetProperty("route").GetString() == "/login" && r.TryGetProperty("rewrite", out _))
                .Should().BeEmpty("/login must reach the chooser page, which is part of the app");
        }

        [Fact]
        public void RoutingFile_ShouldSendA401ToTheChooser()
        {
            var target = JsonDocument.Parse(ConfigText()).RootElement
                .GetProperty("responseOverrides").GetProperty("401").GetProperty("redirect").GetString();

            target.Should().Be("/login");
        }

        [Fact]
        public void RoutingFile_ShouldNotNameGoogleAnywhere()
        {
            ConfigText().Should().NotContainEquivalentOf("google");
        }

        [Fact]
        public void NotAuthorizedLink_ShouldNotGoStraightToGoogle()
        {
            File.ReadAllText(RepoFile("BowlPoolManager.Client", "App.razor"))
                .Should().NotContain(".auth/login/google");
        }

        // Blazor treats a plain link inside the app as a page of the app: "/.auth/..." would end at "Page not found"
        // instead of reaching Azure. These addresses must be loaded by the browser (forceLoad).
        [Fact]
        public void AuthAddresses_ShouldNotBePlainLinks_InAnyPage()
        {
            var root = Path.GetDirectoryName(RepoFile("BowlPoolManager.Client", "App.razor"))!;
            var plain = Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .SelectMany(f => File.ReadAllLines(f).Select((l, i) => (f, l, i + 1)))
                .Where(x => System.Text.RegularExpressions.Regex.IsMatch(x.l, "<a\\b[^>]*href=\"/\\.auth/"))
                .Select(x => $"{Path.GetFileName(x.f)}:{x.Item3}")
                .ToList();

            plain.Should().BeEmpty("a plain <a href=\"/.auth/...\"> is routed inside the app (Page not found)");
        }

        [Theory]
        [InlineData("PrimaryButton.razor")]
        [InlineData("SecondaryButton.razor")]
        public void LinkButtons_ShouldLoadAuthAddressesOutsideTheApp(string file)
        {
            File.ReadAllText(RepoFile("BowlPoolManager.Client", "Ui", file)).Should().Contain("ClickHandler").And.Contain("LinkHref");
        }

        [Fact]
        public void Chooser_ShouldOfferGoogleAndMicrosoftInPlainWords()
        {
            var page = File.ReadAllText(RepoFile("BowlPoolManager.Client", "Pages", "Login.razor"));

            page.Should().Contain("/.auth/login/google").And.Contain("/.auth/login/aad");
            page.Should().Contain("Sign in with your Gmail account");
            page.Should().Contain("Sign in with your Outlook, Hotmail or Live account");
            page.Should().Contain("never sees your password");
        }
    }
}
