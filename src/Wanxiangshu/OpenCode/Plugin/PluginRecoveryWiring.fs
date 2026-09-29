namespace Wanxiangshu.OpenCode

open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode.Host

module PluginRecoveryWiring =

    let private isFlightActive
        (bloggerHost: IBloggerRuntimeHost)
        (bloggerSessionId: SessionId)
        (requestId: BloggerRequestId)
        : bool =
        match bloggerHost.TryGetFlight(SessionId.value bloggerSessionId) with
        | Some flight -> BloggerRequestContext.requestId flight = requestId
        | None -> false

    let attach (boot: PluginBoot.Boot) : unit =
        let scope = boot.Scope

        // Restart dropped the process-local execution bindings; the durable
        // handle records are the evidence for which parented children a road
        // still owns, so its fixed DevOps can be dispatched to again
        // (crash-reconciliation-020).
        match boot.Journal with
        | Some journal -> SessionBindingRecovery.install journal
        | None -> ()

        scope.AttachDurabilityActivation(fun () ->
            scope.RunBackground(fun () ->
                task {
                    // crash-reconciliation-020: settle the child work runs the
                    // previous runtime left active, so the next handoff to that
                    // child is a fresh root instead of a refused identity.
                    match boot.Journal with
                    | Some journal ->
                        do! ChildWorkRecovery.settleOrphanedChildRuns journal

                        // ... and the Blog materializations it left open. No live
                        // execution can own one, and while it stays open the
                        // coordinator never materializes a fresh request — the
                        // Blogger would never ingest the raw tail again
                        // (crash-reconciliation-020 / context-compression-024).
                        let bloggerHost = scope.BloggerRuntimeHost
                        let liveFlight = isFlightActive bloggerHost

                        do! BloggerAbandon.settleStaleOpenAtLoad liveFlight journal
                    | None -> ()

                    do! scope.SignalChatRecovery(ChatExecutionRecoveryLifecycleEvent.PluginRuntimeReloaded)

                    do! scope.SignalChatRecovery(ChatExecutionRecoveryLifecycleEvent.CapacityProjectionReplayed)
                }))
