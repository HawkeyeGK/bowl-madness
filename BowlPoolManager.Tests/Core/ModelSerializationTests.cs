using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using BowlPoolManager.Core.Domain;
using SystemJson = System.Text.Json;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>
    /// Safety net for the .NET 10 upgrade: every model stored in Cosmos must save and reload with both
    /// Newtonsoft (what the Cosmos SDK uses, with its camelCase naming policy) and System.Text.Json
    /// (what the client uses), and both must agree on the property names.
    /// </summary>
    public class ModelSerializationTests
    {
        // ── Sample objects: every property set to a non-default value ────────────

        private static readonly DateTime T1 = new(2026, 12, 20, 18, 30, 0, DateTimeKind.Utc);
        private static readonly DateTime T2 = new(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc);

        private static TeamInfo Team(int id, string school) => new()
        {
            SchoolId = id,
            School = school,
            Mascot = "Herons",
            Abbreviation = "TEST",
            Conference = "Lakes",
            Color = "#112233",
            AltColor = "#445566",
            Logos = new List<string> { "https://example.test/logo.png", "https://example.test/logo-dark.png" },
            PrimaryLogoUrl = "https://example.test/logo.png",
            DarkLogoUrl = "https://example.test/logo-dark.png"
        };

        private static BowlGame SampleBowlGame() => new()
        {
            Id = "game-1", SeasonId = "season-1", BowlName = "Test Bowl", ExternalId = "401000001",
            ApiHomeTeam = "Cedar Falls Herons", ApiAwayTeam = "Northgate Badgers", StartTime = T1,
            Status = GameStatus.InProgress, GameDetail = "3rd • 8:42",
            TeamHome = "Cedar Falls", TeamHomeId = 11, HomeTeamInfo = Team(11, "Cedar Falls"),
            TeamAway = "Northgate", TeamAwayId = 12, AwayTeamInfo = Team(12, "Northgate"),
            TeamHomeSeed = 5, TeamAwaySeed = 12, TeamHomeScore = 24, TeamAwayScore = 17,
            PointValue = 3, IsPlayoff = true, Round = TournamentRound.QuarterFinal, NextGameId = "game-2",
            Location = "Test Stadium", Television = "TestNet"
        };

        private static BowlPool SamplePool() => new()
        {
            Id = "pool-1", SeasonId = "season-1", Name = "Family", Season = 2026,
            GameIds = new List<string> { "game-1", "game-2" }, LockDate = T1, InviteCode = "FAMILY26",
            IsConcluded = true, IsArchived = true, TieBreakerGameId = "game-2",
            PrimaryTieBreaker = TieBreakerMetric.ScoreDelta, SecondaryTieBreaker = TieBreakerMetric.CorrectPickCount
        };

        private static BracketEntry SampleEntry() => new()
        {
            Id = "entry-1", SeasonId = "season-1", PoolId = "pool-1", UserId = "user-1", PlayerName = "Avery's Picks",
            Picks = new Dictionary<string, string> { ["game-1"] = "Cedar Falls", ["game-2"] = "5 Northgate" },
            TieBreakerPoints = 55, CreatedOn = T2, IsPaid = true, IsAdminEntered = true,
            AuditLog = new List<string> { "2026-11-01 created", "2026-11-02 marked paid" }
        };

        private static Season SampleSeason() => new()
        {
            Id = "season-1", SeasonId = "2026", Name = "2026-2027 Season", IsCurrent = true
        };

        private static UserProfile SampleUser() => new()
        {
            Id = "user-1", Email = "someone@example.test", DisplayName = "Someone", AppRole = "Admin",
            IsDisabled = true, CreatedOn = T2
        };

        private static PoolArchive SampleArchive() => new()
        {
            Id = "Archive_pool-1", PoolId = "pool-1", PoolName = "Family", SeasonId = "season-1", Season = 2026,
            ArchivedOn = T1,
            Games = new List<ArchiveGame>
            {
                new()
                {
                    GameId = "game-1", StartTime = T1, BowlName = "Test Bowl", Round = TournamentRound.Sweet16,
                    Region = "East", TeamHomeSeed = 1, TeamAwaySeed = 16, TeamHome = "Cedar Falls", TeamHomeScore = 70,
                    TeamAway = "Northgate", TeamAwayScore = 60, PointValue = 4
                }
            },
            Standings = new List<ArchiveStanding>
            {
                new()
                {
                    PlayerName = "Avery's Picks", Rank = 1, TotalPoints = 40, CorrectPicks = 12, TieBreakerPoints = 55,
                    TieBreakerDelta = 3, Picks = new Dictionary<string, string> { ["game-1"] = "Cedar Falls" }
                }
            }
        };

        private static HoopsGame SampleHoopsGame() => new()
        {
            Id = "hgame-1", SeasonId = "2026", Round = TournamentRound.Elite8, Region = "East", ExternalId = "500000001",
            ApiHomeTeam = "Cedar Falls", ApiAwayTeam = "Northgate", StartTime = T1, Television = "TestNet",
            Status = GameStatus.Final, GameDetail = "Final",
            TeamHome = "Cedar Falls", TeamHomeId = 11, TeamHomeSeed = 1, HomeTeamInfo = Team(11, "Cedar Falls"),
            TeamAway = "Northgate", TeamAwayId = 12, TeamAwaySeed = 8, AwayTeamInfo = Team(12, "Northgate"),
            TeamHomeScore = 80, TeamAwayScore = 71, PointValue = 8, NextGameId = "hgame-2", SeedMatchup = "1v8"
        };

        private static HoopsPool SampleHoopsPool() => new()
        {
            Id = "hpool-1", SeasonId = "2026", Name = "Hoops", Season = 2026, GameIds = new List<string> { "hgame-1" },
            LockDate = T1, InviteCode = "HOOPS26", IsConcluded = true, IsArchived = true,
            PointsPerRound = new Dictionary<TournamentRound, int>
            {
                [TournamentRound.FirstFour] = 0, [TournamentRound.RoundOf64] = 1, [TournamentRound.NationalChampionship] = 32
            }
        };

        private static TeamConfig SampleTeamConfig() => new()
        {
            Id = "Config_Teams_FBS", Teams = new List<TeamInfo> { Team(11, "Cedar Falls"), Team(12, "Northgate") },
            LastUpdated = T2
        };

        public static IEnumerable<object[]> Samples() => new[]
        {
            new object[] { "BowlGame", SampleBowlGame() },
            new object[] { "BowlPool", SamplePool() },
            new object[] { "BracketEntry", SampleEntry() },
            new object[] { "Season", SampleSeason() },
            new object[] { "UserProfile", SampleUser() },
            new object[] { "PoolArchive", SampleArchive() },
            new object[] { "HoopsGame", SampleHoopsGame() },
            new object[] { "HoopsPool", SampleHoopsPool() },
            new object[] { "TeamConfig", SampleTeamConfig() }
        };

        // ── Serializers ───────────────────────────────────────────────────────────

        // The Cosmos SDK uses Newtonsoft with its camelCase naming policy and no date parsing.
        private static readonly JsonSerializerSettings CosmosLike = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateParseHandling = DateParseHandling.None
        };

        private static string ToNewtonsoft(object o) => JsonConvert.SerializeObject(o, CosmosLike);
        private static T FromNewtonsoft<T>(string json) => JsonConvert.DeserializeObject<T>(json, CosmosLike)!;
        private static string ToSystem(object o) => SystemJson.JsonSerializer.Serialize(o, o.GetType());
        private static T FromSystem<T>(string json) => SystemJson.JsonSerializer.Deserialize<T>(json)!;

        private static object Reload(object sample, Func<string, Type, object> read, Func<object, string> write) =>
            read(write(sample), sample.GetType());

        private static object NewtonsoftRead(string json, Type t) => JsonConvert.DeserializeObject(json, t, CosmosLike)!;
        private static object SystemRead(string json, Type t) => SystemJson.JsonSerializer.Deserialize(json, t)!;

        // ── Round trips ───────────────────────────────────────────────────────────

        [Theory]
        [MemberData(nameof(Samples))]
        public void Newtonsoft_ShouldReloadSameModel_WhenSavedWithCosmosSettings(string name, object sample)
        {
            var reloaded = Reload(sample, NewtonsoftRead, ToNewtonsoft);

            reloaded.Should().BeEquivalentTo(sample, name);
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void SystemTextJson_ShouldReloadSameModel_WhenSavedAndLoaded(string name, object sample)
        {
            var reloaded = Reload(sample, SystemRead, ToSystem);

            reloaded.Should().BeEquivalentTo(sample, name);
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void SystemTextJson_ShouldLoadWhatNewtonsoftSaved_WhenModelIsStoredInCosmos(string name, object sample)
        {
            var reloaded = SystemRead(ToNewtonsoft(sample), sample.GetType());

            reloaded.Should().BeEquivalentTo(sample, name);
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void Newtonsoft_ShouldLoadWhatSystemTextJsonSaved_WhenClientSendsModelToApi(string name, object sample)
        {
            var reloaded = NewtonsoftRead(ToSystem(sample), sample.GetType());

            reloaded.Should().BeEquivalentTo(sample, name);
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void BothLibraries_ShouldWriteSamePropertyNames_WhenModelIsSaved(string name, object sample)
        {
            var newtonsoftKeys = KeyPaths(JToken.Parse(ToNewtonsoft(sample)));
            var systemKeys = KeyPaths(JToken.Parse(ToSystem(sample)));

            systemKeys.Should().BeEquivalentTo(newtonsoftKeys, name);
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void Newtonsoft_ShouldWriteSameNamesWithOrWithoutNamingPolicy_WhenModelsNameEveryProperty(string name, object sample)
        {
            // If a property has no explicit name, the naming policy decides it. Cosmos applies camelCase;
            // a plain Newtonsoft call does not. Models that name every property give the same JSON either way.
            if (name == "UserProfile") return; // see UserProfile_ShouldCarryBothJsonAttributes_OnEveryProperty

            var withPolicy = KeyPaths(JToken.Parse(ToNewtonsoft(sample)));
            var withoutPolicy = KeyPaths(JToken.Parse(JsonConvert.SerializeObject(sample)));

            withoutPolicy.Should().BeEquivalentTo(withPolicy, name);
        }

        // ── Attribute rule (CLAUDE.md: both attributes on every stored property) ──

        public static IEnumerable<object[]> ModelTypes() => new[]
        {
            typeof(BowlGame), typeof(BowlPool), typeof(BracketEntry), typeof(Season), typeof(PoolArchive),
            typeof(ArchiveGame), typeof(ArchiveStanding), typeof(HoopsGame), typeof(HoopsPool), typeof(TeamConfig),
            typeof(TeamInfo)
        }.Select(t => new object[] { t });

        private static IEnumerable<PropertyInfo> StoredProperties(Type t) =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p =>
                p.GetCustomAttribute<Newtonsoft.Json.JsonIgnoreAttribute>() == null &&
                p.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() == null);

        [Theory]
        [MemberData(nameof(ModelTypes))]
        public void Model_ShouldCarryBothJsonAttributesWithTheSameName_OnEveryStoredProperty(Type type)
        {
            foreach (var p in StoredProperties(type))
            {
                var newtonsoft = p.GetCustomAttribute<JsonPropertyAttribute>();
                var system = p.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>();

                newtonsoft.Should().NotBeNull($"{type.Name}.{p.Name} needs [JsonProperty]");
                system.Should().NotBeNull($"{type.Name}.{p.Name} needs [JsonPropertyName]");
                newtonsoft!.PropertyName.Should().Be(system!.Name, $"{type.Name}.{p.Name} names must match");
            }
        }

        [Fact]
        [Trait("Category", "KnownFailure")] // No issue yet: UserProfile.Email, DisplayName and CreatedOn lack [JsonProperty].
        public void UserProfile_ShouldCarryBothJsonAttributes_OnEveryProperty()
        {
            foreach (var p in StoredProperties(typeof(UserProfile)))
            {
                p.GetCustomAttribute<JsonPropertyAttribute>().Should().NotBeNull($"UserProfile.{p.Name} needs [JsonProperty]");
                p.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>().Should().NotBeNull($"UserProfile.{p.Name} needs [JsonPropertyName]");
            }
        }

        // ── Documents in today's stored format still load ────────────────────────
        // Values are made up. The shape (including the Cosmos system properties and numeric enums) is today's.

        private const string SystemProps = "\"_rid\":\"AAAA\",\"_self\":\"dbs/x/colls/y/docs/z/\",\"_etag\":\"\\\"1\\\"\",\"_attachments\":\"attachments/\",\"_ts\":1767402319";

        private static void LoadsWithBothLibraries<T>(string json, Action<T> assert)
        {
            assert(FromNewtonsoft<T>(json));
            assert(FromSystem<T>(json));
        }

        [Fact]
        public void BowlGame_ShouldLoad_WhenDocumentIsInTodaysStoredFormat()
        {
            var json = "{\"id\":\"g1\",\"seasonId\":\"s1\",\"bowlName\":\"Test Bowl\",\"externalId\":\"401778333\"," +
                       "\"apiHomeTeam\":\"A Herons\",\"apiAwayTeam\":\"B Badgers\",\"startTime\":\"2026-01-02T21:30:00Z\"," +
                       "\"gameStatus\":2,\"gameDetail\":\"Final\",\"teamHome\":\"A\",\"teamAway\":\"B\",\"teamHomeSeed\":null," +
                       "\"teamAwaySeed\":null,\"teamHomeScore\":35,\"teamAwayScore\":13,\"pointValue\":1,\"isPlayoff\":false," +
                       "\"round\":0,\"nextGameId\":null,\"location\":\"Test Stadium\",\"television\":\"TestNet\"," +
                       "\"type\":\"BowlGame\"," + SystemProps + "}";

            LoadsWithBothLibraries<BowlGame>(json, g =>
            {
                g.Id.Should().Be("g1");
                g.Status.Should().Be(GameStatus.Final);
                g.Round.Should().Be(TournamentRound.Standard);
                g.StartTime.Should().Be(new DateTime(2026, 1, 2, 21, 30, 0, DateTimeKind.Utc));
                g.TeamHomeScore.Should().Be(35);
                g.TeamHomeSeed.Should().BeNull();
                g.WinningTeamName.Should().Be("A");
            });
        }

        [Fact]
        public void BowlGame_ShouldLoad_WhenPlayoffDocumentHasRoundAndNextGame()
        {
            var json = "{\"id\":\"g2\",\"seasonId\":\"s1\",\"bowlName\":\"Playoffs Round 1\",\"gameStatus\":0,\"teamHome\":\"8 A\"," +
                       "\"teamAway\":\"9 B\",\"teamHomeSeed\":8,\"teamAwaySeed\":9,\"pointValue\":3,\"isPlayoff\":true,\"round\":1," +
                       "\"nextGameId\":\"g3\",\"type\":\"BowlGame\"," + SystemProps + "}";

            LoadsWithBothLibraries<BowlGame>(json, g =>
            {
                g.Round.Should().Be(TournamentRound.Round1);
                g.IsPlayoff.Should().BeTrue();
                g.NextGameId.Should().Be("g3");
                g.TeamAwaySeed.Should().Be(9);
            });
        }

        [Fact]
        public void BracketEntry_ShouldLoad_WhenDocumentIsInTodaysStoredFormat()
        {
            var json = "{\"id\":\"e1\",\"seasonId\":\"s1\",\"poolId\":\"p1\",\"userId\":\"u1\",\"playerName\":\"Avery's Picks\"," +
                       "\"picks\":{\"g1\":\"A\",\"g2\":\"8 B\"},\"tieBreakerPoints\":50,\"createdOn\":\"2026-11-01T05:00:00Z\"," +
                       "\"isPaid\":true,\"isAdminEntered\":false,\"type\":\"BracketEntry\",\"auditLog\":[\"created\"]," + SystemProps + "}";

            LoadsWithBothLibraries<BracketEntry>(json, e =>
            {
                e.PlayerName.Should().Be("Avery's Picks");
                e.Picks.Should().Contain("g2", "8 B");
                e.IsPaid.Should().BeTrue();
                e.CreatedOn.Should().Be(new DateTime(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc));
                e.AuditLog.Should().ContainSingle();
            });
        }

        [Fact]
        public void BracketEntry_ShouldLoadWithNullPicks_WhenPicksAreRedacted()
        {
            var json = "{\"id\":\"e1\",\"seasonId\":\"s1\",\"poolId\":\"p1\",\"userId\":\"u1\",\"playerName\":\"X\",\"picks\":null}";

            LoadsWithBothLibraries<BracketEntry>(json, e => e.Picks.Should().BeNull());
        }

        [Fact]
        public void BowlPool_ShouldLoad_WhenDocumentIsInTodaysStoredFormat()
        {
            var json = "{\"id\":\"p1\",\"seasonId\":\"s1\",\"name\":\"Family\",\"season\":2026,\"gameIds\":[\"g1\",\"g2\"]," +
                       "\"lockDate\":\"2026-12-01T00:00:00Z\",\"inviteCode\":\"FAMILY26\",\"isConcluded\":false,\"isArchived\":false," +
                       "\"tieBreakerGameId\":\"g2\",\"primaryTieBreaker\":0,\"secondaryTieBreaker\":1,\"type\":\"BowlPool\"," + SystemProps + "}";

            LoadsWithBothLibraries<BowlPool>(json, p =>
            {
                p.GameIds.Should().Equal("g1", "g2");
                p.PrimaryTieBreaker.Should().Be(TieBreakerMetric.CorrectPickCount);
                p.SecondaryTieBreaker.Should().Be(TieBreakerMetric.ScoreDelta);
                p.LockDate.Should().Be(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }

        [Fact]
        public void Season_ShouldLoad_WhenDocumentIsInTodaysStoredFormat()
        {
            var json = "{\"id\":\"s1\",\"seasonId\":\"2026\",\"name\":\"2026-2027 Season\",\"isCurrent\":true,\"type\":\"Season\"," + SystemProps + "}";

            LoadsWithBothLibraries<Season>(json, s =>
            {
                s.SeasonId.Should().Be("2026");
                s.IsCurrent.Should().BeTrue();
            });
        }

        [Fact]
        public void UserProfile_ShouldLoad_WhenDocumentIsInTodaysStoredFormat()
        {
            var json = "{\"id\":\"u1\",\"email\":\"someone@example.test\",\"displayName\":\"Someone\",\"appRole\":\"SuperAdmin\"," +
                       "\"isDisabled\":false,\"type\":\"UserProfile\",\"createdOn\":\"2025-10-01T12:00:00Z\"," + SystemProps + "}";

            LoadsWithBothLibraries<UserProfile>(json, u =>
            {
                u.Email.Should().Be("someone@example.test");
                u.AppRole.Should().Be("SuperAdmin");
                u.CreatedOn.Should().Be(new DateTime(2025, 10, 1, 12, 0, 0, DateTimeKind.Utc));
            });
        }

        [Fact]
        public void HoopsPool_ShouldLoadPointsPerRound_WhenRoundsAreStoredByName()
        {
            var json = "{\"id\":\"h1\",\"seasonId\":\"2026\",\"name\":\"Hoops\",\"season\":2026,\"gameIds\":[]," +
                       "\"lockDate\":\"2027-03-18T16:00:00Z\",\"inviteCode\":\"HOOPS26\",\"isConcluded\":false,\"isArchived\":false," +
                       "\"pointsPerRound\":{\"FirstFour\":0,\"RoundOf64\":1,\"NationalChampionship\":32},\"type\":\"HoopsPool\"," + SystemProps + "}";

            LoadsWithBothLibraries<HoopsPool>(json, p =>
            {
                p.PointsPerRound.Should().NotBeNull();
                p.PointsPerRound![TournamentRound.RoundOf64].Should().Be(1);
                p.PointsPerRound[TournamentRound.NationalChampionship].Should().Be(32);
            });
        }

        [Fact]
        public void HoopsGame_ShouldLoad_WhenDocumentIsInTodaysStoredFormat()
        {
            var json = "{\"id\":\"hg1\",\"seasonId\":\"2026\",\"round\":11,\"region\":\"East\",\"gameStatus\":2,\"teamHome\":\"A\"," +
                       "\"teamHomeSeed\":1,\"teamAway\":\"B\",\"teamAwaySeed\":16,\"teamHomeScore\":80,\"teamAwayScore\":60," +
                       "\"pointValue\":1,\"nextGameId\":\"hg2\",\"seedMatchup\":\"1v16\",\"type\":\"HoopsGame\"," + SystemProps + "}";

            LoadsWithBothLibraries<HoopsGame>(json, g =>
            {
                g.Round.Should().Be(TournamentRound.RoundOf64);
                g.Status.Should().Be(GameStatus.Final);
                g.SeedMatchup.Should().Be("1v16");
                g.WinningTeamName.Should().Be("A");
            });
        }

        [Fact]
        public void PoolArchive_ShouldLoad_WhenFootballArchiveHasNoRounds()
        {
            var json = "{\"id\":\"Archive_p1\",\"poolId\":\"p1\",\"poolName\":\"Family\",\"seasonId\":\"s1\",\"season\":2025," +
                       "\"archivedOn\":\"2026-01-20T00:00:00Z\",\"games\":[{\"gameId\":\"g1\",\"startTime\":\"2025-12-20T01:00:00Z\"," +
                       "\"bowlName\":\"Test Bowl\",\"teamHome\":\"A\",\"teamHomeScore\":10,\"teamAway\":\"B\",\"teamAwayScore\":7,\"pointValue\":1}]," +
                       "\"standings\":[{\"playerName\":\"X\",\"rank\":1,\"totalPoints\":9,\"correctPicks\":9,\"tieBreakerPoints\":50," +
                       "\"picks\":{\"g1\":\"A\"}}],\"type\":\"PoolArchive\"," + SystemProps + "}";

            LoadsWithBothLibraries<PoolArchive>(json, a =>
            {
                a.Games.Should().ContainSingle();
                a.Games[0].Round.Should().BeNull();
                a.Games[0].WinningTeamName.Should().Be("A");
                a.Standings[0].TieBreakerDelta.Should().BeNull();
            });
        }

        [Fact]
        public void TeamConfig_ShouldLoad_WhenConfigDocumentIsInTodaysStoredFormat()
        {
            var json = "{\"id\":\"Config_Teams_FBS\",\"type\":\"TeamConfig\",\"teams\":[{\"schoolId\":11,\"school\":\"Cedar Falls\"," +
                       "\"mascot\":\"Herons\",\"abbreviation\":\"CEDA\",\"conference\":\"Lakes\",\"color\":\"#112233\",\"alternateColor\":\"#445566\"," +
                       "\"logos\":[\"https://example.test/a.png\",\"https://example.test/a-dark.png\"]}],\"lastUpdated\":\"2026-10-01T00:00:00Z\"," + SystemProps + "}";

            LoadsWithBothLibraries<TeamConfig>(json, c =>
            {
                c.Teams.Should().ContainSingle();
                c.Teams[0].AltColor.Should().Be("#445566");
                c.Teams[0].PrimaryLogoUrl.Should().Be("https://example.test/a.png");
                c.Teams[0].DarkLogoUrl.Should().Be("https://example.test/a-dark.png");
            });
        }

        [Fact]
        public void TeamInfo_ShouldOmitLogos_WhenDenormalizedIntoAGame()
        {
            var denormalized = Team(11, "Cedar Falls").ToDenormalized();

            var newtonsoft = JObject.Parse(ToNewtonsoft(denormalized));
            var system = JObject.Parse(ToSystem(denormalized));

            newtonsoft.ContainsKey("logos").Should().BeFalse();
            system.ContainsKey("logos").Should().BeFalse();
            newtonsoft["primaryLogoUrl"]!.Value<string>().Should().Be("https://example.test/logo.png");
            system["darkLogoUrl"]!.Value<string>().Should().Be("https://example.test/logo-dark.png");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static readonly HashSet<string> DictionaryProperties = new() { "picks", "pointsPerRound" };

        private static List<string> KeyPaths(JToken token)
        {
            var paths = new List<string>();
            void Walk(JToken t, string prefix)
            {
                switch (t)
                {
                    case JObject o:
                        foreach (var p in o.Properties())
                        {
                            paths.Add(prefix + p.Name);
                            // Dictionary keys are data, not property names. Newtonsoft with the Cosmos camelCase
                            // policy also camel-cases dictionary keys (PointsPerRound: "RoundOf64" becomes "roundOf64");
                            // System.Text.Json reads them back case-insensitively (covered by the round-trip tests).
                            if (DictionaryProperties.Contains(p.Name)) paths.Add(prefix + p.Name + ".*");
                            else Walk(p.Value, prefix + p.Name + ".");
                        }
                        break;
                    case JArray a:
                        foreach (var item in a) Walk(item, prefix + "[].");
                        break;
                }
            }
            Walk(token, string.Empty);
            return paths;
        }
    }
}
