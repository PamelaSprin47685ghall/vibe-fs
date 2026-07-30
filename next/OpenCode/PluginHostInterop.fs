namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
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
        if isNull output || isNull output?messages then
            None
        else
            let messages = unbox<obj array> output?messages

            messages
            |> Array.tryPick (fun msg ->
                if not (isNull msg) && not (isNull msg?info) && not (isNull msg?info?sessionID) then
                    Some(unbox<string> msg?info?sessionID)
                else
                    None)

    let toolHooks
        (toolModule: obj)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (gitTreePort: GitTreePort option)
        (workspaceDirectory: string option)
        (scope: PluginRuntimeScope)
        (currentPhysicalUserMessage: string -> string option)
        (onRunStarted: (SessionId -> AgentRole -> string option -> unit) option)
        (backgroundBFor: (string -> string option) option)
        (snapshot: ISessionSnapshotPort option)
        (cancelSignals: (SessionId seq -> unit) option)
        (eventPort: IEventObservationPort option)
        : ToolRegistration =
        ToolRegistry.create
            toolModule
            sessionPort
            journal
            gitTreePort
            workspaceDirectory
            scope.SessionParents
            currentPhysicalUserMessage
            scope.VerdictSessions
            scope.SessionDirectories
            onRunStarted
            backgroundBFor
            snapshot
            cancelSignals
            eventPort
