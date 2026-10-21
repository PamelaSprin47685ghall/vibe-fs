namespace Wanxiangshu.Execution.Session.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.Execution.Session
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider

/// Horizon-owned output surface. It translates plain roster observations into
/// provider prose/TOML while keeping Handle, Journal and PTY representations out
/// of semantic tests.
[<RequireQualifiedAccess>]
module HorizonSurface =
    let private text (value: obj) =
        if isNull value then "" else string value

    let private line language (path: string) (label: string) =
        ProviderProse.render language path (Map [ "label", label ])

    let private agentLines language (value: obj) =
        let label = text (value?label)
        let status = text (value?status)

        let statusPath =
            match status with
            | "returned" -> HorizonRoster.Path.Returned
            | "abandoned" -> HorizonRoster.Path.DidNotReturn
            | _ -> HorizonRoster.Path.StillAway

        let workPath = text (value?work)

        let work =
            match workPath with
            | "latest" ->
                ProviderProse.render
                    language
                    HorizonRoster.Path.LatestWork
                    (Map [ "label", label; "record", text (value?record) ])
            | "unavailable" -> line language HorizonRoster.Path.LatestWorkUnavailable label
            | _ -> line language HorizonRoster.Path.NoWorkYet label

        [ line language statusPath label; work ]

    let render (agents: obj array) (ptys: obj array) : string =
        // provider-language-008: the roster is LLM-facing prose; bind it to the
        // configured language instead of a hardcoded English default.
        let language = ProviderLanguageBinding.readGlobalPreference ()
        let agentLines = agents |> Array.toList |> List.collect (agentLines language)

        let ptyLines =
            ptys
            |> Array.toList
            |> List.sortBy (fun value -> text (value?ptyId))
            |> List.map (fun value ->
                let label = text (value?command)
                line language HorizonRoster.Path.RemainsOpen label)

        let lines = List.append agentLines ptyLines

        if List.isEmpty lines then
            ToolHostCodec.tomlObjectWithInstructions
                (ProviderProse.instructionLines language HorizonRoster.Path.EmptyRoster Map.empty)
                []
        else
            ToolHostCodec.tomlObjectWithInstructions lines []

    let unavailable () : string =
        ToolHostCodec.tomlObjectWithInstructions
            [ ProviderProse.render
                  (ProviderLanguageBinding.readGlobalPreference ())
                  HorizonRoster.Path.UnavailableFromContext
                  Map.empty ]
            []

    let cannotBeSeen () : string =
        ToolHostCodec.tomlObjectWithInstructions
            [ ProviderProse.render
                  (ProviderLanguageBinding.readGlobalPreference ())
                  HorizonRoster.Path.CannotBeSeen
                  Map.empty ]
            []

    let description () : string =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) HorizonRoster.Path.Description Map.empty
