module Wanxiangshu.Tests.ResolveAiSettingsTests

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert

open Wanxiangshu.Mux.AiSettings
open Wanxiangshu.Shell.MuxAiSettingsCodec

let private settingsEqual (label: string) (expectedModel: string option) (expectedThinking: string option) (actual: DelegatedAiSettings) =
    equal $"{label} model" expectedModel actual.modelString
    equal $"{label} thinking" expectedThinking actual.thinkingLevel

let private entry (model: string) (thinking: string) =
    createObj [ "model", box model; "thinkingLevel", box thinking ]

let private defaultsEntry (modelString: string) (thinkingLevel: string) =
    createObj [ "modelString", box modelString; "thinkingLevel", box thinkingLevel ]

let mergeSubagentBeforeAgent () =
    let configFile =
        createObj [
            "subagentAiDefaults",
            box (createObj [ "explore", box (defaultsEntry "openai:subagent" "xhigh") ])
            "agentAiDefaults",
            box (createObj [ "explore", box (defaultsEntry "openai:agent" "medium") ])
        ]

    let merged = mergeNamedSettings (readMuxConfigFileDefaults configFile "explore")
    settingsEqual "subagent wins over agent" (Some "openai:subagent") (Some "xhigh") merged

let modelStringKeyOnConfigDefaults () =
    let fromModelStringScalars =
        decodeAgentAiEntryScalars (
            createObj [ "modelString", box "anthropic:from-key"; "thinkingLevel", box "low" ])

    equal "modelString key" (Some "anthropic:from-key") (fromModelStringScalars.Model |> Option.orElse fromModelStringScalars.ModelString)

    let fromModelScalars =
        decodeAgentAiEntryScalars (
            createObj [ "model", box "openai:from-model"; "modelString", box "ignored" ])

    equal "model beats modelString" (Some "openai:from-model") (fromModelScalars.Model |> Option.orElse fromModelScalars.ModelString)

let blankModelSkipped () =
    let configFile =
        createObj [
            "subagentAiDefaults",
            box (createObj [ "explore", box (defaultsEntry "" "medium") ])
            "agentAiDefaults",
            box (createObj [ "explore", box (defaultsEntry "anthropic:from-agent-defaults" "low") ])
        ]

    let merged = mergeNamedSettings (readMuxConfigFileDefaults configFile "explore")
    settingsEqual "blank subagent model skipped" (Some "anthropic:from-agent-defaults") (Some "medium") merged

let run () =
    mergeSubagentBeforeAgent ()
    modelStringKeyOnConfigDefaults ()
    blankModelSkipped ()
