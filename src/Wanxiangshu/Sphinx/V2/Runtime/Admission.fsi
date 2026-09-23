namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

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

[<RequireQualifiedAccess>]
type IdempotencyOutcome<'value> =
    | Fresh of 'value
    | Replay of revision: Revision
    | Conflict of message: string

module Admission =
    val admitCommand:
        InquiryState -> string -> string -> InquiryCommand -> Result<IdempotencyOutcome<InquiryCommand>, CommandError>

    val admitResult: InquiryState -> ResultSubmission -> Result<WorkItem, CommandError>
    val admitGoalAmendment: InquiryState -> string -> Result<GoalSpec, CommandError>
    val admissibleWorkForClaim: InquiryState -> int -> WorkSpec list
