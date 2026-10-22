namespace Wanxiangshu.Mission.Work

open Wanxiangshu.Execution.Tool
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Participant.Provider

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
    val Description: string = "tool/deliver/description"

    [<Literal>]
    val Blocked: string = "tool/deliver/blocked"

    [<Literal>]
    val AssessmentRequired: string = "tool/deliver/assessment-required"

    [<Literal>]
    val Retired: string = "tool/deliver/finished"

    [<Literal>]
    val Confirmation: string = "tool/deliver/confirmation"

    [<Literal>]
    val NonManagerRole: string = "tool/deliver/non-manager-role"

    [<Literal>]
    val NoAuthority: string = "tool/deliver/no-authority"

    [<Literal>]
    val BindingUnavailable: string = "tool/deliver/binding-unavailable"

    [<Literal>]
    val FinishFailed: string = "tool/deliver/finish-failed"

    /// The retirement result tells the model whether this call handed the work
    /// over to a successor or ended it. The instruction alone cannot tell the
    /// two apart, so the recorded outcome travels with it.
    val retirementResult: outcome: string -> string

    /// The first call of the two-stage sequence records the confirmation and
    /// does not finish the work; a second deliver ends it.
    val confirmationResult: unit -> string

    /// A committed blocker keeps the tenure active. The call reports what to
    /// close and may be repeated once the resources have settled.
    val blockedResult: blockers: string list -> string

    /// The work has no assessment yet: the call asks for one instead of
    /// delivering.
    val assessmentRequiredResult: unit -> string

    /// The description the tool registers with.
    val description: unit -> string

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
    val recordedFenceReplay:
        work: DurableWorkHost.InspectedWork ->
        providerRunId: string ->
        toolCallId: string ->
            Result<string, string> option

    /// The confirmation this call already recorded is a durable fact. The
    /// tenure it confirmed may since have been relayed or delivered, so the
    /// confirmation is replayed from the recorded fence alone: a later state of
    /// the work never turns a recorded confirmation into a refusal. The
    /// committed confirmation fence names the run and call that confirmed; this
    /// call replays that exact confirmation to its recorded state.
    val recordedConfirmationReplay:
        work: DurableWorkHost.InspectedWork ->
        providerRunId: string ->
        toolCallId: string ->
            Result<string, string> option

    /// The tenure this call may retire.
    type Prepared =
        { Fence: Fence
          Incumbent: string
          Reviewed: bool }

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
    val enter: work: DurableWorkHost.InspectedWork -> fence: Fence -> Result<Entry, string>

    /// The refusal text for a call that may not be bound at all.
    val refusalText: path: string -> string
