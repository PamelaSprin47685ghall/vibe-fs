module VibeFs.Mux.ReadDedup

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Shell
open VibeFs.Shell.TreeSitterShell
open VibeFs.Kernel
open VibeFs.Kernel.Dedup
open VibeFs.Kernel.ToolOutputInfo
open VibeFs.Kernel.MessageDedup
open VibeFs.Shell.Dyn

let private readToolNames = Set.ofList [ "read"; "file_read" ]

type private ReadHit = { msgIndex: int; partIndex: int; payload: ReadPayload }

let private tryReadContent (output: obj) : string option =
    if isNullish output then None
    elif typeIs output "string" then
        let s = string output
        if s = "" then None else Some s
    else
        let s = str output "content"
        if s = "" then None else Some s

let private tryPath (input: obj) : string =
    if isNullish input then ""
    else
        match extractFilePaths input with
        | path :: _ -> path
        | [] -> ""

let private decodeMuxReadPart (part: obj) : ReadPayload option =
    if Dyn.str part "type" <> "dynamic-tool" then None
    elif not (Set.contains (Dyn.str part "toolName") readToolNames) then None
    elif Dyn.str part "state" <> "output-available" then None
    else
        match tryReadContent (Dyn.get part "output") with
        | None -> None
        | Some content ->
            let path = tryPath (Dyn.get part "input")
            Some { path = path; content = content }

let private collectMuxReadHits (messages: obj array) : ReadHit list =
    messages
    |> Array.mapi (fun i msg ->
        if isNullish msg then [||]
        else
            let parts = Dyn.get msg "parts"
            if isNullish parts || not (isArray parts) then [||]
            else
                (parts :?> obj array)
                |> Array.mapi (fun j part ->
                    decodeMuxReadPart part
                    |> Option.map (fun payload -> { msgIndex = i; partIndex = j; payload = payload }))
                |> Array.choose id)
    |> Array.concat
    |> List.ofArray

let private applyDedupToMessages (messages: obj array) (hits: ReadHit list) (replaced: bool list) : obj array =
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
                let parts = (Dyn.get msg "parts") :?> obj array
                let newParts =
                    parts
                    |> Array.mapi (fun j part ->
                        match List.tryFind (fun hit -> hit.partIndex = j) hitsInMsg with
                        | None -> part
                        | Some _ ->
                            let originalOutput = Dyn.get part "output"
                            let nextOutput =
                                if isNullish originalOutput || typeIs originalOutput "string" then
                                    box (noChangeEnvelope ())
                                else
                                    box (Dyn.withKey originalOutput "content" (box (noChangeEnvelope ())))
                            Dyn.withKey part "output" nextOutput)
                Dyn.withKey msg "parts" (box newParts))

let collectReadOutputs (messages: obj array) : string[] =
    collectMuxReadHits messages
    |> List.map (fun hit -> hit.payload.content)
    |> Array.ofList

let collectReadOutputsByPath (messages: obj array) : Map<string, string list> =
    collectMuxReadHits messages
    |> List.map (fun hit -> hit.payload)
    |> MessageDedup.collectReadOutputsByPath

let deduplicateReadOutputsWithSeenByPath
    (seenByPath: Map<string, string list>)
    (messages: obj array)
    : obj[] =
    let hits = collectMuxReadHits messages
    let payloads = hits |> List.map (fun hit -> hit.payload)
    let _, (_, replaced) = foldDedup seenByPath payloads
    applyDedupToMessages messages hits replaced

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj[] =
    let seenByPath =
        if Array.isEmpty seenOutputs then Map.empty
        else Map.add "" (Array.toList seenOutputs) Map.empty
    deduplicateReadOutputsWithSeenByPath seenByPath messages

let private decodeModelReadPart (part: obj) : ReadPayload option =
    if Dyn.str part "type" <> "tool-result" then None
    elif not (Set.contains (Dyn.str part "toolName") readToolNames) then None
    else
        let output = Dyn.get part "output"
        if isNullish output then None
        else
            let outputType = Dyn.str output "type"
            let outputValue = Dyn.get output "value"
            if isNullish outputValue then None
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
                    let path = tryPath (Dyn.get part "input")
                    Some { path = path; content = content }

let private collectModelReadHits (messages: obj array) : ReadHit list =
    messages
    |> Array.mapi (fun i msg ->
        if isNullish msg then [||]
        else
            let content = Dyn.get msg "content"
            if isNullish content || not (isArray content) then [||]
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
                let content = (Dyn.get msg "content") :?> obj array
                let newContent =
                    content
                    |> Array.mapi (fun j part ->
                        match List.tryFind (fun hit -> hit.partIndex = j) hitsInMsg with
                        | None -> part
                        | Some _ ->
                            let newOutput = createObj [ "type", box "text"; "value", box (noChangeEnvelope ()) ]
                            Dyn.withKey part "output" (box newOutput))
                Dyn.withKey msg "content" (box newContent))

let deduplicateModelReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : string[] * obj[] =
    let hits = collectModelReadHits messages
    let payloads = hits |> List.map (fun hit -> hit.payload)
    let seenByPath =
        if Array.isEmpty seenOutputs then Map.empty
        else Map.add "" (Array.toList seenOutputs) Map.empty
    let _, (newOutputs, replaced) = foldDedup seenByPath payloads
    Array.ofList newOutputs, applyModelDedupToMessages messages hits replaced
