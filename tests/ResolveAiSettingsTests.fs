module VibeFs.Tests.ResolveAiSettingsTests

open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dyn
open VibeFs.Mux.AiSettings

let private settingsEqual (label: string) (expectedModel: string option) (expectedThinking: string option) (actual: DelegatedAiSettings) =
    equal $"{label} model" expectedModel actual.modelString
    equal $"{label} thinking" expectedThinking actual.thinkingLevel

let private entry (model: string) (thinking: string) =
    createObj [ "model", box model; "thinkingLevel", box thinking ]

let private defaultsEntry (modelString: string) (thinkingLevel: string) =
    createObj [ "modelString", box modelString; "thinkingLevel", box thinkingLevel ]

let mergeSubagentBeforeAgent () =
    let sub =
        namedSettingsFromRecord
            (createObj [ "explore", box (defaultsEntry "openai:subagent" "xhigh") ])
            "explore"

    let agent =
        namedSettingsFromRecord
            (createObj [ "explore", box (defaultsEntry "openai:agent" "medium") ])
            "explore"

    let merged = mergeNamedSettings [ sub; agent ]
    settingsEqual "subagent wins over agent" (Some "openai:subagent") (Some "xhigh") merged

let modelStringKeyOnConfigDefaults () =
    let fromModelString =
        modelFromEntry (createObj [ "modelString", box "anthropic:from-key"; "thinkingLevel", box "low" ])

    equal "modelString key" (Some "anthropic:from-key") fromModelString

    let fromModel =
        modelFromEntry (createObj [ "model", box "openai:from-model"; "modelString", box "ignored" ])

    equal "model beats modelString" (Some "openai:from-model") fromModel

let blankModelSkipped () =
    let sub =
        namedSettingsFromRecord
            (createObj [ "explore", box (defaultsEntry "" "medium") ])
            "explore"

    let agent =
        namedSettingsFromRecord
            (createObj [ "explore", box (defaultsEntry "anthropic:from-agent-defaults" "low") ])
            "explore"

    let merged = mergeNamedSettings [ sub; agent ]
    settingsEqual "blank subagent model skipped" (Some "anthropic:from-agent-defaults") (Some "medium") merged

let run () =
    mergeSubagentBeforeAgent ()
    modelStringKeyOnConfigDefaults ()
    blankModelSkipped ()
