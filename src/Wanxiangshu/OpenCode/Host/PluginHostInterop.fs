namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Knowledge.Casebook.OpenCode
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Context.Companion
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open CompanionProjection

module PluginHostInterop =

    [<Emit("import('@opencode-ai/plugin/tool')")>]
    let importToolModule () : Task<obj> = jsNative

    [<Emit("$0 instanceof Error ? String($0.message) : String($0)")>]
    let private hostErrorText (error: obj) : string = jsNative

    let private fatalHookError operation error =
        Diagnostic.fatal operation [ "result", hostErrorText error ]

    /// Host hook whose F# value stayed CURRIED after compilation.
    /// Keep this arity adaptation as a direct Emit call at the registration site:
    /// moving it behind an ordinary F# helper changes how Fable boxes the original
    /// function and silently turns paired hooks into curried no-ops.
    [<Emit("(args, context) => $0(args)(context)")>]
    let curriedHook (fn: obj) : obj = jsNative

    /// Host hook that Fable emitted as a two-arity arrow.
    [<Emit("(args, context) => $0(args, context)")>]
    let pairedHook (fn: obj) : obj = jsNative

    /// Fatal membrane applied AFTER arity adaptation. At this point `fn` is always
    /// a real two-argument Host callable, so guarding it cannot perturb Fable's
    /// representation of the original hook.
    [<Emit("(args, context) => { try { return Promise.resolve($1(args, context)).catch((err) => { $2($0, err); throw err; }); } catch (err) { $2($0, err); throw err; } }")>]
    let private guardedHostHook (operation: string) (fn: obj) (onError: string -> obj -> unit) : obj = jsNative

    let private fatalHookErrorFor operation error = fatalHookError operation error

    let fatalHook operation (adaptedHook: obj) : obj =
        guardedHostHook operation adaptedHook fatalHookErrorFor

    [<Emit("(args, context) => { const expectedMessage = $2; const handle = (err) => { const message = err && typeof err === 'object' && 'message' in err ? String(err.message) : String(err); if (message === expectedMessage) throw err; $3($0, err); throw err; }; try { return Promise.resolve($1(args, context)).catch(handle); } catch (err) { return handle(err); } }")>]
    let private guardedExpectedRejectionHook
        (operation: string)
        (fn: obj)
        (expected: string)
        (onError: string -> obj -> unit)
        : obj =
        jsNative

    /// Expected protocol rejection crosses the Host hook boundary unchanged;
    /// every other exception still enters the fatal invariant membrane.
    let expectedRejectionHook operation expected (adaptedHook: obj) : obj =
        guardedExpectedRejectionHook operation adaptedHook expected fatalHookErrorFor

    let projectionSessionIdFromMessages (output: obj) =
        ProviderWireDecode.projectionSessionIdFromMessages output

    let toolHooks
        (toolModule: obj)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (gitTreePort: GitTreePort option)
        (workspaceDirectory: string option)
        (scope: PluginRuntimeScope)
        (currentPhysicalUserMessage: string -> string option)
        (onRunStarted: (SessionId -> Role -> string option -> unit) option)
        (parentWorkRecordFor: (string -> Task<string option>) option)
        (childWorkRecordFor: (string -> Task<string option>) option)
        (snapshot: ISessionSnapshotPort option)
        (cancelSignals: (SessionId seq -> unit) option)
        (eventPort: IEventObservationPort option)
        (finalityReviewerTimeoutMs: int option)
        (casebookToolSpecs: ToolSpec list)
        : ToolRegistration =
        let jsTransactionPersistence =
            workspaceDirectory
            |> Option.bind (fun workspace -> WorkspaceEventStore.tryCurrent (RuntimePath.gitCommonDir workspace))
            |> Option.map JsToolsTransactionStore.createPersistence

        let registration =
            ToolRegistry.create
                toolModule
                sessionPort
                journal
                gitTreePort
                workspaceDirectory
                scope.Sessions.SessionParents
                currentPhysicalUserMessage
                scope.Sessions.VerdictSubmissions
                scope.Sessions.SessionDirectories
                onRunStarted
                parentWorkRecordFor
                childWorkRecordFor
                snapshot
                cancelSignals
                eventPort
                (Some scope.ParkedTransformHost)
                scope.SyncDelegateRuntime
                (Some scope.Strength.StrengthRuntime)
                finalityReviewerTimeoutMs
                casebookToolSpecs
                jsTransactionPersistence

        // P0-RECOVERY-JOIN-001: JoinTool RequireFamilyRecovery → PluginRuntimeScope.
        registration.Runtime.AttachFamilyRecovery(fun root -> scope.RequireFamilyRecovery root)
        // EXEC-017: JoinTool Begin(user-message wake) shares this process-local
        // attempt-scoped registry.
        registration.Runtime.AttachJoinAttempts scope.Sessions.JoinInterrupts
        registration
