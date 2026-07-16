module Wanxiangshu.Tests.FallbackHooksHelperAgentModelTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Tests.Assert

/// Mock client shaped like a real OpenCode SDK client's config sub-API:
/// client.config.get() resolves to { data: Config } where Config.agent is a
/// map of agent name -> AgentConfig, AgentConfig.model?: string.
let private mkClientWithAgentModel (agentName: string) (model: string) : obj =
    let configApi =
        createObj
            [ "get",
              box (fun () ->
                  promise {
                      return
                          box
                              {| data =
                                  box {| agent = createObj [ agentName, box (createObj [ "model", box model ]) ] |} |}
                  }) ]

    createObj [ "config", box configApi ]

let private mkClientWithNoModelConfigured (agentName: string) : obj =
    let configApi =
        createObj
            [ "get",
              box (fun () ->
                  promise { return box {| data = box {| agent = createObj [ agentName, box (createObj []) ] |} |} }) ]

    createObj [ "config", box configApi ]

/// Agent has an explicit model in opencode.jsonc — must return Some.
let agentWithExplicitModelReturnsSome () =
    promise {
        let client = mkClientWithAgentModel "investigator" "openai/gpt-5"
        let! result = tryGetAgentExplicitModel client "investigator"
        equal "explicit model found" (Some "openai/gpt-5") result
    }

/// Agent entry exists but has no model field — must return None, not throw.
let agentWithoutModelFieldReturnsNone () =
    promise {
        let client = mkClientWithNoModelConfigured "investigator"
        let! result = tryGetAgentExplicitModel client "investigator"
        equal "no model configured" None result
    }

/// Client entirely lacking a config sub-API (e.g. IntegrationSubagentMockClient-
/// style test doubles) must degrade to None, never throw.
let clientWithoutConfigApiReturnsNone () =
    promise {
        let client = createObj [ "session", box (createObj []) ]
        let! result = tryGetAgentExplicitModel client "investigator"
        equal "missing config api degrades to None" None result
    }

/// Nullish client must degrade to None, never throw.
let nullClientReturnsNone () =
    promise {
        let! result = tryGetAgentExplicitModel null "investigator"
        equal "null client degrades to None" None result
    }

let run () : JS.Promise<unit> =
    promise {
        do! agentWithExplicitModelReturnsSome ()
        do! agentWithoutModelFieldReturnsNone ()
        do! clientWithoutConfigApiReturnsNone ()
        do! nullClientReturnsNone ()
    }
