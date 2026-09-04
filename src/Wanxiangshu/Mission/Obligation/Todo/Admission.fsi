namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Relay

module MagicTodoAdmission =
    type AdmissionLocalizedToolCall =
        { ToolCallId: ToolCallId
          ToolPartOrdinal: int
          TodowriteCallIdsInMessage: ToolCallId list
          ReviewFrontier: XTraceCursor
          ProviderInputDigest: string }

    type ObligationPrepareSuccess =
        { TodoWriteId: TodoWriteId
          Proposed: ObligationList
          Base: ObligationList
          BaseDigest: string
          ProposedDigest: string
          ReviewFrontier: XTraceCursor
          ToolPartOrdinal: int
          ProviderInputDigest: string }

    [<RequireQualifiedAccess>]
    type ExistingPreparedAcceptance =
        | PreparedOnly
        | Accepted

    type ExistingPrepared =
        { Identity: PreparedIdentity
          TodoWriteId: TodoWriteId
          Acceptance: ExistingPreparedAcceptance }

    [<RequireQualifiedAccess>]
    type AdmissionOutcome<'Prepare> =
        | FreshPrepare of 'Prepare
        | IdempotentReplay of TodoWriteId
        | Rejected of MagicTodoReject

    val admitObligations:
        sha256: (string -> string) ->
        incumbencyId: IncumbencyId ->
        settledCurrent: ObligationList ->
        existingPrepared: ExistingPrepared option ->
        localized: AdmissionLocalizedToolCall ->
        submitted: ObligationList ->
            AdmissionOutcome<ObligationPrepareSuccess>
