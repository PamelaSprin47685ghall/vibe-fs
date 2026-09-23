namespace Wanxiangshu.Sphinx.V2.Core

[<RequireQualifiedAccess>]
type WorkState =
    | Planned
    | Ready
    | Leased of Fence
    | Running of Fence * physicalRef: string
    | InputRequired of Fence
    | Succeeded of Attempt
    | Failed of Attempt
    | CancelRequested of Attempt
    | Cancelled of Attempt
    | Superseded of successor: WorkId

type WorkSpec =
    { Id: WorkId
      Attempt: Attempt
      Fence: Fence
      RoundId: RoundId option
      PlanId: PlanId
      Producer: string
      Capability: string
      Input: JsonEnvelope option
      OutputSchema: SchemaRef option
      Dependencies: Set<WorkId>
      ConflictKeys: Set<string>
      /// Physical binding, absent until a Host actually accepts the dispatch.
      PhysicalRef: string option
      /// Resources this work reserves for its attempt.
      Reserved: Map<string, float> }

type WorkItem = { Spec: WorkSpec; State: WorkState }

type WorkError = { Code: string; Message: string }

module Work =
    val reserved: WorkSpec -> Map<string, float>
    val stateName: WorkState -> string
    val validateSpec: WorkSpec -> Result<unit, WorkError>
    val isTerminal: WorkState -> bool
