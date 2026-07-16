module Wanxiangshu.Tests.MuxAiSettingsIntegrationTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Hosts.Mux.AiSettings
open Wanxiangshu.Runtime.Dyn

let private settingsEqual
    (label: string)
    (expectedModel: string option)
    (expectedThinking: string option)
    (actual: DelegatedAiSettings)
    =
    equal $"{label} model" expectedModel actual.modelString
    equal $"{label} thinking" expectedThinking actual.thinkingLevel

let private agentEntry (model: string) (thinking: string) =
    createObj [ "model", box model; "thinkingLevel", box thinking ]

let resolveDelegatedAgentAiSettingsWorkspaceMergeSpec () =
    promise {
        let configFile =
            createObj
                [ "subagentAiDefaults", box (createObj [ "coder", box (agentEntry "openai:sub-default" "low") ])
                  "agentAiDefaults", box (createObj [ "coder", box (agentEntry "openai:agent-default" "medium") ]) ]

        let workspace =
            createObj
                [ "aiSettingsByAgent", box (createObj [ "coder", box (agentEntry "anthropic:ws-coder" "xhigh") ]) ]

        let deps =
            createObj
                [ "loadConfigOrDefault", box (fun () -> configFile)
                  "findWorkspaceEntry",
                  box (
                      System.Func<obj, string, obj>(fun (_cf: obj) (wid: string) ->
                          createObj [ "workspace", (if wid = "ws-int" then workspace else null) ])
                  )
                  "resolveAgentFrontmatter",
                  box (
                      System.Func<obj, obj, string, JS.Promise<obj>>(fun (_rt: obj) (_cwd: obj) (_agentId: string) ->
                          Promise.lift (
                              createObj
                                  [ "ai", box (createObj [ "model", box "openai:from-fm"; "thinkingLevel", box "max" ]) ]
                          ))
                  ) ]

        let config =
            createObj
                [ "workspaceId", box "ws-int"
                  "cwd", box "/tmp/ws"
                  "runtime", box (createObj []) ]

        let! resolved = resolveDelegatedAgentAiSettings deps config "coder"
        settingsEqual "workspace byAgent wins first in merge list" (Some "anthropic:ws-coder") (Some "xhigh") resolved
    }

let resolveDelegatedAgentAiSettingsLenientConfigSpec () =
    promise {
        let configFile = createObj []

        let deps =
            createObj
                [ "loadConfigOrDefault", box (fun () -> configFile)
                  "findWorkspaceEntry",
                  box (System.Func<obj, string, obj>(fun (_cf: obj) (_wid: string) -> createObj [ "workspace", null ]))
                  "resolveAgentFrontmatter",
                  box (
                      System.Func<obj, obj, string, JS.Promise<obj>>(fun (_rt: obj) (_cwd: obj) (_agentId: string) ->
                          Promise.lift (
                              createObj
                                  [ "ai",
                                    box (
                                        createObj
                                            [ "modelString", box "openai:descriptor-only"; "thinkingLevel", box "off" ]
                                    ) ]
                          ))
                  ) ]

        let config = createObj [ "cwd", box "/no-ws"; "runtime", box (createObj []) ]

        let! resolved = resolveDelegatedAgentAiSettings deps config "explore"
        settingsEqual "lenient config descriptor only" (Some "openai:descriptor-only") (Some "off") resolved
    }

let resolveDelegatedAgentAiSettingsFrontmatterRejectSpec () =
    promise {
        let deps =
            createObj
                [ "loadConfigOrDefault", box (fun () -> createObj [])
                  "findWorkspaceEntry",
                  box (System.Func<obj, string, obj>(fun (_cf: obj) (_wid: string) -> createObj [ "workspace", null ]))
                  "resolveAgentFrontmatter",
                  box (
                      System.Func<obj, obj, string, JS.Promise<obj>>(fun (_rt: obj) (_cwd: obj) (_agentId: string) ->
                          Promise.reject (exn "resolveAgentFrontmatter failed"))
                  ) ]

        let config = createObj [ "cwd", box "/tmp"; "runtime", box (createObj []) ]

        let! resolved = resolveDelegatedAgentAiSettings deps config "coder"
        settingsEqual "frontmatter reject yields emptySettings merge" None None resolved
    }

let resolveDelegatedAgentAiSettingsDefaultsOnlyMergeSpec () =
    promise {
        let configFile =
            createObj
                [ "subagentAiDefaults", box (createObj [ "coder", box (agentEntry "openai:sub-default" "low") ])
                  "agentAiDefaults", box (createObj [ "coder", box (agentEntry "openai:agent-default" "medium") ]) ]

        let deps =
            createObj
                [ "loadConfigOrDefault", box (fun () -> configFile)
                  "findWorkspaceEntry",
                  box (System.Func<obj, string, obj>(fun (_cf: obj) (_wid: string) -> createObj [ "workspace", null ]))
                  "resolveAgentFrontmatter",
                  box (
                      System.Func<obj, obj, string, JS.Promise<obj>>(fun (_rt: obj) (_cwd: obj) (_agentId: string) ->
                          Promise.lift (createObj []))
                  ) ]

        let config =
            createObj
                [ "workspaceId", box "ws-none"
                  "cwd", box "/tmp"
                  "runtime", box (createObj []) ]

        let! resolved = resolveDelegatedAgentAiSettings deps config "coder"
        settingsEqual "defaults-only subagent beats agent" (Some "openai:sub-default") (Some "low") resolved
    }

let run () =
    promise {
        do! resolveDelegatedAgentAiSettingsWorkspaceMergeSpec ()
        do! resolveDelegatedAgentAiSettingsDefaultsOnlyMergeSpec ()
        do! resolveDelegatedAgentAiSettingsLenientConfigSpec ()
        do! resolveDelegatedAgentAiSettingsFrontmatterRejectSpec ()
    }
