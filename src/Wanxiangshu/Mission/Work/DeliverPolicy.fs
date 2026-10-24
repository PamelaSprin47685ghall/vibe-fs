namespace Wanxiangshu.Mission.Work

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Host

/// The product's delivery decisions: what a `deliver` call is bound to, which
/// fence may be replayed, what the retirement may commit, and how each
/// outcome is presented to the model.
///
/// The adapter owns the wire: it decodes the call, asks this module for the
/// binding, performs the durable write through the product fact persistence
/// capability (`DurableWorkHost`), and presents the text this module returns.
[<RequireQualifiedAccess>]
module DeliverPolicy =

    [<Literal>]
    let Description = "tool/deliver/description"

    [<Literal>]
    let Blocked = "tool/deliver/blocked"

    [<Literal>]
    let AssessmentRequired = "tool/deliver/assessment-required"

    [<Literal>]
    let Retired = "tool/deliver/finished"

    [<Literal>]
    let NonManagerRole = "tool/deliver/non-manager-role"

    [<Literal>]
    let NoAuthority = "tool/deliver/no-authority"

    [<Literal>]
    let BindingUnavailable = "tool/deliver/binding-unavailable"

    [<Literal>]
    let FinishFailed = "tool/deliver/finish-failed"

    /// The retirement result tells the model whether this call handed the work
    /// over to a successor or ended it. The instruction alone cannot tell the
    /// two apart, so the recorded outcome travels with it.
    let retirementResult (outcome: string) : string =
        ToolResultText.tomlObjectWithInstructions
            [ ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Retired Map.empty ]
            [ "outcome", ToolResultText.TString outcome ]

    /// A committed blocker keeps the tenure active. The call reports what to
    /// close and may be repeated once the resources have settled.
    let blockedResult (blockers: string list) : string =
        ToolResultText.tomlObjectWithInstructions
            [ ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Blocked Map.empty ]
            [ "finished", ToolResultText.TBool false
              "blocker_count", ToolResultText.TInt(List.length blockers)
              "blockers",
              ToolResultText.TTable(
                  blockers
                  |> List.mapi (fun index blocker -> string index, ToolResultText.TString blocker)
              ) ]

    /// The work has no assessment yet: the call asks for one instead of
    /// delivering.
    let assessmentRequiredResult () : string =
        ToolResultText.tomlObjectWithInstructions
            [ ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) AssessmentRequired Map.empty ]
            [ "finished", ToolResultText.TBool false
              "assessment_required", ToolResultText.TBool true ]

    /// The description the tool registers with.
    let description () : string =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Description Map.empty

    /// The fence of one deliver call: the exact attempt this call belongs to.
    type Fence =
        { SessionId: SessionId
          ToolCallId: ToolCallId
          ProviderRun: ProviderRunIdentity
          PhysicalUserMessageId: PhysicalUserMessageId
          Directory: string }

    /// What the product knows about the calling session before the retirement
    /// is attempted.
    type Admission =
        | Admitted of Fence
        | Refused of reason: string

    /// Algorithm E1.1: the fence (ProviderRunId, ToolCallId) is looked up on the
    /// retirements the fold already committed, before the entry reads an active
    /// tenure or a review position. A retried call must return the state that
    /// call recorded: a delivered work has no active tenure left, and a relayed
    /// one leaves a successor that is not yet reviewed. The recorded outcome
    /// name is already the closed result vocabulary, so the expected advance the
    /// entry read is the one field left to compare; a different one is the same
    /// refusal the second call gets. This path writes nothing.
    let recordedFenceReplay
        (work: DurableWorkHost.InspectedWork)
        (providerRunId: string)
        (toolCallId: string)
        : Result<string, string> option =
        let sameFence (retirement: DurableWorkHost.HostRetirement) =
            retirement.ProjectionCut
            && retirement.Fence.ProviderRunId = providerRunId
            && retirement.Fence.ToolCallId = toolCallId

        let recordedState (retirement: DurableWorkHost.HostRetirement) =
            if retirement.Expected = work.Authority then
                Ok(retirementResult retirement.Outcome)
            else
                Error "RetirementReplayConflict"

        work.Retirements |> List.tryFind sameFence |> Option.map recordedState

    /// The tenure this call may retire.
    type Prepared =
        { Fence: Fence
          Incumbent: string
          Reviewed: bool
          ReviewOutcome: string option }

    /// Algorithm E1.1 and E2: the entry either replays a fence the fold already
    /// settled, or binds the active tenure. The replay decision belongs to the
    /// fold, so no earlier layer answers a retried call with a refusal for the
    /// tenure the retirement removed.
    [<RequireQualifiedAccess>]
    type Entry =
        | Replayed of result: string
        | Prepared of prepared: Prepared

    /// The entry for one call: a replay of what this fence already recorded,
    /// or the active tenure this call may retire.
    let enter (work: DurableWorkHost.InspectedWork) (fence: Fence) : Result<Entry, string> =
        let fenceReplay =
            recordedFenceReplay work (ProviderRunIdentity.value fence.ProviderRun) (ToolCallId.value fence.ToolCallId)
            |> Option.map (Result.map Entry.Replayed)

        let activeEntry =
            match work.ActiveId with
            | Some active ->
                Ok(
                    Entry.Prepared
                        { Fence = fence
                          Incumbent = active
                          Reviewed = work.Reviewed
                          ReviewOutcome = work.ReviewOutcome }
                )
            | None ->
                Error(
                    ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) BindingUnavailable Map.empty
                )

        fenceReplay |> Option.defaultWith (fun () -> activeEntry)

    /// The refusal text for a call that may not be bound at all.
    let refusalText (path: string) : string =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) path Map.empty
