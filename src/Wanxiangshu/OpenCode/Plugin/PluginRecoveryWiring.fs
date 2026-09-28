namespace Wanxiangshu.OpenCode

open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.OpenCode.Host

module PluginRecoveryWiring =

    let attach (boot: PluginBoot.Boot) : unit =
        let scope = boot.Scope

        // Restart dropped the process-local execution bindings; the durable
        // handle records are the evidence for which parented children a road
        // still owns, so its fixed DevOps can be dispatched to again
        // (crash-reconciliation-020).
        match boot.Journal with
        | Some journal -> SessionBindingRecovery.restoreFromDurable journal
        | None -> ()

        scope.AttachDurabilityActivation(fun () ->
            scope.RunBackground(fun () ->
                task {
                    do! scope.SignalChatRecovery(ChatExecutionRecoveryLifecycleEvent.PluginRuntimeReloaded)

                    do! scope.SignalChatRecovery(ChatExecutionRecoveryLifecycleEvent.CapacityProjectionReplayed)
                }))
