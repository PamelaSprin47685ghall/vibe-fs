module Wanxiangshu.Shell.MessageTransformPipeline

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.MessageTransformCore

type MessageTransformPlan = Wanxiangshu.Shell.MessageTransformCore.MessageTransformPlan

let private isObject (o: obj) : bool =
    if isNull o then false else jsTypeof o = "object"

let private tryGetString (o: obj) (prop: string) : string option =
    if not (isObject o) then
        None
    else
        let v = o?(prop)

        match jsTypeof v with
        | "string" -> let s = string v in if s <> "" then Some s else None
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

let private getCallIDs (msg: Message<'raw>) : string list =
    let partsCallIDs =
        msg.parts
        |> List.choose (fun part ->
            match part with
            | ToolPart(_, callID, _, _) -> Some callID
            | _ -> None)

    let rawCallIDs = getCallIDsFromRaw msg
    (partsCallIDs @ rawCallIDs) |> Seq.distinct |> Seq.toList

let tryInjectParallelToolPrompt (sessionID: string) (messages: Message<obj> list) : Message<obj> list =
    let isRealCallId (callID: string) : bool =
        not (System.String.IsNullOrWhiteSpace callID)
        && not (Wanxiangshu.Kernel.HostTools.isSynthCallId callID)
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

    let nativeMsgs = messages |> List.filter (fun m -> m.source = Native)

    let lastAssistantIdxOpt =
        nativeMsgs |> List.tryFindIndexBack (fun m -> m.info.role = Assistant)

    match lastAssistantIdxOpt with
    | None -> messages
    | Some lastIdx ->
        let lastAssistantMsg = nativeMsgs.[lastIdx]
        let realCallIDs = getRealCallIds lastAssistantMsg

        if realCallIDs.Length <> 1 then
            messages
        else
            let targetCallID = List.head realCallIDs

            let laterMessages = nativeMsgs.[lastIdx + 1 ..]

            let isTerminalInAssistant =
                lastAssistantMsg.parts
                |> List.exists (fun p ->
                    match p with
                    | ToolPart(_, cid, Some state, _) when cid = targetCallID ->
                        match state.status with
                        | ToolExecutionStatus.Completed
                        | ToolExecutionStatus.Error -> true
                        | _ -> false
                    | _ -> false)

            let isCompleted, messagesAfterCompletion =
                if isTerminalInAssistant then
                    (true, laterMessages)
                else
                    let completionIdxOpt =
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

                    match completionIdxOpt with
                    | None -> (false, [])
                    | Some completionIdx -> (true, laterMessages.[completionIdx + 1 ..])

            if not isCompleted then
                messages
            else
                let hasLaterAssistant =
                    messagesAfterCompletion |> List.exists (fun m -> m.info.role = Assistant)

                if hasLaterAssistant then
                    messages
                else
                    let hintId = "parallel-tool-synth-" + targetCallID

                    let alreadyHasHint =
                        messages
                        |> List.exists (fun m ->
                            m.info.id = hintId
                            || (match m.source with
                                | Synthetic s ->
                                    s.StartsWith("parallel-tool-synth-") || s.StartsWith("parallel-tool-hint:")
                                | _ -> false))

                    if alreadyHasHint then
                        messages
                    else
                        let synthMsg: Message<obj> =
                            { info =
                                { id = hintId
                                  sessionID = sessionID
                                  role = User
                                  agent = "orchestrator"
                                  isError = false
                                  toolName = ""
                                  details = null
                                  time = null }
                              parts = [ TextPart Wanxiangshu.Kernel.PromptFragments.parallelToolPromptProse ]
                              source = Synthetic "parallel-tool-synth-"
                              raw = null }

                        List.append messages [ synthMsg ]

let runMessageTransformPipeline
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (encodeMessages: Message<obj> list -> obj array)
    (injectFn: Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy -> obj array -> JS.Promise<obj array>)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        if plan.Cleaned.IsEmpty then
            return [||]
        else
            let afterAmend = plan.Cleaned

            let afterBacklog =
                applyBacklogProjection plan.SessionID plan.BacklogProjectionPolicy backlogOps afterAmend

            let encodedBacklog = encodeMessages afterBacklog

            let! afterBudget = applyContextBudget plan backlogOps afterBacklog encodedBacklog encodeMessages

            let afterPrompt =
                match plan.ParallelHintPolicy with
                | Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Exclude -> afterBudget
                | Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include ->
                    tryInjectParallelToolPrompt plan.SessionID afterBudget

            let encoded =
                if afterPrompt.Length = afterBacklog.Length then
                    encodedBacklog
                else
                    encodeMessages afterPrompt

            let! injected = injectFn plan.BacklogProjectionPolicy encoded
            let! capsFiles = loadCaps ()
            return buildCaps injected capsFiles None
    }
