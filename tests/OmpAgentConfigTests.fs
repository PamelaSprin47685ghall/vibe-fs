module Wanxiangshu.Tests.OmpAgentConfigTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Omp.AgentConfig

/// applyAgentConfigFor must hand back a config with the canonical 9 Omp agents
/// registered, each carrying the right `mode` (primary for manager/build/plan,
/// subagent for everything else) and a non-null `permission` / `tools` object
/// derived from Kernel.HostTools.canUseForHost.
let applyAgentConfigForRegistersBuiltinAgents () =
    let cfg = applyAgentConfigFor (createObj [])
    let agents = get cfg "agent"
    let primaryNames = [| "manager"; "build"; "plan" |]

    let subagentNames =
        [| "coder"; "inspector"; "meditator"; "reviewer"; "browser"; "executor" |]

    for name in primaryNames do
        let a = get agents name
        check $"primary mode {name}" (str a "mode" = "primary")

    let manager = get agents "manager"
    check "manager prompt mentions todowrite" ((str manager "prompt").Contains "todowrite")

    for name in subagentNames do
        let a = get agents name
        check $"subagent mode {name}" (str a "mode" = "subagent")

    let reviewer = get agents "reviewer"
    let reviewerPerm = get reviewer "permission"
    let reviewerTools = get reviewer "tools"
    check "reviewer permission object exists" (not (isNullish reviewerPerm))
    check "reviewer tools object exists" (not (isNullish reviewerTools))

/// User overrides must survive applyAgentConfigFor: their `prompt` and `model`
/// on a builtin agent win over the canonical defaults, but the disabled flags
/// and permission matrix are still injected.
let applyAgentConfigForPreservesUserOverrides () =
    let user =
        createObj
            [ "agent",
              box (
                  createObj
                      [ "coder",
                        box
                            {| model = "user-coder-model"
                               prompt = "user-coder-prompt" |}
                        "reviewer", box {| model = "user-reviewer-model" |} ]
              ) ]

    let cfg = applyAgentConfigFor user
    let agents = get cfg "agent"
    let coder = get agents "coder"
    check "user coder model preserved" (str coder "model" = "user-coder-model")
    check "user coder prompt preserved" (str coder "prompt" = "user-coder-prompt")
    let reviewer = get agents "reviewer"
    check "user reviewer model preserved" (str reviewer "model" = "user-reviewer-model")

/// Native agent disables are Omp's single switch for turning off the host's
/// built-in dream/distill/checkpoint-writer side-effects. Without these the
/// native agent runbook would clash with ours.
let disableNativeAgentsClearsMemoryAndCheckpoint () =
    let next = disableNativeAgents (createObj [])
    let agents = get next "agent"

    for name in [| "dream"; "distill"; "checkpoint-writer" |] do
        let a = get (get agents name) "disable"
        check $"native {name} disable true" (truthy a)

    let checkpoint = get next "checkpoint"
    let thresholds = get checkpoint "thresholds"
    check "checkpoint thresholds empty array" (isArray thresholds && (unbox<obj[]> thresholds).Length = 0)
    let pushCaps = get checkpoint "push_caps"

    for cap in
        [| "tasks_ledger"
           "focus_task"
           "actor_ledger"
           "memory_titles"
           "global"
           "checkpoint"
           "memory"
           "notes"
           "design_decisions"
           "open_notes" |] do
        check $"push_caps.{cap}=0" (unbox<int> (get pushCaps cap) = 0)

    check "dream.auto=false" (unbox<bool> (get (get next "dream") "auto") = false)
    check "distill.auto=false" (unbox<bool> (get (get next "distill") "auto") = false)
    check "memory.cc_index=false" (unbox<bool> (get (get next "memory") "cc_index") = false)

/// If the user already configured a `dream` agent, disableNativeAgents must
/// keep their prompt/model and only flip the disable bit.
let disableNativeAgentsPreservesUserOverrides () =
    let user =
        createObj
            [ "agent",
              box (
                  createObj
                      [ "dream",
                        box
                            {| model = "user-dream"
                               prompt = "user-dream-prompt" |}
                        "checkpoint", box {| thresholds = [| "10%" |] |} ]
              ) ]

    let next = disableNativeAgents user
    let dream = get (get next "agent") "dream"
    check "user dream model preserved" (str dream "model" = "user-dream")
    check "user dream prompt preserved" (str dream "prompt" = "user-dream-prompt")
    check "user dream disable injected" (truthy (get dream "disable"))
    let checkpoint = get next "checkpoint"
    let thresholds = get checkpoint "thresholds"
    check "user checkpoint thresholds overridden empty" (isArray thresholds && (unbox<obj[]> thresholds).Length = 0)

/// User-defined `permission` and `mcps` on a builtin agent must survive
/// `applyAgentConfigFor`. Only the missing keys are filled by the canonical
/// defaults; user keys win.
let applyAgentConfigForPreservesUserPermissionAndMcps () =
    let user =
        createObj
            [ "agent",
              box (
                  createObj
                      [ "coder",
                        box
                            {| model = "user-coder"
                               permission = {| edit = "deny" |}
                               mcps = [| "user-mcp" |] |} ]
              ) ]

    let cfg = applyAgentConfigFor user
    let coder = get (get cfg "agent") "coder"
    let perm = get coder "permission"
    check "user permission.edit preserved" (str perm "edit" = "deny")
    let mcps = get coder "mcps"

    check
        "user mcps preserved"
        (isArray mcps
         && (unbox<obj[]> mcps).Length = 1
         && (unbox<string[]> (unbox<obj[]> mcps |> Array.map string)).[0] = "user-mcp")

/// User-defined non-builtin agents (e.g. `qatester`) must also pass through
/// the agent map without being dropped or overwritten by canonical defaults.
let applyAgentConfigForKeepsUserCustomAgents () =
    let user =
        createObj
            [ "agent",
              box (
                  createObj
                      [ "qatester",
                        box
                            {| model = "custom-qa"
                               prompt = "QA specific instructions" |} ]
              ) ]

    let cfg = applyAgentConfigFor user
    let agents = get cfg "agent"
    let qa = get agents "qatester"
    check "user custom agent model preserved" (str qa "model" = "custom-qa")
    check "user custom agent prompt preserved" (str qa "prompt" = "QA specific instructions")

/// disableNativeAgents replaces the user's `checkpoint` section entirely with
/// the canonical disabled shape — this is intentional, because the host's
/// checkpoint writer is the side-effect we want to neutralise. Custom user
/// keys inside `checkpoint` will not survive the merge; only the disabled
/// shape is guaranteed.
let disableNativeAgentsReplacesCheckpointSection () =
    let user = createObj [ "checkpoint", box {| signal_every_n_turns = 5 |} ]
    let next = disableNativeAgents user
    let checkpoint = get next "checkpoint"
    let thresholds = get checkpoint "thresholds"
    check "canonical threshold empty array preserved" (isArray thresholds && (unbox<obj[]> thresholds).Length = 0)
    let pushCaps = get checkpoint "push_caps"
    check "canonical push_caps.tasks_ledger=0" (unbox<int> (get pushCaps "tasks_ledger") = 0)

    check
        "canonical memory_reconcile_on_search=false"
        (unbox<bool> (get checkpoint "memory_reconcile_on_search") = false)

    let dream = get next "dream"
    check "canonical dream.auto=false" (unbox<bool> (get dream "auto") = false)
