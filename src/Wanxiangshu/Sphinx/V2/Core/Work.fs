namespace Wanxiangshu.Sphinx.V2.Core

/// Work is identified by (WorkId, Attempt) and carried by a logical fence.
///
/// WHAT[sphinx-v2-003]: a retry is a new Attempt with a new Fence and a new physical
/// call identity, while a genuine re-measurement is a new WorkId. The two are never
/// interchangeable, so a duplicate receipt cannot silently add votes to a ballot and
/// a network retry cannot silently masquerade as an independent second opinion.

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

/// An immutable work specification. Attempt and Fence live here because a retry is a
/// different attempt of the same work: the spec fields that make it *the same* work
/// cannot change, while the identity that makes it *a different try* does.
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

    let stateName (state: WorkState) : string =
        match state with
        | WorkState.Planned -> "Planned"
        | WorkState.Ready -> "Ready"
        | WorkState.Leased _ -> "Leased"
        | WorkState.Running _ -> "Running"
        | WorkState.InputRequired _ -> "InputRequired"
        | WorkState.Succeeded _ -> "Succeeded"
        | WorkState.Failed _ -> "Failed"
        | WorkState.CancelRequested _ -> "CancelRequested"
        | WorkState.Cancelled _ -> "Cancelled"
        | WorkState.Superseded _ -> "Superseded"

    /// Immutability is over everything except Attempt, Fence and PhysicalRef. A retry
    /// changes those three and nothing else; anything else changing is a spec rewrite
    /// and must be a new WorkId.
    let private samePurpose (left: WorkSpec) (right: WorkSpec) : bool =
        left.Id = right.Id
        && left.RoundId = right.RoundId
        && left.PlanId = right.PlanId
        && left.Producer = right.Producer
        && left.Capability = right.Capability
        && left.Input = right.Input
        && left.OutputSchema = right.OutputSchema
        && left.Dependencies = right.Dependencies
        && left.ConflictKeys = right.ConflictKeys

    /// The fence must travel with its attempt. A fence from attempt 1 attached to
    /// attempt 2 is exactly the late-result hijack this field exists to prevent.
    let private fenceBelongsToAttempt (spec: WorkSpec) : bool =
        match spec.Fence with
        | Fence value -> value.Contains(":" + string (Attempt.value spec.Attempt))

    let validateSpec (spec: WorkSpec) : Result<unit, WorkError> =
        if not (samePurpose spec spec) then
            Ok()
        elif Set.isEmpty spec.Dependencies |> not && spec.Dependencies |> Set.contains spec.Id then
            Error { Code = "self-dependency"; Message = "work cannot depend on itself" }
        elif not (fenceBelongsToAttempt spec) then
            Error
                { Code = "invalid-fence"
                  Message = "fence must be bound to its own attempt" }
        else
            Ok()

    /// Resources this work holds for its attempt. An empty map means the capability is
    /// free, which is a claim the Host can contradict by reporting actual usage.
    let reserved (spec: WorkSpec) : Map<string, float> = spec.Reserved

    let isTerminal (state: WorkState) : bool =
        match state with
        | WorkState.Succeeded _
        | WorkState.Failed _
        | WorkState.Cancelled _
        | WorkState.Superseded _ -> true
        | _ -> false
