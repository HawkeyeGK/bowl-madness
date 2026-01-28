using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using BowlPoolManager.Core.Dtos;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core;
using BowlPoolManager.Api.Helpers;

namespace BowlPoolManager.Api.Functions
{
    public class MigrationFunctions
    {
        private readonly ILogger<MigrationFunctions> _logger;
        //private readonly IMigrationRepository _migrationRepository;
        //private readonly IEntryRepository _entryRepository;
        //private readonly IUserRepository _userRepository;

        public MigrationFunctions(ILogger<MigrationFunctions> logger)
        {
            _logger = logger;
        }

        [Function("MigrationPing")]
        public IActionResult MigrationPing([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "migration/ping")] HttpRequest req)
        {
            return new OkObjectResult("Pong");
        }

        /*
        [Function("AnalyzeLegacyData")]
        public async Task<IActionResult> AnalyzeLegacyData([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/migration/analyze")] HttpRequest req)
        {
             // ...
             return new StatusCodeResult(StatusCodes.Status501NotImplemented);
        }

        [Function("ExecuteMigration")]
        public async Task<IActionResult> ExecuteMigration([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/migration/execute")] HttpRequest req)
        {
             // ...
             return new StatusCodeResult(StatusCodes.Status501NotImplemented);
        }
        */
    }
}
