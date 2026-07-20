module Wanxiangshu.Runtime.SubagentPromptBody

open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.SubagentToolContext

let noOutputText = "(no output)"
let abortedPrefix = "(aborted)"

let noOutputMessage () : string = noOutputText
let abortedPrefixMessage () : string = abortedPrefix

let textPart (text: string) : obj =
    box (createObj [ "type", box "text"; "text", box text ])

let textParts (parts: string list) : obj array =
    parts |> List.map textPart |> List.toArray

let private tryReadPromptModel (payload: obj) : obj option =
    let promptModel = Dyn.get payload "model"

    if not (Dyn.isNullish promptModel) then
        Some promptModel
    else
        let modelString = Dyn.str payload "modelString"

        if modelString = "" then
            None
        else
            let basePart =
                match modelString.IndexOf(':') with
                | -1 -> modelString
                | colon -> modelString.[0 .. colon - 1].Trim()

            if basePart = "" then
                None
            else
                let slash = basePart.IndexOf('/')

                if slash <= 0 || slash >= basePart.Length - 1 then
                    None
                else
                    Some(
                        box
                            {| providerID = basePart.[0 .. slash - 1]
                               modelID = basePart.[slash + 1 ..] |}
                    )

let buildPromptBody (agent: string) (prompt: string) (tools: obj) (settings: SubagentAiSettings) : obj =
    let body =
        box
            {| agent = agent
               parts = [| box {| ``type`` = "text"; text = prompt |} |] |}

    let body =
        if Dyn.isNullish tools then
            body
        else
            Dyn.withKey body "tools" tools

    let body =
        match settings.ModelString with
        | None -> body
        | Some modelString ->
            match tryReadPromptModel (createObj [ "modelString", box modelString ]) with
            | Some model -> Dyn.withKey body "model" model
            | None -> body

    let body =
        match settings.ThinkingLevel with
        | Some level when level.Trim() <> "" -> Dyn.withKey body "variant" (box level)
        | _ -> body

    body
