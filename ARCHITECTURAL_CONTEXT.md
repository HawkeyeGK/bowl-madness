# Architectural Context - BowlPoolManager

This document serves as the "Long-Term Memory" for the BowlPoolManager project, capturing core architectural decisions and patterns to ensure that both human developers and AI agents maintain synchronization across the Blazor Client, Azure Functions, and CosmosDB.

## 1. System Overview

BowlPoolManager is a serverless application designed to manage bowl game pools. The system is split across three primary projects:
- **`BowlPoolManager.Client`**: A Blazor WebAssembly (WASM) front-end providing the user interface.
- **`BowlPoolManager.Api`**: An Azure Functions back-end, acting as the serverless API layer.
- **`BowlPoolManager.Core`**: A shared .NET class library containing domain models, DTOs, and constants used by both the client and the API.

The Blazor Client interacts over HTTP with the Azure Functions, which in turn communicate with an Azure CosmosDB instance to persist data.

## 2. Data Schema (CosmosDB)

The application utilizes CosmosDB with an approach abstracted by `CosmosRepositoryBase`. The `CosmosClient` is configured globally in `BowlPoolManager.Api/Program.cs` using `System.Text.Json` with a CamelCase naming policy.

### Container/Partition Key Strategy
- **Partition Keys**: The codebase dynamically passes the Partition Key (as a string) to the repository methods (e.g., `UpsertDocumentAsync<T>(item, partitionKey)`). 
- **Entity Specifics**: 
  - **Games / Entries**: Partitioned largely by `seasonId` (e.g., in `GameRepository.cs` transactions use `new PartitionKey(seasonId)`).
  - **Configurations**: Uses explicit string literals like `"Config_Teams_FBS"` for both the document ID and Partition Key.

### Key Domain Models / DTOs
Models live in `BowlPoolManager.Core/Domain` and map directly to Cosmos documents. They serialize using `System.Text.Json`. 
- `BowlGame`: Represents a football game.
- `BowlPool`: Represents a user-created pool.
- `BracketEntry`: A user's picks within a specific pool.
- `UserProfile`: Details about the logged-in user.

*Example Domain Model JSON Mapping Context:*
```csharp
// System.Text.Json policy defined in builder:
var clientOptions = new CosmosClientOptions 
{ 
    SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase } 
};
```

## 3. API Contract

The `BowlPoolManager.Api` acts as the intermediary between the Client and Data layers.

### Function Triggers
The system heavily limits function triggers to standard HTTP.
- **HTTP Triggers**: Standard REST operations (e.g., `[HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "put", "delete")]`).

### Endpoint Naming Convention
- Routes follow standard REST principles, mostly falling back to default function names mapped to `/api/{FunctionName}` unless customized via the `Route` attribute. 
- Example customized routes:
  - `[HttpTrigger(..., Route = "DeletePool/{poolId}")]` maps to `/api/DeletePool/{poolId}`
  - `[HttpTrigger(..., Route = "Pools/{poolId}/ToggleConclusion")]` maps to `/api/Pools/{poolId}/ToggleConclusion`

### Authentication/Authorization Patterns
- The Azure Functions triggers are set to `AuthorizationLevel.Anonymous`.
- **Manual Assertion**: Security checks are handled manually inside the endpoints using `SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);`. 
- The Client uses `StaticWebAppsAuthenticationStateProvider` alongside standard Blazor `AuthorizationCore` to manage identity.

## 4. State Management (Blazor Client)

The Blazor client avoids heavy external state libraries (like Fluxor) in favor of simple, scoped services.
- **`AppState` Service**: A scoped service class `AppState.cs` is registered in `Program.cs`. 
  - It uses standard C# events (`public event Action? OnChange;`) and notification methods (`public void NotifyDataChanged() => OnChange?.Invoke();`).
  - Components subscribe to `AppState.OnChange` to trigger `StateHasChanged()`.
- **Domain Services**: Dedicated interaction services (e.g., `PoolService`, `SeasonService`) are injected as `Scoped` dependencies to handle localized network calling and mapping logic.

## 5. Development Constraints ("Rules of the Road")

To maintain architectural purity, follow these rules:
- **Serialization**: **Always** use `System.Text.Json`. Do not use `Newtonsoft.Json` for domain mapping.
- **Shared Models**: All models shared between the API and Client must live in `BowlPoolManager.Core`. Do not duplicate definitions.
- **Cosmos Optimization**: All Cosmos queries should try to include the Partition Key. Where batch operations occur, ensure the batch targets a specific `PartitionKey`.
- **Date Handling**: Treat `DateTime` properties as UTC (`DateTime.UtcNow`). 
- **Security Check**: For restricted endpoints, explicitly invoke validation contexts via `SecurityHelper` routines before applying any business logic.

## 6. Cross-Project Dependencies

- `BowlPoolManager.Client` depends directly on `BowlPoolManager.Core`.
- `BowlPoolManager.Api` depends directly on `BowlPoolManager.Core`.
- The Client never directly references the API or data packages; communication is strictly over HTTP. The API hides CosmosDB completely from the Core models.
