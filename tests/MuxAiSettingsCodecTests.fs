module Wanxiangshu.Tests.MuxAiSettingsCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.DelegatedAiSettings
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.MuxAiSettingsCodec

let decodeMuxDelegateConfigOk () =
    let runtime = createObj [ "x", box 1 ]

    let config =
        createObj
            [ "workspaceId", box "ws-delegate"
              "cwd", box "/tmp/ws"
              "runtime", runtime
              "sessionID", box "sess-1" ]

    match decodeMuxDelegateConfig config with
    | Ok d ->
        check "delegate workspaceId" (d.Execution.WorkspaceId |> Option.map Id.workspaceIdValue = Some "ws-delegate")
        check "delegate cwd" (d.Cwd = "/tmp/ws")
        check "delegate session" (Id.sessionIdValue d.Execution.SessionId = "sess-1")
        check "delegate runtime ref" (obj.ReferenceEquals(d.Runtime, runtime))
    | Error _ -> check "delegate ok" false

let decodeMuxDelegateConfigMissingWorkspaceId () =
    let config = createObj [ "cwd", box "/tmp" ]

    match decodeMuxDelegateConfig config with
    | Error(InvalidIntent("mux", "workspaceId", "required")) -> check "delegate missing workspaceId" true
    | _ -> check "delegate missing workspaceId" false

let decodeMuxDelegateConfigLenientMissingWorkspaceId () =
    let runtime = createObj [ "k", box 1 ]
    let config = createObj [ "cwd", box "/tmp/lenient"; "runtime", runtime ]
    let d = decodeMuxDelegateConfigLenient config
    check "lenient workspaceId none" (d.Execution.WorkspaceId = None)
    check "lenient cwd" (d.Cwd = "/tmp/lenient")
    check "lenient runtime ref" (obj.ReferenceEquals(d.Runtime, runtime))

let coerceThinkingLevelPublic () =
    equal "coerce med" (Some "medium") (coerceThinkingLevel "med")
    check "coerce bogus" (coerceThinkingLevel "bogus" = None)

let decodeMuxParentRuntimeEnvScalars () =
    let muxEnv =
        createObj [ "MUX_MODEL_STRING", box " openai:gpt-5 "; "MUX_THINKING_LEVEL", box "med" ]

    let s = decodeMuxParentRuntimeEnv (unbox muxEnv)
    equal "parent model trim" (Some "openai:gpt-5") s.ModelString
    equal "parent thinking med" (Some "medium") s.ThinkingLevel

let decodeMuxParentRuntimeEnvNullish () =
    let s = decodeMuxParentRuntimeEnv null
    check "parent null model" (s.ModelString = None)
    check "parent null thinking" (s.ThinkingLevel = None)

let readParentMuxEnvScalars () =
    let config =
        createObj
            [ "muxEnv",
              box (
                  createObj
                      [ "MUX_MODEL_STRING", box " anthropic:parent "
                        "MUX_THINKING_LEVEL", box "xhigh" ]
              ) ]

    let s = readParentMuxEnv config
    equal "readParent model trim" (Some "anthropic:parent") s.ModelString
    equal "readParent thinking xhigh" (Some "xhigh") s.ThinkingLevel

let readParentMuxEnvMissingMuxEnv () =
    let s = readParentMuxEnv (createObj [ "cwd", box "/tmp" ])
    check "readParent no muxEnv model" (s.ModelString = None)
    check "readParent no muxEnv thinking" (s.ThinkingLevel = None)

let decodeAgentAiEntryScalarsModelPrecedence () =
    let entry =
        createObj
            [ "model", box "openai:from-model"
              "modelString", box "ignored"
              "thinkingLevel", box " high " ]

    let s = decodeAgentAiEntryScalars (unbox entry)
    equal "entry model" (Some "openai:from-model") s.Model
    equal "entry modelString" (Some "ignored") s.ModelString
    equal "entry thinking trim" (Some "high") s.ThinkingLevel

let decodeAgentAiEntryScalarsNullish () =
    let s = decodeAgentAiEntryScalars null
    check "entry null model" (s.Model = None)
    check "entry null modelString" (s.ModelString = None)
    check "entry null thinking" (s.ThinkingLevel = None)

let normalizeTrimmedStrBlank () =
    check "normalize blank" (normalizeTrimmedStr (box "   ") = None)
    equal "normalize value" (Some "x") (normalizeTrimmedStr (box " x "))

let readMuxConfigFileDefaultsSubagentFirst () =
    let configFile =
        createObj
            [ "subagentAiDefaults",
              box (createObj [ "coder", box (createObj [ "model", box "openai:sub"; "thinkingLevel", box "low" ]) ])
              "agentAiDefaults",
              box (createObj [ "coder", box (createObj [ "model", box "openai:agent"; "thinkingLevel", box "high" ]) ]) ]

    let sources: DelegatedAiSettings option list =
        readMuxConfigFileDefaults configFile "coder"

    check "defaults list length" (sources.Length = 2)

    match sources.[0] with
    | Some s -> equal "subagent model" (Some "openai:sub") s.modelString
    | None -> check "subagent present" false

let run () =
    readMuxConfigFileDefaultsSubagentFirst ()
    decodeMuxDelegateConfigOk ()
    decodeMuxDelegateConfigMissingWorkspaceId ()
    decodeMuxDelegateConfigLenientMissingWorkspaceId ()
    coerceThinkingLevelPublic ()
    decodeMuxParentRuntimeEnvScalars ()
    decodeMuxParentRuntimeEnvNullish ()
    readParentMuxEnvScalars ()
    readParentMuxEnvMissingMuxEnv ()
    decodeAgentAiEntryScalarsModelPrecedence ()
    decodeAgentAiEntryScalarsNullish ()
    normalizeTrimmedStrBlank ()
