namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.Persistence.Journal

module PluginHostInterop =

    [<RequireQualifiedAccess>]
    type internal HookSettlementEvidence =
        | NoOwnedExecution
        | ExactSettlementComplete
        | DurableOutcomeUnknown
        | SettlementIncomplete

    [<RequireQualifiedAccess>]
    type internal HookFailurePolicy =
        | RethrowUnchanged
        | FatalAfterSettlement
        | RejectFatalBeforeSettlement

    type internal HookFailureOutcome =
        { Failure: ExecutionFailure
          Lifecycle: DurableExecutionLifecycle
          ExecutionKey: ChatExecutionKey option
          Settlement: HookSettlementEvidence }

    [<Emit("import('@opencode-ai/plugin/tool')")>]
    val importToolModule: unit -> Task<obj>

    /// Host hook whose F# value stayed CURRIED after compilation.
    /// Keep this arity adaptation as a direct Emit call at the registration site:
    /// moving it behind an ordinary F# helper changes how Fable boxes the original
    /// function and silently turns paired hooks into curried no-ops.
    [<Emit("(args, context) => $0(args)(context)")>]
    val curriedHook: fn: obj -> obj

    /// Host hook that Fable emitted as a two-arity arrow.
    ///
    /// Passing that arrow through an `obj` boundary can make Fable substitute a
    /// `curry2(fn)` adapter for `$0`. Calling that adapter with two JS arguments
    /// returns its second-stage function without executing the hook body. Accept
    /// both runtime shapes here: invoke the supplied callable positionally, then
    /// finish the curried second stage when Fable inserted one.
    [<Emit("(args, context) => { const result = $0(args, context); return typeof result === 'function' ? result(context) : result; }")>]
    val pairedHook: fn: obj -> obj

    [<Emit("(args, _context) => $0(args)")>]
    val unaryHook: fn: obj -> obj

    [<Emit("(_args, _context) => $0()")>]
    val nullaryHook: fn: obj -> obj

    val internal interpretHookFailure: outcome: HookFailureOutcome -> HookFailurePolicy

    val internal normalizeHookFailure: error: obj -> HookFailureOutcome

    val policyAwareHook: operation: string -> adaptedHook: obj -> obj

    val registeredHook: key: HookKey -> adaptedHook: obj -> string * obj

    val projectionSessionIdFromMessages: output: obj -> string option

    val toolHooks:
        toolModule: obj ->
        sessionPort: ISessionHostPort ->
        journal: AgentJournal option ->
        gitTreePort: GitTreePort option ->
        workspaceDirectory: string option ->
        scope: PluginRuntimeScope ->
        currentPhysicalUserMessage: (string -> string option) ->
        onRunStarted: (SessionId -> Role -> string option -> unit) option ->
        parentWorkRecordFor: (string -> Task<string option>) option ->
        childWorkRecordFor: (string -> Task<string option>) option ->
        snapshot: ISessionSnapshotPort option ->
        cancelSignals: (SessionId seq -> unit) option ->
        eventPort: IEventObservationPort option ->
        finalityReviewerTimeoutMs: int option ->
        casebookToolSpecs: ToolSpec list ->
            ToolRegistration
