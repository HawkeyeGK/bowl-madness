# Infrastructure

Facts about where Bowl Madness and Hoops Madness run. No secrets, keys or setting values belong here, only names.
Last checked 2026-10-26. "Unverified" means it was recorded earlier and could not be re-checked.

## Azure resources
| Resource | Name | Notes |
|---|---|---|
| Resource group | rg-bowlmadness-prod | Central US |
| Static Web App | swa-bowlmadness-prod | Central US, Free tier; hosts the Blazor client and the Functions API |
| Cosmos DB account | in the resource group above | Central US, NoSQL API, serverless; database BowlMadnessDb. Find the account with `az cosmosdb list -g rg-bowlmadness-prod` |
| Application Insights | swa-bowlmadness-prod | Central US, workspace-based (Log Analytics, DefaultWorkspace in DefaultResourceGroup-CUS) |

Cosmos containers (partition key): Players (/id), Seasons (/seasonId), Picks (/seasonId), PoolArchives (/seasonId), Configuration (/id), and the legacy MainContainer (/id), confirmed by a read-only container listing on 2026-10-24. Details: ARCHITECTURAL_CONTEXT.md.

## Hosting
- Azure Static Web Apps, Free tier. Default host: polite-glacier-059273d10.3.azurestaticapps.net.
- The Free tier allows two custom domains; both are in use.

## Custom domains and DNS
- bowl-madness.com and hoops-madness.com are custom domains on the Static Web App; both show status Ready in Azure.
- Registrar: Porkbun (unverified; not checkable from the repo or Azure).
- Recorded setup (unverified): each apex domain has an ALIAS record to the default host above; www.bowl-madness.com and www.hoops-madness.com are permanent (301) URL forwards to the apex, path included.
- The site shown is chosen by host name in the client (ARCHITECTURAL_CONTEXT.md Section 11).
- IP addresses are not recorded because they can change.

## Deploys and PR previews
- Source: GitHub HawkeyeGK/bowl-madness (public), branch main.
- One workflow, "Azure Static Web Apps CI/CD" (.github/workflows/azure-static-web-apps-polite-glacier-059273d10.yml).
- A push to main builds and deploys to production. A pull request opened, updated or reopened against main builds and deploys a preview environment for that PR; closing the PR runs a job that removes it.
- Build inputs: app_location BowlPoolManager.Client, api_location BowlPoolManager.Api, output_location wwwroot.
- The deploy uses the GitHub secret AZURE_STATIC_WEB_APPS_API_TOKEN_POLITE_GLACIER_059273D10 (exists; name only).
- The preview address appears in the Build and Deploy job log ("Visit your site at ...") and in `az staticwebapp environment list`, where rows other than `default` are previews. The format is `https://polite-glacier-059273d10-<PR number>.centralus.3.azurestaticapps.net`. The action currently fails to post a comment on the PR, so do not rely on one.
- The preview limit on the Free tier is unverified.
- No PR was open when checked, so only the default environment existed.

## App settings (names only)
Read by the code:
| Name | Used for |
|---|---|
| CosmosDbConnectionString | Cosmos connection (also accepted as a connection-string entry of the same name) |
| CfbdApiKey | College Football Data API |
| CfbdApiKeyHoops | College Basketball Data API |
| BootstrapAdminEmail | Email that becomes the first SuperAdmin |
| APPLICATIONINSIGHTS_CONNECTION_STRING | Read by the Application Insights SDK (not by app code directly) |

Which of these are set in Azure is unverified. Locally they come from BowlPoolManager.Api/local.settings.json, which is git-ignored.
The browser Application Insights snippet in wwwroot/index.html carries its own connection string; it is public by design and is not repeated here.

## Application Insights
- API: wired (Program.cs and host.json). Information logs from app code reach Application Insights through AllowAppInformationLogs (categories under BowlPoolManager.Api); framework categories stay at Warning. Before this, the SDK default sent only Warning and above, so function log messages were missing. Client: JavaScript SDK loader in index.html (commit a7b1f3f). enableAutoRouteTracking: true is set in the loader, so each Blazor route change sends a page view.
- Limits (read 2026-10-26): the component has a daily cap of 0.2 GB. The workspace has no daily cap and keeps data 30 days. Volume before the filter change was about 70 MB per 30 days.

## Reading the logs
Read this first when troubleshooting a failure. Everything below was run read-only against production on 2026-10-27 from PowerShell.

### Running a query
- Needs the Azure CLI `application-insights` extension (`az extension add -n application-insights`) and a signed-in `az`. It was installed and working with az 2.83.0.
- `az` is a .cmd wrapper, so PowerShell mangles quotes in an inline query. Put the query in a file with no byte order mark and pass it as `@file`. In Windows PowerShell 5.1, `Set-Content -Encoding utf8` writes a BOM, so write the file with .NET as below.
- Always pass `--offset`. Without it the window is narrower than a query's own `ago(7d)`: the same request count returned 34 rows without `--offset` and 125 with `--offset 7d`.
- Use `-o json`. `-o tsv` gave unusable output here. `2>$null` keeps `az` warnings out of the JSON.

```powershell
function Invoke-Kql($query, $offset = '1d') {
    $file = Join-Path $env:TEMP 'query.kql'
    [System.IO.File]::WriteAllText($file, $query, (New-Object System.Text.UTF8Encoding($false)))
    $json = az monitor app-insights query --app swa-bowlmadness-prod -g rg-bowlmadness-prod --analytics-query "@$file" --offset $offset -o json 2>$null | ConvertFrom-Json
    $table = $json.tables[0]
    $cols = $table.columns.name
    $table.rows | ForEach-Object {
        $row = [ordered]@{}
        for ($i = 0; $i -lt $cols.Count; $i++) { $row[$cols[$i]] = $_[$i] }
        [pscustomobject]$row
    }
}
```

Use it as `Invoke-Kql "<query>" '7d' | Format-Table -AutoSize`. Times are UTC and `duration` is in milliseconds.

### Tables and fields
| Table | What a row is | Fields that matter |
|---|---|---|
| requests | One function invocation | `name` (function name, such as GetGames), `resultCode`, `success`, `duration`, `operation_Id`, `customDimensions.InvocationId` |
| traces | One log message | `message`, `severityLevel`, `operation_Id`, `customDimensions.CategoryName`, `customDimensions.InvocationId` |
| exceptions | One exception | `type`, `outerMessage`, `operation_Id`, `customDimensions.InvocationId`, `customDimensions.CategoryName` |
| pageViews | A browser route change | `name`, `url`, `session_Id`, `operation_Id` |
| dependencies | Calls out of a page or function | Browser fetch calls to the site and API, and the function's calls to its database. The `target` column holds the database host name, so do not paste it into docs or issues. |

- Worker-side logs (the app's own code) carry `customDimensions.CategoryName` starting with `BowlPoolManager.Api.` and a `severityLevel` on traces: 1 Information, 2 Warning, 3 Error. Rows with an empty CategoryName come from the Functions host (for example `Executed '...'` lines and health-check messages).
- To join an exception to its request, use the invocation id. Both tables hold it in `customDimensions.InvocationId`. An unhandled error writes about three exception rows, and only one of them carries `operation_Id`, `InvocationId` and `CategoryName`, so filter on a non-empty `InvocationId`.
- A page view and the server requests it triggers share an `operation_Id` (seen on 12 operations). Only page views carry `session_Id`. Server requests and traces had no `session_Id`, `user_Id` or `user_AuthenticatedId` in the last 7 days (0 of 125 requests), so those columns never identify a user. Some trace messages do contain a user id (`UserFunctions`: "Created new user {UserId}." and the non-admin user-list warning). Calls that did not come from a page (bots, scripts) have no page view.
- Sampling is on for everything except requests (host.json). No sampled rows were found in 7 days (`itemCount` was 1 on every trace), but counts of non-request items could be low at higher volume.

### Example queries
Recent exceptions:
```
exceptions | where timestamp > ago(24h)
| project timestamp, type, outerMessage=substring(outerMessage,0,100), operation_Id, InvocationId=tostring(customDimensions.InvocationId), category=tostring(customDimensions.CategoryName)
| order by timestamp desc | take 50
```

One function's failures, with the exception behind each (change `GetGames`):
```
requests | where name == 'GetGames' and success == false
| extend InvocationId = tostring(customDimensions.InvocationId)
| join kind=leftouter (exceptions | extend InvocationId = tostring(customDimensions.InvocationId) | where isnotempty(InvocationId) | project InvocationId, exceptionType=type, exceptionMessage=substring(outerMessage,0,100)) on InvocationId
| project timestamp, name, resultCode, duration, InvocationId, exceptionType, exceptionMessage
| order by timestamp desc | take 20
```

Requests by status code:
```
requests | where timestamp > ago(24h)
| summarize requests=count(), failed=countif(success == false) by resultCode, name
| order by requests desc
```

Slow requests (over 5 seconds):
```
requests | where timestamp > ago(24h) and duration > 5000
| project timestamp, name, resultCode, durationMs=round(duration), operation_Id
| order by durationMs desc | take 20
```

What a user did around an error. Take the `operation_Id` from an error row. This finds the browser session that made that call and lists everything in every operation of that session:
```
let op = '<operation_Id>';
let sid = toscalar(pageViews | where operation_Id == op | summarize any(session_Id));
let ops = pageViews | where session_Id == sid | distinct operation_Id;
union pageViews, dependencies, requests, traces, exceptions
| where operation_Id in (ops)
| project timestamp, itemType, name=coalesce(name, substring(message,0,80), type), resultCode, operation_Id
| order by timestamp asc | take 100
```
To drop the host's health-check noise from any trace query, add `| where message !has 'Process reporting unhealthy'`.

### What is not logged
- App Information logs only exist from 2026-10-27. Before that, app code sent only Warning and above.
- The host writes an "unhealthy" storage health-check warning about 540 times a day (3,915 empty-category rows in 7 days). Its cause is unexplained and it is not actionable. Filter it out when reading.
- Entry and pick contents are not logged on purpose. EntryFunctions and HoopsEntryFunctions log fixed text ("Creating/Updating Entry.") and exceptions only. Exception messages and the external API error bodies that CfbdService and BasketballDataService log are free text and were not checked for pick data.
- The `user_Id` and `user_AuthenticatedId` columns are empty on server rows (see above). Who did something has to be inferred from the page view session or from the few messages that carry a user id.
- Requests that fail with 499 after about 15 seconds are tracked in issue 117 as likely cold starts; the cause is not confirmed. Shorter 499s also occur and have not been investigated.
- Text typed into the ESPN team search appears in trace messages (`Searching ESPN teams for '{Query}'` and the ESPN service's search messages, success and failure). Other messages also carry names and ids (pool, game, team and bowl names, user ids), so treat trace messages as free text that can contain them.

## Protection
Delete locks (CanNotDelete), created 2026-10-26, one each on the resource group, the database account, the Static Web App and the Log Analytics workspace. They stop deletion of those four resources. They do not protect documents or containers inside Cosmos.

Backup policy: Continuous, 7-day tier, since 2026-10-26. Before that it was Periodic, a backup every 4 hours kept for 8 hours. The switch cannot be reversed. Check it with `az cosmosdb show` and `--query backupPolicy`.

### How a restore works
1. In the Azure portal, open the Cosmos account, then Point In Time Restore. The section is empty without the `Microsoft.DocumentDB/locations/restorableDatabaseAccounts/*/read` permission.
2. Pick the restore point (UTC, any second in the last 7 days), the location, the whole account or selected databases and containers, a resource group, and a new account name.
3. A restore creates a new account and never overwrites the live one. The status shows Creating, then Online.
4. To use the restored data, change the `CosmosDbConnectionString` app setting to the restored account and add the delete locks again.
5. Firewall, network and role settings and stored procedures, triggers and functions are not carried over. Set them again on the restored account.
6. A restore is charged about $0.15 per GB restored, once (Microsoft's West US example price).

Unconfirmed: whether a restored account can be serverless, and the Central US price.

## Verification commands (read-only)
- `az staticwebapp show`, `hostname list`, `environment list` with `-n swa-bowlmadness-prod -g rg-bowlmadness-prod`
- `az cosmosdb list -g rg-bowlmadness-prod`, then `az cosmosdb sql container list` for that account with `-d BowlMadnessDb`
- `gh secret list`, `gh workflow list`

Never run commands that print keys, connection strings or setting values (`az cosmosdb keys list`, `az staticwebapp appsettings list`, `az staticwebapp secrets list`).
