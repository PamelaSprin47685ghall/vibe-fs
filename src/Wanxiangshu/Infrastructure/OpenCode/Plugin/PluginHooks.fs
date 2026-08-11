namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.Infrastructure
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open PluginHostInterop

module PluginHooks =

    /// Host hook surface: chat / transform / config / compaction / text /
    /// tool hooks plus event + dispose, and the optional client tool module.
    let create (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) (transform: obj -> obj -> Task<unit>) : Task<obj> =
        task {
            let scope = boot.Scope
            let journal = boot.Journal
            let wired = host.Wired
            let workspaceDirectory = boot.WorkspaceDirectory
            let input = boot.Input
            let sessionPort = host.SessionPort
            let snapshotOpt = host.SnapshotOpt
            let gitTreePort = host.GitTreePort
            let eventPort = host.EventPort

            let chatParams = ChatParamsHook.create journal

            // CASE-003: typed capture at the tool boundary — shared
            // CasebookLifecycle.collector; marker flag gates the after-hook.
            // Store IO stays out of SpikePlugin (unified-store dual-write gate).
            let observationCollector = CasebookLifecycle.collector

            let casebookEnabled =
                match workspaceDirectory with
                | Some ws -> CasebookFeature.isEnabled ws
                | None -> false

            // HOST-009: the object handed to the Host carries Host hooks and
            // nothing else.
            //
            // Six extra keys used to hang here — `projection`, `events`,
            // `sessions`, `journal`, `hostEventsSubscription`, `bindRunStarted`.
            // None is a hook name in the Host's `Hooks` type, so the Host never
            // read any of them; they were internal ports exposed for test
            // visibility, which is the one thing VERIFY-008 names as forbidden.
            // Two had no reader at all. Layer 1–3 tests reach these modules
            // directly through `dist`.
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
                      "experimental.chat.messages.transform", box (curriedHook (box transform))
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
                          let inventory = ManagerConfig.configureManager config
                          scope.Strength.RecordManagedAgentInventory inventory
                          scope.RecordCompactionSettingGap(HostCompactionGate.enforceSettings config))
                      // HOST-006: this hook cannot refuse a compaction — its output
                      // has no cancel field (`plugin/index.ts:305`) and
                      // `plugin.trigger` discards the return value. Registered
                      // anyway so the containment layer has a same-turn signal, and
                      // so the absence of a veto is documented at the boundary
                      // rather than inferred from silence.
                      "experimental.session.compacting", box (pairedHook (box HostCompactionGate.onSessionCompacting))
                      // HOST-006: always `enabled = false`. `compaction.auto=false`
                      // already makes the replay branch unreachable, but this is the
                      // one vetoable synthetic-turn injection point, and leaving it
                      // unanswered relies on an upstream default staying harmless.
                      "experimental.compaction.autocontinue",
                      box (pairedHook (box HostCompactionGate.onCompactionAutoContinue))
                      "experimental.text.complete",
                      box (
                          pairedHook (
                              box (fun (textInput: obj) (textOutput: obj) ->
                                  scope.SyncDelegateRuntime
                                  |> Option.iter (fun runtime -> runtime.TextComplete(textInput, textOutput)))
                          )
                      )
                      // CASE-003: typed observation capture at the tool
                      // execution boundary (read/glob/grep). Best-effort —
                      // unparseable executions are skipped, capture never
                      // changes the tool result. Registered only when the
                      // Casebook marker exists.
                      "tool.execute.after",
                      box (
                          pairedHook (
                              box (fun (toolInput: obj) (toolOutput: obj) ->
                                  if casebookEnabled then
                                      let toolName = if isNull toolInput then "" else string (toolInput?tool)

                                      let sessionId =
                                          if isNull toolInput then
                                              ""
                                          else
                                              string (toolInput?sessionID)

                                      let rendered = if isNull toolOutput then "" else string (toolOutput?output)

                                      if not (System.String.IsNullOrWhiteSpace sessionId) then
                                          observationCollector.Collect(sessionId, toolName, toolInput?args, rendered))
                          )
                      ) ]

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

                    // EXEC-006 / EXEC-008: same LWR materialiser, direction-dependent Opening.
                    // parent → child: includeOpening=true；child → parent: false.
                    let parentWorkRecordFor =
                        Some(fun sessionId ->
                            XTraceCapture.lifecycleWorkRecord journal (SessionId.create sessionId) true)

                    let childWorkRecordFor =
                        Some(fun sessionId ->
                            XTraceCapture.lifecycleWorkRecord journal (SessionId.create sessionId) false)

                    let finalityReviewerTimeoutMs =
                        let configured: obj = input?finalityReviewerTimeoutMs

                        if isNull configured then
                            None
                        else
                            Some(unbox<int> configured)

                    let casebookToolSpecs =
                        match workspaceDirectory with
                        | Some ws -> CasebookTools.buildSpecs (ToolHostCodec.factory toolModule) ws
                        | None -> []

                    let toolRegistration =
                        toolHooks
                            toolModule
                            sessionPort
                            journal
                            gitTreePort
                            (workspaceDirectory)
                            scope
                            wired.CurrentPhysicalUserMessage
                            onRunStarted
                            parentWorkRecordFor
                            childWorkRecordFor
                            snapshotOpt
                            (Some wired.CancelSignals)
                            (Some eventPort)
                            finalityReviewerTimeoutMs
                            casebookToolSpecs

                    scope.AttachToolRuntime(toolRegistration.Runtime :> ISessionRuntimeOwner)
                    hooks?tool <- toolRegistration.Tools
                with ex ->
                    raise (InvalidOperationException(sprintf "Failed to load OpenCode tool module: %s" ex.Message))

            return box hooks
        }
