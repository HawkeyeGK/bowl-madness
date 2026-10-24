# Infrastructure

Facts about where Bowl Madness and Hoops Madness run. No secrets, keys or setting values belong here, only names.
Last checked 2026-10-24. "Unverified" means it was recorded earlier and could not be re-checked.

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
- API: wired (Program.cs and host.json). Client: JavaScript SDK loader in index.html (commit a7b1f3f).
- Known gap: Blazor route changes do not send page views, so traffic appears to land on "/". No trackPageView call exists in the client (verified by search). A fix would hook NavigationManager location changes to the SDK.

## Verification commands (read-only)
- `az staticwebapp show`, `hostname list`, `environment list` with `-n swa-bowlmadness-prod -g rg-bowlmadness-prod`
- `az cosmosdb list -g rg-bowlmadness-prod`, then `az cosmosdb sql container list` for that account with `-d BowlMadnessDb`
- `gh secret list`, `gh workflow list`

Never run commands that print keys, connection strings or setting values (`az cosmosdb keys list`, `az staticwebapp appsettings list`, `az staticwebapp secrets list`).
