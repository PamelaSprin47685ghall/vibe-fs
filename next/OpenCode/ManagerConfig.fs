namespace Wanxiangshu.Next.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.Next.Tools

module ManagerConfig =

    let configureManager (config: obj) : unit =
        if not (isNull config) then
            let agents =
                if isNull config?agent then
                    let created: obj = createEmpty
                    config?agent <- created
                    created
                else
                    config?agent

            let managerConfig = StaticTools.managerAgentConfig ()
            agents?manager <- managerConfig
            agents?build <- managerConfig
            agents?plan <- managerConfig
            agents?orchestrator <- StaticTools.orchestratorAgentConfig ()
            agents?coder <- StaticTools.coderAgentConfig ()
            let toollessConfig = StaticTools.toollessAgentConfig ()
            agents?blogger <- toollessConfig
            agents?executor <- StaticTools.executorAgentConfig ()
            agents?inspector <- StaticTools.inspectorAgentConfig ()
            agents?devops <- StaticTools.devopsAgentConfig ()
            agents?browser <- StaticTools.browserAgentConfig ()
            agents?meditator <- StaticTools.meditatorAgentConfig ()
            agents?reviewer <- StaticTools.reviewerAgentConfig ()
            // SSOT §3 declares auto-compaction OFF, but no production config set it
            // (only the OPENCODE_DISABLE_AUTOCOMPACT test env var did). The opencode
            // host only honors it via cfg.compaction.auto === false (host-docs 05.md:331),
            // so emit it explicitly on the mutated config object.
            config?compaction <- createObj [ "auto" ==> false ]

            // Optional: WANXIANGSHU_CHAT_MAX_RETRIES=0 disables host built-in
            // provider retries so PluginFallbackRetry owns A/A/B/B with EffectiveModel.
            // Host chat.params cannot change model mid-retry; only a new prompt can.
            match System.Environment.GetEnvironmentVariable("WANXIANGSHU_CHAT_MAX_RETRIES") with
            | null
            | "" -> ()
            | raw ->
                match System.Int32.TryParse raw with
                | true, n when n >= 0 ->
                    let experimental =
                        if isNull config?experimental then
                            let created: obj = createEmpty
                            config?experimental <- created
                            created
                        else
                            config?experimental

                    experimental?chatMaxRetries <- n
                | _ -> ()

// Do not write unknown host config keys here: an invalid field can
// prevent the whole agent registration map from loading.
