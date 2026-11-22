using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// The four caller tiers used by the endpoint sign-in and role tests.
    /// Anonymous means no x-ms-client-principal header at all.
    /// </summary>
    public enum Tier
    {
        Anonymous,
        Player,
        Admin,
        SuperAdmin
    }

    /// <summary>
    /// Shared fakes for the EndpointAuth* test classes: a seeded user repository, fake
    /// HttpRequestData / HttpResponseData (with a working JSON serializer so success paths
    /// can run to the end), and a DefaultHttpContext request for the ASP.NET style functions.
    /// Nothing here touches a real service, Cosmos or a secret.
    /// </summary>
    public static class EndpointKit
    {
        public const string PlayerId = "player-1";
        public const string OtherPlayerId = "player-2";
        public const string AdminId = "admin-1";
        public const string SuperAdminId = "super-1";
        public const string DisabledPlayerId = "player-disabled";
        public const string DisabledSuperAdminId = "super-disabled";

        public static string? IdFor(Tier tier) => tier switch
        {
            Tier.Anonymous => null,
            Tier.Player => PlayerId,
            Tier.Admin => AdminId,
            Tier.SuperAdmin => SuperAdminId,
            _ => null
        };

        /// <summary>User repository with one profile per tier, a second player and two disabled accounts.</summary>
        public static Mock<IUserRepository> Users()
        {
            var profiles = new Dictionary<string, UserProfile>
            {
                [PlayerId] = new() { Id = PlayerId, Email = "player1@test.com", AppRole = Constants.Roles.Player },
                [OtherPlayerId] = new() { Id = OtherPlayerId, Email = "player2@test.com", AppRole = Constants.Roles.Player },
                [AdminId] = new() { Id = AdminId, Email = "admin@test.com", AppRole = Constants.Roles.Admin },
                [SuperAdminId] = new() { Id = SuperAdminId, Email = "super@test.com", AppRole = Constants.Roles.SuperAdmin },
                [DisabledPlayerId] = new() { Id = DisabledPlayerId, Email = "off@test.com", AppRole = Constants.Roles.Player, IsDisabled = true },
                [DisabledSuperAdminId] = new() { Id = DisabledSuperAdminId, Email = "offsuper@test.com", AppRole = Constants.Roles.SuperAdmin, IsDisabled = true }
            };

            var repo = new Mock<IUserRepository>();
            repo.Setup(r => r.GetUserAsync(It.IsAny<string>()))
                .ReturnsAsync((string id) => profiles.TryGetValue(id, out var p) ? p : null);
            repo.Setup(r => r.UpsertUserAsync(It.IsAny<UserProfile>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.GetUsersAsync()).ReturnsAsync(profiles.Values.ToList());
            return repo;
        }

        public static string Principal(string userId, string userDetails = "someone@test.com") =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                userId,
                userDetails,
                identityProvider = "aad",
                userRoles = new[] { "authenticated" }
            })));

        public static HttpRequestData Request(Tier tier, string query = "", object? body = null) =>
            Request(IdFor(tier), query, body);

        /// <summary>Fake Functions request. A null userId means no x-ms-client-principal header.</summary>
        public static HttpRequestData Request(string? userId, string query = "", object? body = null, string? userDetails = null)
        {
            var services = new ServiceCollection();
            services.AddOptions<WorkerOptions>().Configure(o => o.Serializer = new JsonObjectSerializer());
            var ctxMock = new Mock<FunctionContext>();
            ctxMock.SetupGet(c => c.InstanceServices).Returns(services.BuildServiceProvider());
            var ctx = ctxMock.Object;

            var headers = new HttpHeadersCollection();
            if (userId != null)
                headers.Add("x-ms-client-principal", userDetails == null ? Principal(userId) : Principal(userId, userDetails));

            var requestMock = new Mock<HttpRequestData>(ctx);
            requestMock.Setup(r => r.Headers).Returns(headers);
            var url = new Uri("https://localhost/api/test" + query);
            requestMock.Setup(r => r.Url).Returns(url);
            requestMock.Setup(r => r.Query).Returns(System.Web.HttpUtility.ParseQueryString(url.Query));
            var bodyBytes = body == null ? Array.Empty<byte>() : JsonSerializer.SerializeToUtf8Bytes(body);
            requestMock.Setup(r => r.Body).Returns(new MemoryStream(bodyBytes));

            var responseMock = new Mock<HttpResponseData>(ctx);
            responseMock.SetupProperty(r => r.StatusCode);
            responseMock.SetupProperty(r => r.Headers, new HttpHeadersCollection());
            responseMock.Setup(r => r.Body).Returns(new MemoryStream());
            requestMock.Setup(r => r.CreateResponse()).Returns(responseMock.Object);

            return requestMock.Object;
        }

        public static string BodyText(HttpResponseData response)
        {
            response.Body.Position = 0;
            return new StreamReader(response.Body).ReadToEnd();
        }

        /// <summary>Request for the functions that take the ASP.NET HttpRequest and return IActionResult.</summary>
        public static HttpRequest AspRequest(string? userId, object? body = null)
        {
            var http = new DefaultHttpContext();
            if (userId != null)
                http.Request.Headers["x-ms-client-principal"] = Principal(userId);
            var bodyBytes = body == null ? Array.Empty<byte>() : JsonSerializer.SerializeToUtf8Bytes(body);
            http.Request.Body = new MemoryStream(bodyBytes);
            return http.Request;
        }

        public static HttpRequest AspRequest(Tier tier, object? body = null) => AspRequest(IdFor(tier), body);

        /// <summary>HTTP status of an IActionResult; 200 for results that carry none (file downloads).</summary>
        public static int StatusOf(IActionResult result) => result switch
        {
            StatusCodeResult s => s.StatusCode,
            ObjectResult o => o.StatusCode ?? 200,
            FileResult => 200,
            _ => -1
        };

        public static int Status(HttpResponseData response) => (int)response.StatusCode;
    }
}
