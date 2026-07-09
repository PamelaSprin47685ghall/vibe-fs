module Wanxiangshu.Kernel.AmendFilter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Messaging

// ---- Public API ----

/// True when o is a non-null JS object (not undefined, not null, not primitive).
let private isObject (o: obj) : bool =
    if isNull o then false else jsTypeof o = "object"

/// Safely read a string property from a JS object: only accepts string/number, ignores undefined/null.
let private tryGetString (o: obj) (prop: string) : string option =
    if not (isObject o) then
        None
    else
        let v = o?(prop)

        match jsTypeof v with
        | "string" -> let s = string v in if s <> "" then Some s else None
        | "number" -> Some(string v)
        | _ -> None

/// Try to read a callID string from a raw JS object via common property names.
let private tryCallIDFromRaw (raw: obj) : string option =
    match tryGetString raw "toolCallId" with
    | Some id -> Some id
    | None ->
        match tryGetString raw "callId" with
        | Some id -> Some id
        | None -> tryGetString raw "callID"

/// Extract callIDs stashed in raw JS object: top-level, info block, or parts array.
let private getCallIDsFromRaw (msg: Message<'raw>) : string list =
    let ids = System.Collections.Generic.HashSet<string>()

    let rec inspect (o: obj) =
        if isObject o then
            match tryCallIDFromRaw o with
            | Some id -> ids.Add(id) |> ignore
            | None -> ()

            let info: obj = o?info

            if isObject info then
                match tryCallIDFromRaw info with
                | Some id -> ids.Add(id) |> ignore
                | None -> ()

            let parts: obj = o?parts

            if isObject parts && JS.Constructors.Array.isArray (parts) then
                let arr: obj array = unbox parts

                for part in arr do
                    if isObject part then
                        match tryCallIDFromRaw part with
                        | Some id -> ids.Add(id) |> ignore
                        | None -> ()

    inspect (box msg.raw)
    Seq.toList ids

/// Extract all callIDs from a message: F# ToolParts first, then raw JS fallback.
let getCallIDs (msg: Message<'raw>) : string list =
    let partsCallIDs =
        msg.parts
        |> List.choose (fun part ->
            match part with
            | ToolPart(_, callID, _, _) -> Some callID
            | _ -> None)

    let rawCallIDs = getCallIDsFromRaw msg

    (partsCallIDs @ rawCallIDs) |> Seq.distinct |> Seq.toList

/// Pop the most recent tool call chain from the message list.
/// A tool call chain = preceding context (from previous ToolResult+1 or 0)
/// + assistant message with ToolPart(s) + all corresponding ToolResult(s).
/// Supports parallel multi-tool calls within a single assistant message.
/// Returns: (poppedMessages * remainingMessages).
let popOneToolCall (messages: Message<'raw> list) : (Message<'raw> list * Message<'raw> list) =
    match messages with
    | [] -> ([], [])
    | _ ->
        let lastToolCallIdx =
            messages
            |> List.indexed
            |> List.tryFindBack (fun (_, msg) -> msg.info.role = Assistant && List.exists partIsTool msg.parts)
            |> Option.map fst

        match lastToolCallIdx with
        | None -> ([], messages)
        | Some idx ->
            let callIDs = getCallIDs messages.[idx] |> Set.ofList

            let endIdx =
                if Set.isEmpty callIDs then
                    idx
                else
                    messages
                    |> List.indexed
                    |> List.filter (fun (i, m) ->
                        i > idx
                        && m.info.role = ToolResult
                        && (Set.isEmpty callIDs
                            || List.isEmpty (getCallIDs m)
                            || List.exists (fun cid -> Set.contains cid callIDs) (getCallIDs m)))
                    |> List.map fst
                    |> function
                        | [] -> idx
                        | indices -> List.max indices

            let startIdx =
                if idx = 0 then
                    0
                else
                    let prevToolResultIdx =
                        messages.[.. idx - 1]
                        |> List.indexed
                        |> List.tryFindBack (fun (_, m) -> m.info.role = ToolResult)
                        |> Option.map fst

                    match prevToolResultIdx with
                    | Some pti -> pti + 1
                    | None ->
                        let prevUserIdx =
                            messages.[.. idx - 1]
                            |> List.indexed
                            |> List.tryFindBack (fun (_, m) -> m.info.role = User)
                            |> Option.map fst

                        match prevUserIdx with
                        | Some ui -> ui
                        | None -> 0

            let removed = messages.[startIdx..endIdx]
            let before = if startIdx = 0 then [] else messages.[.. startIdx - 1]

            let after =
                if endIdx >= messages.Length - 1 then
                    []
                else
                    messages.[endIdx + 1 ..]

            (removed, before @ after)

/// Pop tool calls until `count` chains are removed or the list is exhausted.
let popUntilCallID (count: int) (messages: Message<'raw> list) : (Message<'raw> list * Message<'raw> list) =
    let rec loop n remaining removed =
        if n <= 0 || List.isEmpty remaining then
            (List.rev removed, remaining)
        else
            let (popped, rest) = popOneToolCall remaining

            if List.isEmpty popped then
                (List.rev removed, rest)
            else
                loop (n - 1) rest (List.rev popped @ removed)

    loop count messages []

/// Scan messages left-to-right, applying amend markers as they appear.
/// An amend=N marker triggers N pops from the accumulated history.
/// The amend message itself is never added to the accumulator (no self-harm).
let filterAmendMessages (extractor: 'raw -> int option) (messages: Message<'raw> list) : Message<'raw> list =
    let tryGetAmend raw = extractor raw

    let rec popN n acc =
        if n <= 0 then
            acc
        else
            match popOneToolCall acc with
            | ([], _) -> acc
            | (_, remaining) -> popN (n - 1) remaining

    let rec processMessages acc remaining =
        match remaining with
        | [] -> List.rev acc
        | msg :: rest ->
            match tryGetAmend msg.raw with
            | Some n when n > 0 ->
                let tempNormal = List.rev acc
                let poppedNormal = popN n tempNormal
                let newAcc = List.rev poppedNormal
                processMessages (msg :: newAcc) rest
            | _ -> processMessages (msg :: acc) rest

    processMessages [] messages
