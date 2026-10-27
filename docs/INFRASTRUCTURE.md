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
