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
open Wanxiangshu.Review
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

    /// Host hook whose F# value stayed CURRIED after compilation.
    ///
    /// `plugin/index.ts:290` calls `fn(input, output)`, so every hook must be handed
    /// over as a two-argument JS function. Which template produces one depends on
    /// what Fable emitted, and that is not a matter of taste:
    ///
    ///   obj-typed record field / partial application  → curried chain
    ///   plain two-parameter `let`                      → two-arity arrow
    ///
    /// Applying this template to a two-arity arrow calls it with ONE argument, so the
    /// body runs with `output = undefined`. That is what happened to all three
    /// transform-family hooks: `dotnet build` was green, and every provider step threw
    /// `Cannot read properties of undefined (reading 'messages')` on a real Host.
    ///
    /// Two named helpers rather than a runtime arity sniff. `fn.length` is exactly the
    /// kind of guess that hides the next mismatch instead of failing on it.
    [<Emit("(args, context) => $0(args)(context)")>]
    let curriedHook (fn: obj) : obj = jsNative

    /// Host hook that Fable emitted as a two-arity arrow.
    [<Emit("(args, context) => $0(args, context)")>]
    let pairedHook (fn: obj) : obj = jsNative

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
                scope.Sessions.VerdictSessions
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
