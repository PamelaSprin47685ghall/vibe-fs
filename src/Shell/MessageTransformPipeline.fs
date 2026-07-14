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

// ── Stack-slot helpers ──────────────────────────────────────────────────────

/// Extract the message ID from an encoded host object. Works across
/// opencode (info.id), mux (id), and omp (info.id) layouts.
let private tryEncodedMessageId (o: obj) : string =
    if isNull o then
        ""
    else
        let infoObj = get o "info"

        if not (isNullish infoObj) then
            let id = Dyn.str infoObj "id"
            if id <> "" then id else ""
        else
            Dyn.str o "id"

/// Determine whether an encoded object is a trailing synthetic (nudge or
/// parallel-tool hint) by inspecting its message ID.
let private isTrailingSyntheticId (id: string) : bool =
    id.StartsWith("context-budget-nudge-")
    || id.StartsWith("parallel-tool-synth-")
    || id.StartsWith("parallel-tool-hint:")

/// Replace the backlog-prefix encoded segment with the cached version when
/// the folded-backlog count is unchanged, preserving JS object references.
let private applyBacklogSlot
    (scope: RuntimeScope)
    (sessionID: string)
    (backlogCount: int)
    (encoded: obj array)
    : obj array =
    if backlogCount <= 1 then
        // No fold range → no synthetic prefix to cache
        encoded
    else
        // synthetic-prefix count = max(0, backlogCount - 1); the element at
        // that index is the modified anchor. So the cacheable segment is
        // encoded.[0 .. prefixCount] (prefixCount + 1 elements).
        let prefixCount = backlogCount - 1
        let segmentLen = prefixCount + 1

        if encoded.Length < segmentLen then
            encoded
        else
            match getBacklogSlot scope sessionID with
            | Some slot when slot.FoldedCount = backlogCount && slot.EncodedSegment.Length = segmentLen ->
                // Count unchanged → splice cached segment
                Array.blit slot.EncodedSegment 0 encoded 0 segmentLen
                encoded
            | _ ->
                // Count changed (or first time) → cache new segment
                let segment = encoded.[.. segmentLen - 1]

                setBacklogSlot
                    scope
                    sessionID
                    { FoldedCount = backlogCount
                      EncodedSegment = segment }

                encoded

/// Replace the trailing synthetic (nudge or parallel-hint) at the end of the
/// encoded array with its cached version when the message ID matches, so the
/// same JS object reference is reused across turns in the same episode.
let private applyTopSlot (scope: RuntimeScope) (sessionID: string) (encoded: obj array) (baseLength: int) : obj array =
    if encoded.Length = 0 || encoded.Length <= baseLength then
        clearTopSlot scope sessionID
        encoded
    else
        let lastIdx = encoded.Length - 1
        let lastObj = encoded.[lastIdx]
        let lastId = tryEncodedMessageId lastObj

        if not (isTrailingSyntheticId lastId) then
            clearTopSlot scope sessionID
            encoded
        else
            match getTopSlot scope sessionID with
            | Some slot ->
                match slot.Item with
                | Some cached when tryEncodedMessageId cached = lastId ->
                    encoded.[lastIdx] <- cached
                    encoded
                | _ ->
                    setTopSlot scope sessionID { Item = Some lastObj }
                    encoded
            | None ->
                setTopSlot scope sessionID { Item = Some lastObj }
                encoded

/// Determine the conversation key from the first native user message ID.
/// Used to invalidate CapsSlot when the conversation changes under the
/// same (possibly empty) sessionID.
let private firstNativeUserMsgId (messages: Message<obj> list) : string =
    messages
    |> List.tryPick (fun m ->
        if m.source = Native && m.info.role = User then
            let id = m.info.id

            if id <> "" && not (id.StartsWith("caps-")) && not (id.StartsWith("backlog-")) then
                Some id
            else
                None
        else
            None)
    |> Option.defaultValue ""

/// Prepend caps using CapsSlot: build once, reuse the same encoded prefix
/// forever (until process restart clears the in-memory slot).
let private prependCapsWithSlot
    (scope: RuntimeScope)
    (sessionID: string)
    (plan: MessageTransformPlan)
    (encoded: obj array)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        let convKey = firstNativeUserMsgId plan.Cleaned

        match getCapsSlot scope sessionID with
        | Some slot when slot.Prefix.Length > 0 && slot.ConvKey = convKey -> return Array.append slot.Prefix encoded
        | _ ->
            let! capsFiles = loadCaps ()
            let result = buildCaps encoded capsFiles None
            let prefixLen = result.Length - encoded.Length

            if prefixLen > 0 then
                let prefix = result.[.. prefixLen - 1]
                setCapsSlot scope sessionID { Prefix = prefix; ConvKey = convKey }

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

            // Determine the backlog entry count for BacklogSlot keying
            let backlogCount =
                if
                    plan.BacklogProjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include
                then
                    (backlogOps.GetOrRebuildBacklog plan.SessionID plan.Cleaned).Length
                else
                    0

            // Apply BacklogSlot to the freshly encoded array
            let encodedBacklogSlot =
                applyBacklogSlot plan.Scope plan.SessionID backlogCount encodedBacklog

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

            // Apply BacklogSlot again when re-encoded (length changed due to nudge/hint)
            let encodedAfterBacklogSlot2 =
                if afterPrompt.Length <> afterBacklog.Length then
                    applyBacklogSlot plan.Scope plan.SessionID backlogCount encoded
                else
                    encoded

            // Apply TopSlot: reuse trailing synthetic references
            let trailingCount = afterPrompt.Length - afterBacklog.Length

            let encodedWithTopSlot =
                applyTopSlot plan.Scope plan.SessionID encodedAfterBacklogSlot2 afterBacklog.Length

            let! injected = injectFn plan.BacklogProjectionPolicy encodedWithTopSlot

            return! prependCapsWithSlot plan.Scope plan.SessionID plan injected loadCaps buildCaps
    }
