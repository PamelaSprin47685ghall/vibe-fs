namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

/// Domain commands are the only way in. Workers never submit an event directly, and no
/// command carries a certificate patch, a budget debit or a goal revision — those are
/// consequences of a fold, not inputs a caller may supply (WHAT[sphinx-v2-018]).
[<RequireQualifiedAccess>]
type InquiryCommand =
    | StartCommand of StartPayload
    | ClaimWorkCommand of claimLimit: int
    | SubmitResultCommand of results: ResultSubmission list
    | CancelCommand of reason: string
    | AmendGoalCommand of authorizedBy: string * addedConstraints: string list * replacementText: string option
    | ReadStatusCommand
    | ExportCommand of mode: string

and StartPayload =
    { CommandId: string
      Goal: GoalSpec
      ResourceSpecs: ResourceSpec list
      RenderReserve: Map<string, float>
      ProfileRef: string
      ConfigHash: string }

and ResultSubmission =
    { WorkId: WorkId
      Attempt: Attempt
      Fence: Fence
      ObservationId: ObservationId
      CanonicalResult: string
      ResultSchema: SchemaRef
      ClusterId: string
      Usage: SettledUsage option }

type CommandError = { Code: string; Message: string }

/// The three-layer idempotency contract (WHAT[sphinx-v2-011]).
[<RequireQualifiedAccess>]
type IdempotencyOutcome<'value> =
    /// First time: run the command.
    | Fresh of 'value
    /// Same command id, same content: return the original receipt, do not re-run.
    | Replay of revision: Revision
    /// Same command id, different content: refuse.
    | Conflict of message: string

module Admission =

    let private error<'value> (code: string) (message: string) : Result<'value, CommandError> =
        Error { Code = code; Message = message }

    /// The idempotency lookup precedes the stale-revision check on purpose: a network
    /// retry of an already-applied command must return the original receipt, not turn
    /// into a revision conflict (WHAT[sphinx-v2-011]).
    let admitCommand
        (state: InquiryState)
        (commandId: string)
        (fingerprint: string)
        (command: InquiryCommand)
        : Result<IdempotencyOutcome<InquiryCommand>, CommandError> =
        let blankCommand = System.String.IsNullOrWhiteSpace commandId
        let blankFingerprint = System.String.IsNullOrWhiteSpace fingerprint

        let invalidPayload =
            match command with
            | InquiryCommand.SubmitResultCommand [] -> true
            | InquiryCommand.ClaimWorkCommand limit -> limit < 1
            | _ -> false

        let reject () =
            if blankCommand then
                error "invalid-command" "command id must not be blank"
            elif blankFingerprint then
                error "invalid-command" "command fingerprint must not be blank"
            else
                error "invalid-command" "command payload is not admissible"

        let replay = InquiryState.commandRevision state commandId

        let admit () =
            match replay with
            | Some revision -> Ok(IdempotencyOutcome.Replay revision)
            | None -> Ok(IdempotencyOutcome.Fresh command)

        match blankCommand || blankFingerprint || invalidPayload with
        | true -> reject ()
        | false -> admit ()

    /// A result is admissible only against a work that exists, is running, and carries
    /// the exact attempt and fence the caller presents.
    /// A result is admissible only when the work is currently runnable under this exact
    /// attempt and fence.
    let private runnableOutcome (submission: ResultSubmission) (item: WorkItem) : Result<WorkItem, CommandError> =
        let running =
            match item.State with
            | WorkState.Running _ -> true
            | WorkState.Succeeded _ -> true
            | _ -> false

        match running with
        | true -> Ok item
        | false ->
            error
                "work-not-running"
                (sprintf "work %s is in state %s" (WorkId.value submission.WorkId) (Work.stateName item.State))

    let private matchingOutcome (submission: ResultSubmission) (item: WorkItem) : Result<WorkItem, CommandError> =
        let matching =
            item.Spec.Attempt = submission.Attempt && item.Spec.Fence = submission.Fence

        match matching with
        | true -> runnableOutcome submission item
        | false -> error "attempt-mismatch" "result attempt or fence does not match the current attempt"

    let private resultReason
        (submission: ResultSubmission)
        (item: WorkItem option)
        : Result<WorkItem, CommandError> =
        let unknownWork () =
            error "unknown-work" (sprintf "work %s is not planned" (WorkId.value submission.WorkId))

        match item with
        | Some current -> matchingOutcome submission current
        | None -> unknownWork ()

    let admitResult (state: InquiryState) (submission: ResultSubmission) : Result<WorkItem, CommandError> =
        resultReason submission (state.Work |> Map.tryFind submission.WorkId)

    /// Goal amendment is the one command that may move the goal, and it must name its
    /// authorizer — a plugin cannot authorize itself (WHAT[sphinx-v2-001]).
    let admitGoalAmendment
        (state: InquiryState)
        (authorizedBy: string)
        : Result<GoalSpec, CommandError> =
        if System.String.IsNullOrWhiteSpace authorizedBy then
            error "invalid-goal" "goal amendment requires a non-blank authorizer"
        else
            Ok state.Goal

    /// The evidence a claim command may carry: only work the caller was allowed to run.
    let admissibleWorkForClaim (state: InquiryState) (limit: int) : WorkSpec list =
        state
        |> InquiryState.readyWork
        |> List.sortBy (fun item -> WorkId.value item.Spec.Id)
        |> List.truncate limit
        |> List.map (fun item -> item.Spec)
