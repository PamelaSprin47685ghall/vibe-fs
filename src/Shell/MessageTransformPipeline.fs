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
open Wanxiangshu.Shell.MessageTransformStack
open Wanxiangshu.Shell.RuntimeScope

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

// ── Synthetic-state helpers ─────────────────────────────────────────────────

let private applyBacklogSlot
    (state: TransformState)
    (eventCount: int)
    (segmentLength: int)
    (encoded: obj array)
    : obj array * TransformState =
    if state.Backlog.EventCount = eventCount && state.Backlog.Segment.Length > 0 then
        let segmentLength = min state.Backlog.Segment.Length encoded.Length
        Array.blit state.Backlog.Segment 0 encoded 0 segmentLength
        encoded, state
    else
        // Only the leading projection messages belong to the hook. Native host
        // messages must remain the freshly decoded objects from this turn.
        let length = min segmentLength encoded.Length
        let segment = if length = 0 then [||] else encoded.[.. length - 1]

        encoded,
        { state with
            Backlog =
                { EventCount = eventCount
                  Segment = segment } }

let private applyTopSlot (state: TransformState) (kind: TopSlotKind) (encoded: obj array) : obj array * TransformState =
    match kind, state.Top with
    | NoTop, _ ->
        encoded,
        { state with
            Top = { Kind = NoTop; Item = None } }
    | _, { Kind = cachedKind; Item = Some item } when cachedKind = kind ->
        encoded.[encoded.Length - 1] <- item
        encoded, state
    | _ ->
        encoded,
        { state with
            Top =
                { Kind = kind
                  Item = Some encoded.[encoded.Length - 1] } }

let private prependCapsWithState
    (scope: RuntimeScope)
    (sessionID: string)
    (plan: MessageTransformPlan)
    (encoded: obj array)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        // Some hosts omit a session ID. Do not let independent conversations
        // share a CAPS prefix in that case.
        let capsSessionID =
            if sessionID <> "" then
                sessionID
            else
                plan.Cleaned
                |> List.tryFind (fun message -> message.source = Native && message.info.role = User)
                |> Option.map (fun message -> "anonymous-" + message.info.id)
                |> Option.defaultValue "anonymous-empty"

        let state = get scope capsSessionID

        match state.Caps with
        | Some prefix -> return Array.append prefix encoded
        | None ->
            let! capsFiles = loadCaps ()
            let result = buildCaps encoded capsFiles None
            let prefixLen = result.Length - encoded.Length

            if prefixLen > 0 then
                let prefix = result.[.. prefixLen - 1]
                set scope capsSessionID { state with Caps = Some prefix }

            return result
    }

// ── Main pipeline ───────────────────────────────────────────────────────────

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

            // Event-log-backed hosts return the durable backlog directly; its
            // count is the compatibility key for hosts without an event store.
            let eventCount = (backlogOps.GetOrRebuildBacklog plan.SessionID plan.Cleaned).Length
            let state = get plan.Scope plan.SessionID

            let backlogSegmentLength =
                afterBacklog
                |> List.takeWhile (fun message -> message.source <> Native)
                |> List.length

            let encodedBacklogSlot, stateAfterBacklog =
                applyBacklogSlot state eventCount backlogSegmentLength encodedBacklog

            set plan.Scope plan.SessionID stateAfterBacklog

            let! afterBudget = applyContextBudget plan backlogOps afterBacklog encodedBacklogSlot encodeMessages

            let afterPrompt =
                match plan.ParallelHintPolicy with
                | Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Exclude -> afterBudget
                | Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include ->
                    tryInjectParallelToolPrompt plan.SessionID afterBudget

            let encoded =
                if afterPrompt.Length = afterBacklog.Length then
                    encodedBacklogSlot
                else
                    encodeMessages afterPrompt

            let encodedAfterBacklogSlot, stateAfterReencode =
                if afterPrompt.Length <> afterBacklog.Length then
                    applyBacklogSlot (get plan.Scope plan.SessionID) eventCount backlogSegmentLength encoded
                else
                    encoded, get plan.Scope plan.SessionID

            let topKind =
                if afterBudget.Length > afterBacklog.Length then
                    BudgetNudgeTop
                elif afterPrompt.Length > afterBacklog.Length then
                    ParallelHintTop
                else
                    NoTop

            let encodedWithTopSlot, stateAfterTop =
                applyTopSlot stateAfterReencode topKind encodedAfterBacklogSlot

            set plan.Scope plan.SessionID stateAfterTop

            let! injected = injectFn plan.BacklogProjectionPolicy encodedWithTopSlot

            return! prependCapsWithState plan.Scope plan.SessionID plan injected loadCaps buildCaps
    }
