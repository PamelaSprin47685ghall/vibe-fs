namespace Wanxiangshu.Host.Ports

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// HNI P-08: a runnable model as the product names it. Choosing a route is product logic.
type ModelRoute =
    { Provider: string
      Model: string
      Variant: string option }

type SpawnRequest =
    { Parent: SessionId
      Role: AgentRole
      Candidates: ModelRoute list
      SpawnKey: string option }

[<RequireQualifiedAccess>]
type SpawnRoute =
    | Keep
    | Route of routes: ModelRoute list
    | Refuse of reason: string

type IModelCatalog =
    abstract Available: unit -> HostResult<ModelRoute list>
    abstract Apply: session: SessionId * route: ModelRoute -> HostResult<unit>

type SpawnRouting = SpawnRequest -> Task<SpawnRoute>
