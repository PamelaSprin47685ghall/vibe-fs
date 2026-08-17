namespace Wanxiangshu.Execution.Session.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources

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
            | "returned" -> HorizonTool.Path.Returned
            | "abandoned" -> HorizonTool.Path.DidNotReturn
            | _ -> HorizonTool.Path.StillAway

        let workPath = text (value?work)

        let work =
            match workPath with
            | "latest" ->
                ProviderProse.render
                    language
                    HorizonTool.Path.LatestWork
                    (Map [ "label", label; "record", text (value?record) ])
            | "unavailable" -> line language HorizonTool.Path.LatestWorkUnavailable label
            | _ -> line language HorizonTool.Path.NoWorkYet label

        [ line language statusPath label; work ]

    let render (agents: obj array) (ptys: obj array) : string =
        let language = ProviderLanguage.English
        let agentLines = agents |> Array.toList |> List.collect (agentLines language)

        let ptyLines =
            ptys
            |> Array.toList
            |> List.sortBy (fun value -> text (value?ptyId))
            |> List.map (fun value ->
                let label = text (value?command)
                line language HorizonTool.Path.RemainsOpen label)

        let lines = List.append agentLines ptyLines

        if List.isEmpty lines then
            ToolHostCodec.tomlObjectWithInstructions
                (ProviderProse.instructionLines language HorizonTool.Path.EmptyRoster Map.empty)
                []
        else
            ToolHostCodec.tomlObjectWithInstructions lines []

    let unavailable () : string =
        ToolHostCodec.tomlObjectWithInstructions
            [ ProviderProse.render ProviderLanguage.English HorizonTool.Path.UnavailableFromContext Map.empty ]
            []

    let cannotBeSeen () : string =
        ToolHostCodec.tomlObjectWithInstructions
            [ ProviderProse.render ProviderLanguage.English HorizonTool.Path.CannotBeSeen Map.empty ]
            []

    let description () : string =
        ProviderProse.render ProviderLanguage.English HorizonTool.Path.Description Map.empty
