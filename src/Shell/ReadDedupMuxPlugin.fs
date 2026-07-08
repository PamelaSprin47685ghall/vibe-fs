module Wanxiangshu.Shell.ReadDedupMuxPlugin

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Dedup
open Wanxiangshu.Kernel.MessageDedup
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.HostMessagePartCodec
open Wanxiangshu.Shell.ReadDedupCore
open Wanxiangshu.Shell.TreeSitterShell

let private readToolNames = Set.ofList [ "read"; "file_read" ]

let private tryPath (input: obj) : string =
    if Dyn.isNullish input then
        ""
    else
        match extractFilePaths input with
        | path :: _ -> path
        | [] -> ""

let private decodeMuxReadPart (part: obj) : ReadPayload option =
    if Dyn.str part "type" <> "dynamic-tool" then
        None
    elif not (Set.contains (Dyn.str part "toolName") readToolNames) then
        None
    elif Dyn.str part "state" <> "output-available" then
        None
    else
        match decodeDynamicToolReadOutput part with
        | None -> None
        | Some content ->
            if isNoChangeOutput content then
                None
            else
                let path = tryPath (Dyn.get part "input")
                Some { path = path; content = content }

let private collectMuxReadHits (messages: obj array) : ReadHit list =
    messages
    |> Array.mapi (fun i msg ->
        getMessageParts msg
        |> Array.mapi (fun j part ->
            decodeMuxReadPart part
            |> Option.map (fun payload ->
                { msgIndex = i
                  partIndex = j
                  payload = payload }))
        |> Array.choose id)
    |> Array.concat
    |> List.ofArray

let collectReadOutputs (messages: obj array) : string[] =
    collectMuxReadHits messages
    |> List.map (fun hit -> hit.payload.content)
    |> Array.ofList

let collectReadOutputsByPath (messages: obj array) : Map<string, string list> =
    collectMuxReadHits messages
    |> List.map (fun hit -> hit.payload)
    |> Wanxiangshu.Kernel.MessageDedup.collectReadOutputsByPath

let deduplicateReadOutputsWithSeenByPath (seenByPath: Map<string, string list>) (messages: obj array) : obj[] =
    let getParts msg = getMessageParts msg

    let getHit i j part =
        decodeMuxReadPart part
        |> Option.map (fun payload ->
            { msgIndex = i
              partIndex = j
              payload = payload })

    let verdicts, _ = processDedupHits seenByPath messages getParts getHit

    let replacements =
        verdicts
        |> List.choose (fun (hit, verdict) ->
            match verdict with
            | AlreadySeen -> Some hit
            | NewContent _ -> None)

    if List.isEmpty replacements then
        messages
    else
        let msgGroups = replacements |> List.groupBy (fun hit -> hit.msgIndex)

        messages
        |> Array.mapi (fun i msg ->
            match List.tryFind (fun (idx, _) -> idx = i) msgGroups with
            | None -> msg
            | Some(_, hitsInMsg) ->
                let parts = getMessageParts msg

                let newParts =
                    parts
                    |> Array.mapi (fun j part ->
                        match List.tryFind (fun hit -> hit.partIndex = j) hitsInMsg with
                        | None -> part
                        | Some _ ->
                            let originalOutput = Dyn.get part "output"

                            let nextOutput =
                                if Dyn.isNullish originalOutput || Dyn.typeIs originalOutput "string" then
                                    box (noChangeEnvelope ())
                                else
                                    box (Dyn.withKey originalOutput "content" (box (noChangeEnvelope ())))

                            Dyn.withKey part "output" nextOutput)

                Dyn.withKey msg "parts" (box newParts))

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj[] =
    let seenByPath =
        if Array.isEmpty seenOutputs then
            Map.empty
        else
            Map.add "" (Array.toList seenOutputs) Map.empty

    deduplicateReadOutputsWithSeenByPath seenByPath messages
