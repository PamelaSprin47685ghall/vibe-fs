namespace Wanxiangshu.OpenCode

open Wanxiangshu.Execution.Session.ChatExecution

module PluginRecoveryWiring =

    let attach (boot: PluginBoot.Boot) : unit =
        let scope = boot.Scope

        scope.AttachDurabilityActivation(fun () ->
            scope.RunBackground(fun () ->
                task {
                    do! scope.SignalChatRecovery(ChatExecutionRecoveryLifecycleEvent.PluginRuntimeReloaded)

                    do! scope.SignalChatRecovery(ChatExecutionRecoveryLifecycleEvent.CapacityProjectionReplayed)
                }))
