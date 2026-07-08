module Wanxiangshu.Mux.ReadDedup

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Dedup
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.MessageDedup
open Wanxiangshu.Shell.TreeSitterShell

let collectReadOutputs (messages: obj array) : string[] =
    Wanxiangshu.Shell.ReadDedupMuxPlugin.collectReadOutputs messages

let collectReadOutputsByPath (messages: obj array) : Map<string, string list> =
    Wanxiangshu.Shell.ReadDedupMuxPlugin.collectReadOutputsByPath messages

let deduplicateReadOutputsWithSeenByPath (seenByPath: Map<string, DedupState>) (messages: obj array) : obj[] =
    Wanxiangshu.Shell.ReadDedupMuxPlugin.deduplicateReadOutputsWithSeenByPath seenByPath messages

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj[] =
    Wanxiangshu.Shell.ReadDedupMuxPlugin.deduplicateReadOutputsWithSeen seenOutputs messages

let private readToolNames = Set.ofList [ "read"; "file_read" ]

type private ReadHit =
    { msgIndex: int
      partIndex: int
      payload: ReadPayload }

let private tryReadContent (output: obj) : string option =
    if Wanxiangshu.Shell.Dyn.isNullish output then
        None
    elif Wanxiangshu.Shell.Dyn.typeIs output "string" then
        let s = string output
        if s = "" then None else Some s
    else
        let s = Wanxiangshu.Shell.Dyn.str output "content"
        if s = "" then None else Some s

let private tryPath (input: obj) : string =
    if Wanxiangshu.Shell.Dyn.isNullish input then
        ""
    else
        match extractFilePaths input with
        | path :: _ -> path
        | [] -> ""


let private decodeModelReadPart (part: obj) : ReadPayload option =
    if Wanxiangshu.Shell.Dyn.str part "type" <> "tool-result" then
        None
    elif not (Set.contains (Wanxiangshu.Shell.Dyn.str part "toolName") readToolNames) then
        None
    else
        let output = Wanxiangshu.Shell.Dyn.get part "output"

        if Wanxiangshu.Shell.Dyn.isNullish output then
            None
        else
            let outputType = Wanxiangshu.Shell.Dyn.str output "type"
            let outputValue = Wanxiangshu.Shell.Dyn.get output "value"

            if Wanxiangshu.Shell.Dyn.isNullish outputValue then
                None
            else
                let content =
                    if outputType = "text" then
                        string outputValue
                    elif outputType = "json" then
                        match tryReadContent outputValue with
                        | Some c -> c
                        | None -> ""
                    else
                        ""

                if content = "" then
                    None
                else
                    let path = tryPath (Wanxiangshu.Shell.Dyn.get part "input")
                    Some { path = path; content = content }

let private collectModelReadHits (messages: obj array) : ReadHit list =
    messages
    |> Array.mapi (fun i msg ->
        if Wanxiangshu.Shell.Dyn.isNullish msg then
            [||]
        else
            let content = Wanxiangshu.Shell.Dyn.get msg "content"

            if
                Wanxiangshu.Shell.Dyn.isNullish content
                || not (Wanxiangshu.Shell.Dyn.isArray content)
            then
                [||]
            else
                (content :?> obj array)
                |> Array.mapi (fun j part ->
                    decodeModelReadPart part
                    |> Option.map (fun payload ->
                        { msgIndex = i
                          partIndex = j
                          payload = payload }))
                |> Array.choose id)
    |> Array.concat
    |> List.ofArray

let private applyModelDedupToMessages (messages: obj array) (hits: ReadHit list) (replaced: bool list) : obj array =
    if List.forall not replaced then
        messages
    else
        let replacements =
            List.zip hits replaced
            |> List.choose (fun (hit, wasReplaced) -> if wasReplaced then Some hit else None)

        let msgMap =
            replacements
            |> List.groupBy (fun hit -> hit.msgIndex)
            |> Map.ofList

        messages
        |> Array.mapi (fun i msg ->
            match Map.tryFind i msgMap with
            | None -> msg
            | Some hitsInMsg ->
                let partMap = hitsInMsg |> List.map (fun hit -> hit.partIndex, hit) |> Map.ofList
                let contentObj = Wanxiangshu.Shell.Dyn.get msg "content"

                if
                    Wanxiangshu.Shell.Dyn.isNullish contentObj
                    || not (Wanxiangshu.Shell.Dyn.isArray contentObj)
                then
                    msg
                else
                    let content = contentObj :?> obj array

                    let newContent =
                        content
                        |> Array.mapi (fun j part ->
                            match Map.tryFind j partMap with
                            | None -> part
                            | Some _ ->
                                let newOutput = createObj [ "type", box "text"; "value", box (noChangeEnvelope ()) ]
                                Wanxiangshu.Shell.Dyn.withKey part "output" (box newOutput))

                    Wanxiangshu.Shell.Dyn.withKey msg "content" (box newContent))

let deduplicateModelReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : string[] * obj[] =
    let hits = collectModelReadHits messages
    let payloads = hits |> List.map (fun hit -> hit.payload)

    let seenByPath =
        if Array.isEmpty seenOutputs then
            Map.empty
        else
            let state =
                { fingerprints = Set.empty
                  rawOutputs = Array.toList seenOutputs }
            Map.add "" state Map.empty

    let _, (newOutputs, replaced) = foldDedup seenByPath payloads
    Array.ofList newOutputs, applyModelDedupToMessages messages hits replaced
