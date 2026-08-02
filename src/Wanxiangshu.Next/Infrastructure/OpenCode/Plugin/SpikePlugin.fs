namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.ProviderProjection
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open PluginHostInterop

module SpikePlugin =

    let initSpikePlugin (input: obj) : Task<obj> =
        task {
            let portOpt = OpenCodePort.create input

            let journal =
                match PluginHost.createJournal input with
                | Ok value -> value
                | Error err -> raise (InvalidOperationException err)

            let scope = new PluginRuntimeScope(journal)

            PluginHost.restoreSessionParents journal scope.SessionParents

            let familyParent (sessionId: SessionId) =
                match scope.SessionParents.TryGetValue(SessionId.value sessionId) with
                | true, parentId -> Some(SessionId.create parentId)
                | false, _ -> None

            match PluginHost.createHost input portOpt (Some familyParent) with
            | Error err -> return raise (InvalidOperationException err)
            | Ok(eventPort, sessionPort, snapshotOpt, terminalKey, sharedTerminalPort) ->
                scope.AttachSharedTerminal(terminalKey, sharedTerminalPort)

                for KeyValue(childId, parentId) in scope.SessionParents do
                    scope.OwnedSessions.Add childId |> ignore
                    scope.OwnedSessions.Add parentId |> ignore

                let gitTreePort =
                    match PluginHost.gitTreePortFromInput input with
                    | Some port -> Some port
                    | None -> PluginHost.workspaceDirectory input |> Option.map GitTree.create

                // The stable workspace, captured once at plugin init. The transform
                // input carries no directory; the blogger must be pinned to this
                // path (not the manager worktree) so its system prompt survives the
                // worktree release at publish. First boot wins: the main workspace
                // instance starts before the manager worktree instances.
                let workspaceDirectory = PluginHost.workspaceDirectory input

                if SharedState.RootWorkspace.IsNone then
                    SharedState.RootWorkspace <- workspaceDirectory

                let wired =
                    HostSignalBootstrap.wire sessionPort eventPort snapshotOpt journal gitTreePort scope input

                // PROMPT-011: the pending-claim pass must NOT run here, inside the
                // plugin constructor. The Host awaits the constructor before its
                // project instance is ready (`plugin/index.ts:112-123,222-224`), and
                // `reconcile` reads `session.messages` through the SDK — an
                // in-process fetch that competes with Host startup. Under parallel
                // canary load that read can exceed the silence window and park the
                // constructor, so a restarted session never sends its next prompt.
                // Attach the gate instead; the first real Host event starts the
                // pass (single-flight), and every business entry point awaits it.
                scope.AttachRecoveryGate(PromptRecovery.RecoveryGate(journal, snapshotOpt))

                let transform inObj outObj : Task<unit> =
                    task {
                        let projectionSessionIdOpt = projectionSessionIdFromMessages outObj

                        projectionSessionIdOpt |> Option.iter wired.RegisterOwned

                        do
                            CompanionTransform.handleCompanionTransform
                                scope.Companions
                                scope.CompanionGate
                                scope
                                sessionPort
                                journal
                                (Some(fun bloggerId ->
                                    // Register ownership + ActiveRun so idle→reconcile
                                    // emits TerminalOutcome.Completed for this child.
                                    wired.RegisterOwned(SessionId.value bloggerId)
                                    wired.BindActiveRun bloggerId AgentRole.Blogger None))
                                SharedState.RootWorkspace
                                inObj
                                outObj

                        do! XWire.applyTransform snapshotOpt journal scope outObj

                        // COMPANION-003/007: keep the XTrace in step with the
                        // provider-visible semantic projection at the transform
                        // boundary. Idempotent by (turn, part) provenance; the
                        // Blogger chunker's ingest cursor maps back through this
                        // trace, so a lagging trace would stall BlogEntryCommitted.
                        match projectionSessionIdOpt with
                        | Some sessionId ->
                            let rawMessages = unbox<obj array> outObj?messages |> Array.toList

                            let semantic =
                                Projection.decodeMessageView rawMessages |> ProviderProjection.toSemantic

                            XTraceCapture.captureProjection journal (SessionId.create sessionId) semantic
                        | None -> ()

                        // REVIEW-010: seal LAST, and only after the Companion rewrite has
                        // mutated `outObj`. The seal must digest the message view the
                        // provider actually receives; sealing before the rewrite would
                        // record bytes the Host never sends.
                        //
                        // Host source awaits every hook in turn (`plugin/index.ts:280-292`),
                        // so returning a Task here makes the SDK read complete before the
                        // provider request is built.
                        let sealTask =
                            match projectionSessionIdOpt with
                            | None -> Task.FromResult()
                            | Some projectionSessionId ->
                                let rawMessages = unbox<obj array> outObj?messages |> Array.toList

                                ReviewSeal.sealTransform
                                    snapshotOpt
                                    journal
                                    (SessionId.create projectionSessionId)
                                    (Projection.decodeMessageView rawMessages)
                                    (Projection.lastUserMessageId rawMessages)
                                    wired.PendingReviewSeals

                        do! sealTask
                        ()
                    }

                let chatParams = ChatParamsHook.create journal

                // HOST-009: the object handed to the Host carries Host hooks and
                // nothing else.
                //
                // Six extra keys used to hang here — `projection`, `events`,
                // `sessions`, `journal`, `hostEventsSubscription`, `bindRunStarted`.
                // None is a hook name in the Host's `Hooks` type, so the Host never
                // read any of them; they were internal ports exposed for test
                // visibility, which is the one thing VERIFY-008 names as forbidden.
                // Two had no reader at all. Layer 1–3 tests reach these modules
                // directly through `build/next`.
                let hooks =
                    createObj
                        [ "chat.message", box (curriedHook wired.ChatMessageHook)
                          // Host built-in retry reuses the same user message;
                          // 0.5.0 relies on Agent bindings — chat.params is a no-op.
                          "chat.params", box (curriedHook chatParams)
                          // ONE transform registration.
                          //
                          // Both `chat.transform` and this key used to point at the
                          // same function "for compatibility". Host source has only
                          // the experimental name — the other is absent from the
                          // `Hooks` type and triggered nowhere; `prompt.ts:1255` and
                          // `compaction.ts:350` are the only trigger sites. So the
                          // extra key was never a fallback; it was a second live
                          // registration of one hook, and every provider step ran the
                          // Companion rewrite and the REVIEW-010 seal twice over the
                          // same message array.
                          "experimental.chat.messages.transform", box (pairedHook (box transform))
                          // HOST-006 prevention layer. The config hook is the only
                          // place the plugin can reach the compaction settings: the
                          // Host hands over the live instance-state object and runs
                          // this before other services (`bootstrap.ts:36`), so a write
                          // here is in force before anything reads it.
                          //
                          // `enforceSettings` reports the first key it could not
                          // establish. That is carried to the startup probe rather than
                          // thrown here: HOST-006's verdict needs both halves — the
                          // settings AND the first turn — and failing at config time
                          // would report the symptom without the observation.
                          "config",
                          box (fun (config: obj) ->
                              ManagerConfig.configureManager config
                              scope.RecordCompactionSettingGap(HostCompactionGate.enforceSettings config))
                          // HOST-006: this hook cannot refuse a compaction — its output
                          // has no cancel field (`plugin/index.ts:305`) and
                          // `plugin.trigger` discards the return value. Registered
                          // anyway so the containment layer has a same-turn signal, and
                          // so the absence of a veto is documented at the boundary
                          // rather than inferred from silence.
                          "experimental.session.compacting",
                          box (pairedHook (box HostCompactionGate.onSessionCompacting))
                          // HOST-006: always `enabled = false`. `compaction.auto=false`
                          // already makes the replay branch unreachable, but this is the
                          // one vetoable synthetic-turn injection point, and leaving it
                          // unanswered relies on an upstream default staying harmless.
                          "experimental.compaction.autocontinue",
                          box (pairedHook (box HostCompactionGate.onCompactionAutoContinue)) ]

                hooks?event <- box wired.ObserveEvent

                // HOST-009 dispose: cancel owned Tasks, kill PTYs/processes, dispose
                // sessions. `scope.Dispose` owns all of it, and the Host awaits this
                // hook (`plugin/index.ts:266`), so teardown completes before shutdown
                // proceeds.
                hooks?dispose <- box (fun () -> scope.Dispose())

                let client = if isNull input then null else input?client

                if not (isNull client) then
                    try
                        let! toolModule = importToolModule ()

                        let onRunStarted =
                            Some(fun sessionId role directory -> wired.BindActiveRun sessionId role directory)

                        // Child background SSOT (EXEC-008): the parent's frozen
                        // LifecycleWorkRecord at creation time — Opening + Y frames
                        // + X gap + terminal, one algorithm for every state.
                        let backgroundBFor =
                            Some(fun sessionId ->
                                let fromLwr = XTraceCapture.parentWorkRecord journal (SessionId.create sessionId)

                                match fromLwr with
                                | Some text -> Some text
                                | None ->
                                    // Pre-LWR fallback: EffectiveFrames is the compressed
                                    // middle when no opening has been captured yet.
                                    match scope.Companions.TryGetValue sessionId with
                                    | true, host ->
                                        host.Memory.EffectiveFrames
                                        |> Option.filter (fun text -> not (String.IsNullOrWhiteSpace text))
                                    | false, _ -> None)

                        let toolRegistration =
                            toolHooks
                                toolModule
                                sessionPort
                                journal
                                gitTreePort
                                (PluginHost.workspaceDirectory input)
                                scope
                                wired.CurrentPhysicalUserMessage
                                onRunStarted
                                backgroundBFor
                                snapshotOpt
                                (Some wired.CancelSignals)
                                (Some eventPort)

                        scope.AttachToolRuntime(toolRegistration.Runtime :> ISessionRuntimeOwner)
                        hooks?tool <- toolRegistration.Tools
                    with ex ->
                        raise (InvalidOperationException(sprintf "Failed to load OpenCode tool module: %s" ex.Message))

                return box hooks
        }
