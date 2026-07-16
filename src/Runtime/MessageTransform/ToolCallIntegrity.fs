module Wanxiangshu.Runtime.MessageTransform.ToolCallIntegrity

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Runtime.Dyn

let private isObject (o: obj) : bool =
    if isNull o then false else jsTypeof o = "object"

let private tryGetString (o: obj) (prop: string) : string option =
    if not (isObject o) then
        None
    else
        let v = o?(prop)

        match jsTypeof v with
        | "string" ->
            let s = string v
            if s <> "" then Some s else None
        | "number" -> Some(string v)
        | _ -> None

let private tryCallIDFromRaw (raw: obj) : string option =
    match tryGetString raw "toolCallId" with
    | Some id -> Some id
    | None ->
        match tryGetString raw "callId" with
        | Some id -> Some id
        | None -> tryGetString raw "callID"

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

let getCallIDs (msg: Message<'raw>) : string list =
    let partsCallIDs =
        msg.parts
        |> List.choose (fun part ->
            match part with
            | ToolPart(_, callID, _, _) -> Some callID
            | _ -> None)

    let rawCallIDs = getCallIDsFromRaw msg
    (partsCallIDs @ rawCallIDs) |> Seq.distinct |> Seq.toList

let isRealCallId (callID: string) : bool =
    not (System.String.IsNullOrWhiteSpace callID)
    && not (isSynthCallId callID)
    && not (callID.StartsWith "semble-")
    && not (callID.StartsWith "caps-")
    && not (callID.StartsWith "prefetch-")
    && not (callID.StartsWith "internal-")

let getRealCallIds (m: Message<obj>) : string list =
    m.parts
    |> List.choose (fun p ->
        match p with
        | ToolPart(_, callID, _, _) when isRealCallId callID -> Some callID
        | _ -> None)

let isTerminalCallInAssistant (targetCallID: string) (msg: Message<obj>) : bool =
    msg.parts
    |> List.exists (fun p ->
        match p with
        | ToolPart(_, cid, Some state, _) when cid = targetCallID ->
            match state.status with
            | ToolExecutionStatus.Completed
            | ToolExecutionStatus.Error -> true
            | _ -> false
        | _ -> false)

let findCompletionIndex (targetCallID: string) (laterMessages: Message<obj> list) : int option =
    laterMessages
    |> List.tryFindIndex (fun m ->
        let hasTerminalPart =
            m.parts
            |> List.exists (fun p ->
                match p with
                | ToolPart(_, cid, stateOpt, _) when cid = targetCallID ->
                    match stateOpt with
                    | Some state ->
                        match state.status with
                        | ToolExecutionStatus.Completed
                        | ToolExecutionStatus.Error -> true
                        | _ -> false
                    | None -> true
                | _ -> false)

        let isMatchingToolResult =
            m.info.role = ToolResult
            && (m.info.id = targetCallID
                || m.info.id = targetCallID + "-result"
                || m.info.id = targetCallID + "_result"
                || m.info.id = targetCallID + ":result"
                || (let callIDs = getCallIDs m in List.contains targetCallID callIDs))

        hasTerminalPart || isMatchingToolResult)
