module VibeFs.Mux.ReadDedup

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dedup
open VibeFs.Kernel.ToolOutputInfo
open VibeFs.Kernel.MessageDedup
open VibeFs.Shell.TreeSitterShell

let collectReadOutputs (messages: obj array) : string[] =
    VibeFs.Shell.ReadDedupMuxPlugin.collectReadOutputs messages

let collectReadOutputsByPath (messages: obj array) : Map<string, string list> =
    VibeFs.Shell.ReadDedupMuxPlugin.collectReadOutputsByPath messages

let deduplicateReadOutputsWithSeenByPath
    (seenByPath: Map<string, string list>)
    (messages: obj array)
    : obj[] =
    VibeFs.Shell.ReadDedupMuxPlugin.deduplicateReadOutputsWithSeenByPath seenByPath messages

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj[] =
    VibeFs.Shell.ReadDedupMuxPlugin.deduplicateReadOutputsWithSeen seenOutputs messages

let private readToolNames = Set.ofList [ "read"; "file_read" ]

type private ReadHit = { msgIndex: int; partIndex: int; payload: ReadPayload }

let private tryReadContent (output: obj) : string option =
    if VibeFs.Shell.Dyn.isNullish output then None
    elif VibeFs.Shell.Dyn.typeIs output "string" then
        let s = string output
        if s = "" then None else Some s
    else
        let s = VibeFs.Shell.Dyn.str output "content"
        if s = "" then None else Some s

let private tryPath (input: obj) : string =
    if VibeFs.Shell.Dyn.isNullish input then ""
    else
        match extractFilePaths input with
        | path :: _ -> path
        | [] -> ""


let private decodeModelReadPart (part: obj) : ReadPayload option =
    if VibeFs.Shell.Dyn.str part "type" <> "tool-result" then None
    elif not (Set.contains (VibeFs.Shell.Dyn.str part "toolName") readToolNames) then None
    else
        let output = VibeFs.Shell.Dyn.get part "output"
        if VibeFs.Shell.Dyn.isNullish output then None
        else
            let outputType = VibeFs.Shell.Dyn.str output "type"
            let outputValue = VibeFs.Shell.Dyn.get output "value"
            if VibeFs.Shell.Dyn.isNullish outputValue then None
            else
                let content =
                    if outputType = "text" then string outputValue
                    elif outputType = "json" then
                        match tryReadContent outputValue with
                        | Some c -> c
                        | None -> ""
                    else ""
                if content = "" then None
                else
                    let path = tryPath (VibeFs.Shell.Dyn.get part "input")
                    Some { path = path; content = content }

let private collectModelReadHits (messages: obj array) : ReadHit list =
    messages
    |> Array.mapi (fun i msg ->
        if VibeFs.Shell.Dyn.isNullish msg then [||]
        else
            let content = VibeFs.Shell.Dyn.get msg "content"
            if VibeFs.Shell.Dyn.isNullish content || not (VibeFs.Shell.Dyn.isArray content) then [||]
            else
                (content :?> obj array)
                |> Array.mapi (fun j part ->
                    decodeModelReadPart part
                    |> Option.map (fun payload -> { msgIndex = i; partIndex = j; payload = payload }))
                |> Array.choose id)
    |> Array.concat
    |> List.ofArray

let private applyModelDedupToMessages (messages: obj array) (hits: ReadHit list) (replaced: bool list) : obj array =
    if List.forall not replaced then messages
    else
        let replacements =
            List.zip hits replaced
            |> List.choose (fun (hit, wasReplaced) -> if wasReplaced then Some hit else None)
        let msgGroups = replacements |> List.groupBy (fun hit -> hit.msgIndex)
        messages
        |> Array.mapi (fun i msg ->
            match List.tryFind (fun (idx, _) -> idx = i) msgGroups with
            | None -> msg
            | Some (_, hitsInMsg) ->
                let contentObj = VibeFs.Shell.Dyn.get msg "content"
                if VibeFs.Shell.Dyn.isNullish contentObj || not (VibeFs.Shell.Dyn.isArray contentObj) then msg
                else
                    let content = contentObj :?> obj array
                    let newContent =
                        content
                        |> Array.mapi (fun j part ->
                            match List.tryFind (fun hit -> hit.partIndex = j) hitsInMsg with
                            | None -> part
                            | Some _ ->
                                let newOutput = createObj [ "type", box "text"; "value", box (noChangeEnvelope ()) ]
                                VibeFs.Shell.Dyn.withKey part "output" (box newOutput))
                    VibeFs.Shell.Dyn.withKey msg "content" (box newContent))

let deduplicateModelReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : string[] * obj[] =
    let hits = collectModelReadHits messages
    let payloads = hits |> List.map (fun hit -> hit.payload)
    let seenByPath =
        if Array.isEmpty seenOutputs then Map.empty
        else Map.add "" (Array.toList seenOutputs) Map.empty
    let _, (newOutputs, replaced) = foldDedup seenByPath payloads
    Array.ofList newOutputs, applyModelDedupToMessages messages hits replaced