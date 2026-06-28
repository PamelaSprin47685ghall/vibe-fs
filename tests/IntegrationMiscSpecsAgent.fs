module Wanxiangshu.Tests.IntegrationMiscSpecsAgent

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Opencode.AgentConfig
open Wanxiangshu.Shell.Dyn

let agentConfigSpec () = promise {
    let! workspaceDir = mkdtempAsync "agent-config-"
    let! p = plugin (box {| directory = workspaceDir |})
    let cfgInput =
        box {|
            agent = box {|
                browser = box {| model = "kimi-for-coding/k2p7" |}
                executor = box {| model = "opencode-go/deepseek-v4-flash" |}
                custom = box {| model = "custom-model" |}
            |}
        |}
    let! cfg = (get p "config") $ cfgInput |> unbox<JS.Promise<obj>>
    let agents = get cfg "agent"
    let browser = get agents "browser"
    check "browser prompt empty" (str browser "prompt" = "")
    check "browser mode subagent" (str browser "mode" = "subagent")
    let executor = get agents "executor"
    check "executor mode subagent" (str executor "mode" = "subagent")
    let custom = get agents "custom"
    check "custom model preserved" (str custom "model" = "custom-model")
    let manager = get agents "manager"
    check "manager mode primary" (str manager "mode" = "primary")
    do! rmAsync workspaceDir
}

let bookkeeperAgentConfigSpec () = promise {
    let! workspaceDir = mkdtempAsync "no-bookkeeper-agent-config-"
    let! p = plugin (box {| directory = workspaceDir |})
    let! cfg = (get p "config") $ (createObj []) |> unbox<JS.Promise<obj>>
    let agents = get cfg "agent"
    check "bookkeeper agent removed" (isNullish (get agents "bookkeeper"))
    do! rmAsync workspaceDir
}

let disableMimoMemoryAndCheckpointSpec () = promise {
    let cfg = createObj []
    let next = disableMimoMemoryAndCheckpoint cfg
    let agents = get next "agent"
    check "dream agent disabled" (truthy (get (get agents "dream") "disable"))
    check "distill agent disabled" (truthy (get (get agents "distill") "disable"))
    check "checkpoint-writer agent disabled" (truthy (get (get agents "checkpoint-writer") "disable"))
    let checkpoint = get next "checkpoint"
    let thresholds = get checkpoint "thresholds"
    check "checkpoint.thresholds empty array"
        (isArray thresholds && (unbox<obj[]> thresholds).Length = 0)
    let pushCaps = get checkpoint "push_caps"
    for cap in [|
        "tasks_ledger"; "focus_task"; "actor_ledger"; "memory_titles"
        "global"; "checkpoint"; "memory"; "notes"
        "design_decisions"; "open_notes" |] do
        check $"checkpoint.push_caps.{cap}=0" (unbox<int> (get pushCaps cap) = 0)
    check "checkpoint.memory_reconcile_on_search=false"
        (unbox<bool> (get checkpoint "memory_reconcile_on_search") = false)
    check "dream.auto=false" (unbox<bool> (get (get next "dream") "auto") = false)
    check "distill.auto=false" (unbox<bool> (get (get next "distill") "auto") = false)
    check "memory.cc_index=false" (unbox<bool> (get (get next "memory") "cc_index") = false)
}

let disableMimoMemoryAndCheckpointPreservesUserAgentSpec () = promise {
    let cfg =
        createObj [
            "agent", box (createObj [
                "dream", box {| model = "user-model"; prompt = "user-prompt" |}
            ])
            "checkpoint", box {| thresholds = [| "10%" |] |}
        ]
    let next = disableMimoMemoryAndCheckpoint cfg
    let dream = get (get next "agent") "dream"
    check "user dream model preserved" (str dream "model" = "user-model")
    check "user dream prompt preserved" (str dream "prompt" = "user-prompt")
    check "user dream disable injected" (truthy (get dream "disable"))
    let checkpoint = get next "checkpoint"
    let thresholds = get checkpoint "thresholds"
    check "user checkpoint.thresholds overridden empty"
        (isArray thresholds && (unbox<obj[]> thresholds).Length = 0)
}

let applyAgentConfigForMimoDisablesWorkflowSpec () = promise {
    let cfg = createObj []
    let next = applyAgentConfigFor Wanxiangshu.Kernel.HostTools.mimocode cfg (createObj [])
    let agents = get next "agent"
    for name in [| "manager"; "build"; "plan"; "coder"; "investigator"; "meditator"; "reviewer"; "browser"; "executor" |] do
        let agent = get agents name
        let permissions = get agent "permission"
        let tools = get agent "tools"
        check $"mimo {name} permission.workflow deny" (str permissions "workflow" = "deny")
        check $"mimo {name} tools.workflow false" (unbox<bool> (get tools "workflow") = false)
}

let pluginConfigHookDisablesMimoMemoryAndCheckpointSpec () = promise {
    let! workspaceDir = mkdtempAsync "plugin-disable-mimo-"
    let! p = plugin (box {| directory = workspaceDir |})
    let! cfg = (get p "config") $ (createObj []) |> unbox<JS.Promise<obj>>
    let agents = get cfg "agent"
    check "config hook dream disabled" (truthy (get (get agents "dream") "disable"))
    check "config hook distill disabled" (truthy (get (get agents "distill") "disable"))
    check "config hook checkpoint-writer disabled" (truthy (get (get agents "checkpoint-writer") "disable"))
    let checkpoint = get cfg "checkpoint"
    let thresholds = get checkpoint "thresholds"
    check "config hook checkpoint.thresholds empty"
        (isArray thresholds && (unbox<obj[]> thresholds).Length = 0)
    check "config hook dream.auto=false" (unbox<bool> (get (get cfg "dream") "auto") = false)
    check "config hook distill.auto=false" (unbox<bool> (get (get cfg "distill") "auto") = false)
    check "config hook memory.cc_index=false" (unbox<bool> (get (get cfg "memory") "cc_index") = false)
    do! rmAsync workspaceDir
}