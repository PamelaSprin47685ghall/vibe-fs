namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.HostSession

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Domain
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Interaction.PairGuidance
open Wanxiangshu.Context.Trace
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.Anchor
open Wanxiangshu.Participant.Provider.Projection

/// HOST-013：永久 pair-programming auto-injected pairs。
///
/// Ordinary Host 编码是 ResultGap 上的一条 completed synthetic `skill({ name: "" })` tool part。
/// OpenCode `toModelMessagesEffect` 把它展开成 provider tool-call + tool-result；Cursor 保持真实
/// terminal tool result，并在 NUL+BOM 后拼同一 canonical instruction payload。
/// 禁止 pending/running：Host 会把它们收成 "[Tool execution was interrupted]"。
/// 每个 occurrence 的 transcript 位置由 durable CallGap/ResultGap 决定；同一 placement
/// occasion（SessionId + CallGap + ResultGap）最多一个 pair，重复 transform 只 replay、
/// 不再新增。同 epoch 内前次 provider wire 必须是后次 wire 的字节前缀（ARCH-004）。
module PairProgrammingThoughtTransform =

    [<RequireQualifiedAccess>]
    type CursorPresentationOwner = | PairGuidance

    [<Literal>]
    let private PairProgrammingGuidelinePath = "host/pair-programming-guideline"

    /// Algorithm G2: the parked-site reminder rides the same PairHint channel.
    [<Literal>]
    let private ParkedHintPath = "tool/js-program/parked-hint"

    /// HOST-013 English canonical used by tests; production loads via session language.
    let text =
        ProviderProse.instructionLines ProviderLanguage.SimplifiedChinese PairProgrammingGuidelinePath Map.empty
        |> LlmFacing.renderInstructions

    // ── JS Evidence parsers（flat；无嵌套 decision）──────────────────────────

    let private tryObj (value: obj) : obj option =
        if isNull value then None else Some value

    let private tryUnboxString (value: obj) : string option =
        if isNull value then None else Some(unbox<string> value)

    let private messageInfo (rawMsg: obj) : obj option =
        if isNull rawMsg then None
        elif isNull rawMsg?info then None
        else Some(rawMsg?info)

    let private providerIdFromInfo (info: obj) : string option =
        if not (isNull info?providerID) then
            Some(unbox<string> info?providerID)
        elif not (isNull info?model) && not (isNull info?model?providerID) then
            Some(unbox<string> info?model?providerID)
        else
            None

    /// Provider id on a Host message (`info.providerID` or `info.model.providerID`).
    let providerIdOfMessage (rawMsg: obj) : string option =
        messageInfo rawMsg |> Option.bind providerIdFromInfo

    /// Most recent provider id on the transcript (assistant `providerID` or user `model.providerID`).
    let providerIdFromMessages (rawMessages: obj list) : string option =
        rawMessages |> List.rev |> List.tryPick providerIdOfMessage

    /// Unconditionally true: Cursor mode (NUL+BOM tool result suffix injection without synthetic tool messages)
    /// is unified across all providers.
    let isCursorProvider (providerId: string option) =
        ignore providerId
        true

    /// HOST-013 borrows the Host-owned skill wire. Empty name is injection-only;
    /// real non-empty skill names remain ordinary executable Host skills.
    let toolName = "skill"

    let private isMarkerSource (markerSource: string) =
        markerSource = PairGuidanceOccurrences.source

    /// HOST-013：marker 身份仅按 `info.source`。
    let isPairProgrammingThought (rawMsg: obj) : bool =
        messageInfo rawMsg
        |> Option.map (fun info -> unbox<string> info?source)
        |> Option.exists isMarkerSource

    /// Active empty-name skill loads are reserved for synthetic injection only.
    let reprimandText (lang: ProviderLanguage option) : string =
        match lang with
        | Some ProviderLanguage.SimplifiedChinese ->
            "DENIED. `skill` 只保留给系统注入的 pair-programming hint，不能主动调用或读取。真实 `skill` 工具仍可正常使用；请只加载 available skills 列表中的非空 name。"
        | _ ->
            "DENIED. `skill` is reserved for the injected pair-programming hint and cannot be called or read manually. The real `skill` tool remains available; load only non-empty names from the available skills list."

    let private isSkillToolPart (part: obj) : bool =
        not (isNull part) && (tryUnboxString part?tool |> Option.exists ((=) toolName))

    let private isReservedSkillToolPart (part: obj) : bool =
        isSkillToolPart part
        && not (isNull part?state)
        && (isNull part?state?input
            || isNull part?state?input?name
            || (tryUnboxString part?state?input?name |> Option.exists String.IsNullOrWhiteSpace))

    let private writeCompletedReprimandState (reprimand: string) (part: obj) (originalState: obj) =
        if isNull originalState then
            let s =
                createObj
                    [ "status", box "completed"
                      "input", box (createObj [])
                      "output", box reprimand
                      "time", box (createObj [ "start", box 0; "end", box 0 ]) ]

            part?state <- s
        else
            originalState?status <- box "completed"
            originalState?output <- box reprimand
            emitJsExpr originalState "delete $0.error; delete $0.errorText" |> ignore

    let private clearPartError (part: obj) =
        if not (isNull part?error) then
            emitJsExpr part "delete $0.error; delete $0.errorText" |> ignore

    let private overwritePartOutput (reprimand: string) (part: obj) =
        if not (isNull part?output) then
            part?output <- box reprimand

    let private applyReservedSkillReprimand (reprimand: string) (part: obj) : obj =
        writeCompletedReprimandState reprimand part (part?state)
        clearPartError part
        overwritePartOutput reprimand part
        part

    let private reprimandToolPart (lang: ProviderLanguage option) (part: obj) : obj =
        if not (isReservedSkillToolPart part) then
            part
        else
            applyReservedSkillReprimand (reprimandText lang) part

    let private isJsArray (value: obj) : bool =
        not (isNull value) && emitJsExpr value "Array.isArray($0)"

    let private asJsArray (value: obj) : obj array option =
        if isNull value then None
        elif isJsArray value then Some(unbox<obj array> value)
        else None

    let private rawParts (rawMsg: obj) : obj array =
        if isNull rawMsg then
            [||]
        else
            asJsArray rawMsg?parts
            |> Option.orElseWith (fun () -> asJsArray rawMsg?content)
            |> Option.defaultValue [||]

    let private reprimandReservedSkillParts (lang: ProviderLanguage option) (parts: obj array) =
        parts
        |> Array.filter isReservedSkillToolPart
        |> Array.iter (fun part -> reprimandToolPart lang part |> ignore)

    let private sanitizeNonMarkerMessage (lang: ProviderLanguage option) (rawMsg: obj) : obj =
        let parts = rawParts rawMsg

        if Array.isEmpty parts then
            rawMsg
        else
            reprimandReservedSkillParts lang parts
            rawMsg

    let private sanitizeActiveReservedSkillMessage (lang: ProviderLanguage option) (rawMsg: obj) : obj =
        if isNull rawMsg || isPairProgrammingThought rawMsg then
            rawMsg
        else
            sanitizeNonMarkerMessage lang rawMsg

    /// Transform only active `skill({ name: "" })` calls from failed into completed DENIED results.
    /// Every non-empty real skill call passes through untouched.
    let sanitizeActiveToolCalls (lang: ProviderLanguage option) (rawMessages: obj list) : obj list =
        rawMessages |> List.map (sanitizeActiveReservedSkillMessage lang)

    let private buildPairMessage (callId: string) (markerText: string) : obj =
        let part =
            createObj
                [ "type", box "tool"
                  "tool", box toolName
                  "callID", box callId
                  "state",
                  box (
                      createObj
                          [ "status", box "completed"
                            "input", box (createObj [])
                            "output", box markerText
                            "time", box (createObj [ "start", box 0; "end", box 0 ]) ]
                  ) ]

        createObj
            [ "info",
              box (
                  createObj
                      [ "id", box callId
                        "role", box "assistant"
                        "source", box PairGuidanceOccurrences.source
                        "synthetic", box true ]
              )
              "parts", box [| part |] ]

    let private roleFrom (target: obj) : string option = tryUnboxString target?role

    let private messageRole (rawMsg: obj) : string =
        if isNull rawMsg then
            ""
        else
            messageInfo rawMsg
            |> Option.bind roleFrom
            |> Option.orElseWith (fun () -> roleFrom rawMsg)
            |> Option.defaultValue ""
            |> fun value -> value.ToLowerInvariant()

    let private partTypeKind (part: obj) : string option =
        tryUnboxString part?``type`` |> Option.map (fun t -> t.ToLowerInvariant())

    let private isToolPartKind (kind: string) =
        kind = "tool"
        || kind = "tool-call"
        || kind = "tool_call"
        || kind = "tool-result"
        || kind = "tool_result"

    let private isToolPart (part: obj) : bool =
        if isNull part then
            false
        else
            partTypeKind part |> Option.exists isToolPartKind

    let private statusFromState (state: obj) : string option =
        tryUnboxString state?status
        |> Option.map (fun status -> status.ToLowerInvariant())

    let private partStatus (part: obj) : string option =
        if isNull part then
            None
        else
            tryObj part?state |> Option.bind statusFromState

    // ── transcript addressing ────────────────────────────────────────────────

    let private callIdFromRawId (raw: string) : string =
        if raw.EndsWith("-call", StringComparison.Ordinal) then
            raw.Substring(0, raw.Length - 5)
        else
            raw

    let private syntheticCallIdFromInfo (info: obj) : string option =
        tryUnboxString info?pairCallID
        |> Option.orElseWith (fun () -> tryUnboxString info?id |> Option.map callIdFromRawId)

    /// The callId a stripped synthetic message belongs to: the call half's id
    /// is `callId + "-call"`, the result half's id is `callId`.
    let private syntheticCallIdOf (rawMsg: obj) : string option =
        messageInfo rawMsg |> Option.bind syntheticCallIdFromInfo

    /// Real messages in transcript order, each carrying its Host message
    /// address (`info.id` / `id`). Every real message must have one — a message
    /// without an address could never anchor a synthetic half.
    ///
    /// The same address MAY appear more than once: context-compression-031
    /// `carry` can repeat historical rows. Keep every row. Render Before at
    /// the first copy and After at the last copy. Cursor uses the last carrier.
    let private addressedRealMessages (realMessages: obj list) : Result<(string * obj) list, string> =
        let rec loop acc =
            function
            | [] -> Ok(List.rev acc)
            | message :: rest ->
                match ProviderWireDecode.hostMessageId message with
                | None -> Error "transcript message without address (HOST-013)"
                | Some id -> loop ((id, message) :: acc) rest

        loop [] realMessages

    // ── replay：唯一合法渲染路径 ─────────────────────────────────────────────

    /// The one and only HOST-013 renderer: real messages + durable anchored
    /// pairs → the exact provider wire. Historical completed rows sit at their
    /// own durable ResultGap; nothing re-decides their position (`historyBlock` 禁止).
    ///
    /// 组内排序唯一合法：`Ordinal` 升序。
    ///
    /// Anchor 不在当前真实消息里的 historical pair **不重放、不报错**。
    /// Durable fact 仍保留；完整 transcript 回来时 anchor 在场即可再 replay。
    let cursorGuidanceSeparator = "\u0000\uFEFF"

    let private systemBlockPrefix = "<system>"
    let private systemBlockSuffix = "</system>"

    let systemBlock (body: string) : string =
        let content = if isNull body then "" else body.Trim()

        if
            content.StartsWith(systemBlockPrefix, StringComparison.Ordinal)
            && content.EndsWith(systemBlockSuffix, StringComparison.Ordinal)
        then
            content
        else
            String.concat "\n" [ systemBlockPrefix; content; systemBlockSuffix ]

    let private isString (value: obj) : bool =
        not (isNull value) && emitJsExpr value "typeof $0 === 'string'"

    let private terminalGuidanceIndex (index: int) (part: obj) : int option =
        match isToolPart part, partStatus part, tryObj part?state with
        | false, _, _ -> None
        | true, Some "completed", Some state when isString state?output -> Some index
        | true, Some "error", Some state when isString state?error -> Some index
        | true, Some "error", Some state when isString state?output -> Some index
        | _ -> None

    let private applyGuidanceSuffix (suffix: string) (originalPart: obj) (clonedState: obj) =
        let originalState = originalPart?state

        match partStatus originalPart with
        | Some "completed" -> clonedState?output <- box ((unbox<string> originalState?output) + suffix)
        | Some "error" when isString originalState?error ->
            clonedState?error <- box ((unbox<string> originalState?error) + suffix)
        | Some "error" -> clonedState?output <- box ((unbox<string> originalState?output) + suffix)
        | _ -> ()

    let appendCursorSuffixes (suffixTexts: string list) (rawMsg: obj) : obj option =
        let parts = rawParts rawMsg
        let indices = parts |> Array.mapi terminalGuidanceIndex |> Array.choose id

        if Array.isEmpty indices then
            None
        else
            let suffix =
                suffixTexts
                |> List.map (fun text -> cursorGuidanceSeparator + text)
                |> String.concat ""

            let clonedParts = Array.copy parts

            indices
            |> Array.iter (fun index ->
                let originalPart = parts.[index]
                let clonedState = emitJsExpr originalPart?state "Object.assign({}, $0)"
                applyGuidanceSuffix suffix originalPart clonedState
                let clonedPart = emitJsExpr originalPart "Object.assign({}, $0)"
                clonedPart?state <- clonedState
                clonedParts.[index] <- clonedPart)

            let clonedMessage = emitJsExpr rawMsg "Object.assign({}, $0)"
            clonedMessage?parts <- box clonedParts
            Some clonedMessage

    let private mapTerminalResultText (mapText: string -> string) (originalPart: obj) : obj =
        let originalState = originalPart?state
        let clonedState = emitJsExpr originalState "Object.assign({}, $0)"

        match partStatus originalPart with
        | Some "completed" when isString originalState?output ->
            clonedState?output <- box (mapText (unbox<string> originalState?output))
        | Some "error" when isString originalState?error ->
            clonedState?error <- box (mapText (unbox<string> originalState?error))
        | Some "error" when isString originalState?output ->
            clonedState?output <- box (mapText (unbox<string> originalState?output))
        | _ -> ()

        let clonedPart = emitJsExpr originalPart "Object.assign({}, $0)"
        clonedPart?state <- clonedState
        clonedPart

    let private stripKnownCursorSuffixes (suffixTexts: string list) (originalPart: obj) : obj =
        let stripKnown (value: string) =
            (value, suffixTexts)
            ||> List.fold (fun current text ->
                current
                    .Replace(cursorGuidanceSeparator + text, "", StringComparison.Ordinal)
                    .Replace(cursorGuidanceSeparator + systemBlock text, "", StringComparison.Ordinal))

        mapTerminalResultText stripKnown originalPart

    let private terminalTextPartIndex (parts: obj array) : int option =
        parts
        |> Array.mapi (fun index part ->
            if isNull part then
                None
            elif
                isString part?text
                && (isNull part?``type`` || unbox<string> part?``type`` = "text")
            then
                Some index
            elif isString part then
                Some index
            else
                None)
        |> Array.choose id
        |> Array.tryLast

    let private appendFallbackUserText (suffix: string) (rawMsg: obj) (parts: obj array) : obj option =
        if isString rawMsg?content then
            let clonedMessage = emitJsExpr rawMsg "Object.assign({}, $0)"
            clonedMessage?content <- box ((unbox<string> rawMsg?content) + suffix)
            Some clonedMessage
        elif isString rawMsg?text then
            let clonedMessage = emitJsExpr rawMsg "Object.assign({}, $0)"
            clonedMessage?text <- box ((unbox<string> rawMsg?text) + suffix)
            Some clonedMessage
        else
            let textPart = createObj [ "type", box "text"; "text", box suffix ]
            let clonedParts = Array.append parts [| textPart |]
            let clonedMessage = emitJsExpr rawMsg "Object.assign({}, $0)"
            clonedMessage?parts <- box clonedParts
            Some clonedMessage

    let private partText (part: obj) : string =
        if isString part?text then unbox<string> part?text
        elif isString part then unbox<string> part
        else ""

    let private appendCursorGuidanceToUserMessage (markerTexts: string list) (rawMsg: obj) : obj option =
        let parts = rawParts rawMsg

        let suffix =
            markerTexts
            |> List.map (fun text -> cursorGuidanceSeparator + systemBlock text)
            |> String.concat ""

        match terminalTextPartIndex parts with
        | Some index ->
            let originalPart = parts.[index]
            let clonedPart = emitJsExpr originalPart "Object.assign({}, $0)"
            let originalText = partText originalPart

            clonedPart?text <- box (originalText + suffix)
            let clonedParts = Array.copy parts
            clonedParts.[index] <- clonedPart
            let clonedMessage = emitJsExpr rawMsg "Object.assign({}, $0)"
            clonedMessage?parts <- box clonedParts
            Some clonedMessage
        | None -> appendFallbackUserText suffix rawMsg parts

    let private stripFallbackUserText (stripKnown: string -> string) (rawMsg: obj) : obj =
        if isString rawMsg?content then
            let clonedMessage = emitJsExpr rawMsg "Object.assign({}, $0)"
            clonedMessage?content <- box (stripKnown (unbox<string> rawMsg?content))
            clonedMessage
        elif isString rawMsg?text then
            let clonedMessage = emitJsExpr rawMsg "Object.assign({}, $0)"
            clonedMessage?text <- box (stripKnown (unbox<string> rawMsg?text))
            clonedMessage
        else
            rawMsg

    let private stripUserMessageCursorSuffixes (suffixTexts: string list) (rawMsg: obj) : obj =
        let parts = rawParts rawMsg

        let stripKnown (value: string) =
            (value, suffixTexts)
            ||> List.fold (fun current text ->
                current
                    .Replace(cursorGuidanceSeparator + systemBlock text, "", StringComparison.Ordinal)
                    .Replace(cursorGuidanceSeparator + text, "", StringComparison.Ordinal))

        match terminalTextPartIndex parts with
        | Some index ->
            let originalPart = parts.[index]
            let originalText = partText originalPart
            let clonedPart = emitJsExpr originalPart "Object.assign({}, $0)"
            clonedPart?text <- box (stripKnown originalText)
            let clonedParts = Array.copy parts
            clonedParts.[index] <- clonedPart
            let clonedMessage = emitJsExpr rawMsg "Object.assign({}, $0)"
            clonedMessage?parts <- box clonedParts
            clonedMessage
        | None -> stripFallbackUserText stripKnown rawMsg

    let stripCursorSuffixes (suffixTexts: string list) (rawMsg: obj) : obj =
        let parts = rawParts rawMsg
        let indices = parts |> Array.mapi terminalGuidanceIndex |> Array.choose id

        if not (Array.isEmpty indices) then
            let clonedParts = Array.copy parts

            indices
            |> Array.iter (fun index -> clonedParts.[index] <- stripKnownCursorSuffixes suffixTexts parts.[index])

            let clonedMessage = emitJsExpr rawMsg "Object.assign({}, $0)"
            clonedMessage?parts <- box clonedParts
            clonedMessage
        elif messageRole rawMsg = "user" then
            stripUserMessageCursorSuffixes suffixTexts rawMsg
        else
            rawMsg

    type private CursorPresentation =
        { GuidanceBytes: string
          RequirementBytes: string }

    type private CursorResultIdentity =
        { MessageId: TranscriptMessageAddress
          PartId: HostToolPartId option
          CallId: ToolCallId }

    type private CapturedCursorResult =
        { Body: string
          Presentations: CursorPresentation list }

    type private CapturedCursorResults =
        { HasTrace: bool
          Results: Map<CursorResultIdentity, CapturedCursorResult> }

    let private cursorPartId (part: obj) =
        if isNull part?id then
            Some None
        elif isString part?id then
            Some(Some(HostToolPartId.create (unbox<string> part?id)))
        else
            None

    let private cursorResultIdentity (rawMsg: obj) =
        let parts = rawParts rawMsg

        parts
        |> Array.mapi terminalGuidanceIndex
        |> Array.choose id
        |> Array.toList
        |> List.filter (fun index -> isString rawMsg?info?id && isString parts.[index]?callID)
        |> List.choose (fun index ->
            cursorPartId parts.[index]
            |> Option.map (fun partId ->
                index,
                { MessageId = TranscriptMessageAddress.create (unbox<string> rawMsg?info?id)
                  PartId = partId
                  CallId = ToolCallId.create (unbox<string> parts.[index]?callID) }))

    let private cursorPresentationPrefixes (guidance: PairProgrammingGuideline list) =
        let guidanceBytes =
            guidance
            |> List.map (fun pair -> cursorGuidanceSeparator + pair.MarkerText)
            |> String.concat ""

        [ { GuidanceBytes = guidanceBytes
            RequirementBytes = "" }
          { GuidanceBytes = ""
            RequirementBytes = "" } ]

    let private presentationsAtMessage (session: SessionAgentProjection option) (rawMsg: obj) =
        let atMessage gap =
            match gap with
            | TranscriptGap.After address ->
                isString rawMsg?info?id
                && TranscriptMessageAddress.value address = unbox<string> rawMsg?info?id
            | _ -> false

        match session with
        | None -> []
        | Some state ->
            let guidance = state.Guidelines |> Option.defaultValue GuidelineProjection.empty

            let atAnchor (pairs: PairProgrammingGuideline list) =
                cursorPresentationPrefixes (pairs |> List.filter (fun pair -> atMessage pair.ResultGap))

            atAnchor (GuidelineProjection.pairs guidance)
            @ atAnchor (GuidelineProjection.visiblePairs guidance)
            |> List.distinct

    let private captureCursorResult journal trace session (rawMsg: obj) =
        let capture identity =
            taskResult {
                let! body =
                    XTraceMaterialization.tryHostToolResult
                        journal
                        identity.MessageId
                        identity.PartId
                        identity.CallId
                        trace

                return
                    body
                    |> Option.map (fun original ->
                        identity,
                        { Body = original
                          Presentations = presentationsAtMessage session rawMsg })
            }

        taskResult {
            let! captured = cursorResultIdentity rawMsg |> TaskResultList.traverseM (snd >> capture)
            return captured |> List.choose id
        }

    let private captureCursorResults journal sessionId rawMessages =
        let session =
            AgentJournal.snapshot journal
            |> fun projection -> AgentProjection.tryFind sessionId projection.AgentProjections

        let capture trace =
            taskResult {
                let! captured =
                    rawMessages
                    |> TaskResultList.traverseM (captureCursorResult journal trace session)

                return
                    { HasTrace = true
                      Results = captured |> List.concat |> Map.ofList }
            }

        session
        |> Option.bind _.XTrace
        |> Option.map capture
        |> Option.defaultWith (fun () ->
            Task.FromResult(
                Ok
                    { HasTrace = false
                      Results = Map.empty }
            ))

    let private findCapturedCursorResult captures rawMsg =
        cursorResultIdentity rawMsg
        |> List.choose (fun (index, identity) ->
            captures.Results
            |> Map.tryFind identity
            |> Option.map (fun result -> index, result))

    let private mapCapturedResult index mapText (rawMsg: obj) =
        let clonedParts = Array.copy (rawParts rawMsg)
        clonedParts.[index] <- mapTerminalResultText mapText clonedParts.[index]
        let clonedMessage = emitJsExpr rawMsg "Object.assign({}, $0)"
        clonedMessage?parts <- box clonedParts
        clonedMessage

    let private matchingPresentations captured value =
        captured.Presentations
        |> List.filter (fun presentation ->
            value = captured.Body + presentation.GuidanceBytes + presentation.RequirementBytes)

    let private stripCapturedPresentation owner captured value =
        let retain presentation =
            match owner with
            | CursorPresentationOwner.PairGuidance -> captured.Body

        match matchingPresentations captured value |> List.map retain |> List.distinct with
        | [] -> Ok value
        | [ retained ] -> Ok retained
        | _ -> Error "Cursor presentation ownership is ambiguous at the captured Host result"

    let private stripCapturedResult captures owner suffixTexts rawMsg =
        let strip message (index, captured) =
            let originalPart = (rawParts message).[index]
            let state = originalPart?state

            let value =
                if partStatus originalPart = Some "error" && isString state?error then
                    unbox<string> state?error
                else
                    unbox<string> state?output

            stripCapturedPresentation owner captured value
            |> Result.map (fun retained -> mapCapturedResult index (fun _ -> retained) message)

        if not captures.HasTrace || messageRole rawMsg = "user" then
            Ok(stripCursorSuffixes suffixTexts rawMsg)
        else
            (Ok rawMsg, findCapturedCursorResult captures rawMsg)
            ||> List.fold (fun result captured -> result |> Result.bind (fun message -> strip message captured))

    let stripCursorSuffixesWithJournal
        (journal: AgentJournal)
        (sessionId: SessionId)
        (owner: CursorPresentationOwner)
        (suffixTexts: string list)
        (rawMessages: obj list)
        : Task<Result<obj list, string>> =
        taskResult {
            let! captured = captureCursorResults journal sessionId rawMessages

            return!
                rawMessages
                |> List.traverseResultM (stripCapturedResult captured owner suffixTexts)
        }

    let private appendCursorGuidanceToTerminalToolResult (markerTexts: string list) (rawMsg: obj) : obj option =
        appendCursorSuffixes markerTexts rawMsg

    let private gapPresent (addressSet: Set<string>) (gap: TranscriptGap) =
        match gap with
        | TranscriptGap.Start -> true
        | TranscriptGap.Before address
        | TranscriptGap.After address -> Set.contains (TranscriptMessageAddress.value address) addressSet

    let private bucketAdd
        (table: Dictionary<string, ResizeArray<PairGuidanceOccurrence>>)
        (address: TranscriptMessageAddress)
        (pair: PairGuidanceOccurrence)
        =
        let key = TranscriptMessageAddress.value address

        match table.TryGetValue key with
        | true, entries -> entries.Add pair
        | false, _ ->
            // DSL-MUTABLE: algorithm-scratch — new entry list for dictionary insert
            let entries = ResizeArray<PairGuidanceOccurrence>()
            entries.Add pair
            table.[key] <- entries

    let private addAtGap
        (starts: ResizeArray<PairGuidanceOccurrence>)
        (before: Dictionary<string, ResizeArray<PairGuidanceOccurrence>>)
        (after: Dictionary<string, ResizeArray<PairGuidanceOccurrence>>)
        (pair: PairGuidanceOccurrence)
        (gap: TranscriptGap)
        =
        match gap with
        | TranscriptGap.Start -> starts.Add pair
        | TranscriptGap.Before address -> bucketAdd before address pair
        | TranscriptGap.After address -> bucketAdd after address pair

    let private placeCursorResultGap
        (cursorAfter: Dictionary<string, ResizeArray<PairGuidanceOccurrence>>)
        (pair: PairGuidanceOccurrence)
        =
        match pair.ResultGap with
        | TranscriptGap.After address -> bucketAdd cursorAfter address pair
        | _ -> ()

    let private registerPlaceablePair
        (providerId: string option)
        (starts: ResizeArray<PairGuidanceOccurrence>)
        (before: Dictionary<string, ResizeArray<PairGuidanceOccurrence>>)
        (after: Dictionary<string, ResizeArray<PairGuidanceOccurrence>>)
        (cursorAfter: Dictionary<string, ResizeArray<PairGuidanceOccurrence>>)
        (pair: PairGuidanceOccurrence)
        =
        if isCursorProvider providerId then
            placeCursorResultGap cursorAfter pair
        else
            addAtGap starts before after pair pair.ResultGap

    let private ordered (entries: ResizeArray<PairGuidanceOccurrence>) =
        entries |> Seq.sortBy (fun pair -> pair.Ordinal) |> Seq.toList

    let private emitPairs (output: ResizeArray<obj>) (entries: ResizeArray<PairGuidanceOccurrence>) =
        for pair in ordered entries do
            output.Add(buildPairMessage pair.CallId pair.MarkerText)

    let private tryBucket (table: Dictionary<string, ResizeArray<PairGuidanceOccurrence>>) (address: string) =
        match table.TryGetValue address with
        | true, entries -> Some entries
        | _ -> None

    let private emitBucketPairs
        (output: ResizeArray<obj>)
        (table: Dictionary<string, ResizeArray<PairGuidanceOccurrence>>)
        (address: string)
        =
        tryBucket table address |> Option.iter (emitPairs output)

    let private terminalResultText (part: obj) =
        let state = part?state

        if partStatus part = Some "error" && isString state?error then
            tryUnboxString state?error
        else
            tryUnboxString state?output

    let private projectCapturedGuidance captures markerTexts message =
        let insert current (index, captured) =
            let guidanceBytes =
                markerTexts
                |> List.map (fun text -> cursorGuidanceSeparator + text)
                |> String.concat ""

            let present value =
                captured.Presentations
                |> List.tryFind (fun presentation ->
                    presentation.GuidanceBytes = ""
                    && value = captured.Body + presentation.RequirementBytes)
                |> Option.map (fun presentation -> captured.Body + guidanceBytes + presentation.RequirementBytes)

            rawParts current
            |> fun parts -> terminalResultText parts.[index]
            |> Option.bind present
            |> Option.map (fun output -> mapCapturedResult index (fun _ -> output) current)
            |> Option.defaultValue current

        match findCapturedCursorResult captures message with
        | [] -> None
        | captured -> Some(List.fold insert message captured)

    let private projectGuidanceToMessage captures (markerTexts: string list) (message: obj) : obj =
        if messageRole message = "user" then
            appendCursorGuidanceToUserMessage markerTexts message
            |> Option.defaultValue message
        else
            projectCapturedGuidance captures markerTexts message
            |> Option.orElseWith (fun () -> appendCursorGuidanceToTerminalToolResult markerTexts message)
            |> Option.defaultValue message

    let private projectCursorMessage
        captures
        (cursorAfter: Dictionary<string, ResizeArray<PairGuidanceOccurrence>>)
        (address: string)
        (message: obj)
        =
        match tryBucket cursorAfter address with
        | None -> message
        | Some entries ->
            entries
            |> Seq.sortBy (fun pair -> pair.Ordinal)
            |> Seq.map (fun pair -> pair.MarkerText)
            |> Seq.toList
            |> fun markerTexts -> projectGuidanceToMessage captures markerTexts message

    let private messageCarriesGuidance (rawMsg: obj) : bool =
        if messageRole rawMsg = "user" then
            terminalTextPartIndex (rawParts rawMsg) |> Option.isSome
        else
            rawParts rawMsg
            |> Array.mapi terminalGuidanceIndex
            |> Array.exists Option.isSome

    let private replayAddressed
        captures
        (providerId: string option)
        (addressed: (string * obj) list)
        (pairs: PairGuidanceOccurrence list)
        : obj list =
        let addressSet = addressed |> List.map fst |> Set.ofList

        let firstIndex, lastIndex, carrierIndex =
            addressed
            |> List.indexed
            |> List.fold
                (fun (first, last, carriers) (index, (address, message)) ->
                    (if Map.containsKey address first then
                         first
                     else
                         Map.add address index first),
                    Map.add address index last,
                    (if messageCarriesGuidance message then
                         Map.add address index carriers
                     else
                         carriers))
                (Map.empty, Map.empty, Map.empty)

        // Both durable anchors must remain present even though ordinary and
        // Cursor only render at ResultGap. CallGap stays durable for placement
        // identity and reversible Cursor → ordinary replay.
        let placeable =
            pairs
            |> List.filter (fun pair -> gapPresent addressSet pair.CallGap && gapPresent addressSet pair.ResultGap)

        // DSL-MUTABLE: algorithm-scratch — placeable pair start accumulator
        let starts = ResizeArray<PairGuidanceOccurrence>()
        // DSL-MUTABLE: algorithm-scratch — before-gap pair bucket
        let before = Dictionary<string, ResizeArray<PairGuidanceOccurrence>>()
        // DSL-MUTABLE: algorithm-scratch — after-gap pair bucket
        let after = Dictionary<string, ResizeArray<PairGuidanceOccurrence>>()
        // DSL-MUTABLE: algorithm-scratch — cursor result-gap pair bucket
        let cursorAfter = Dictionary<string, ResizeArray<PairGuidanceOccurrence>>()

        for pair in placeable do
            registerPlaceablePair providerId starts before after cursorAfter pair

        // DSL-MUTABLE: algorithm-scratch — output pair accumulator
        let output = ResizeArray<obj>()
        emitPairs output starts

        addressed
        |> List.iteri (fun index (address, message) ->
            if Map.find address firstIndex = index then
                emitBucketPairs output before address

            let projected =
                if Map.tryFind address carrierIndex = Some index then
                    projectCursorMessage captures cursorAfter address message
                else
                    message

            output.Add projected

            if Map.find address lastIndex = index then
                emitBucketPairs output after address)

        Seq.toList output

    let private replay
        captures
        (providerId: string option)
        (realMessages: obj list)
        (pairs: PairGuidanceOccurrence list)
        : Result<obj list, string> =
        addressedRealMessages realMessages
        |> Result.map (fun addressed -> replayAddressed captures providerId addressed pairs)

    // ── 本轮新 pair 的 placement（只读当前真实消息）──────────────────────────

    // semantic-decorator-owner: guidance-delivery
    // semantic-decorator-WHAT: guidance-delivery-011
    // semantic-decorator-trace-relation: R_gap_pair(address) = (gapCtor address, gapCtor address), left then right, with a pure extensional constructor
    // semantic-decorator-proof: requirements/guidance-delivery/tests/pair-gap-constructor.test.mjs::WHAT[guidance-delivery-011] PPT_gap_constructor_receives_the_same_address_exactly_twice_in_pair_order
    // semantic-decorator-failure-policy: a constructor exception propagates immediately and prevents the second invocation
    // semantic-decorator-cancel-policy: pure synchronous gap construction introduces no cancellation boundary
    // semantic-decorator-deadline-policy: pure synchronous gap construction introduces no deadline
    // semantic-decorator-retry-bound: 2
    let internal gapsAroundAddress
        (gapCtor: TranscriptMessageAddress -> TranscriptGap)
        (message: obj)
        (errorMsg: string)
        =
        match ProviderWireDecode.hostMessageId message with
        | Some id ->
            let address = TranscriptMessageAddress.create id
            Ok(gapCtor address, gapCtor address)
        | None -> Error errorMsg

    /// 本轮新 occurrence 的 placement：**最新一条真实消息**，且只有它承载
    /// cursor 呈现时才有 placement；否则本轮不新增 occurrence。
    ///
    /// 旧实现按「有无 tool batch / 是否 trailing user」分支，产出的
    /// `Before(user)`、`After(assistant 文本)` 形状 cursor 渲染器不认，于是
    /// durable occurrence 记了却一个字节也不出现（首轮之后的纯用户轮次、重锚
    /// 后的空窗）。锚定最新消息既保证字节一定落在本次追加区（append-only），
    /// 又保证落点一定是渲染器真会改写的消息。
    ///
    /// 空 transcript 无 placement；最新消息不是载体（例如末尾是纯 assistant
    /// 文本）本轮不生成 occurrence，等下一次出现载体的请求再追加。
    let decideCurrentPlacement (realMessages: obj list) : Result<(TranscriptGap * TranscriptGap) option, string> =
        match List.rev realMessages with
        | [] -> Ok None
        | last :: _ when messageCarriesGuidance last ->
            gapsAroundAddress TranscriptGap.After last "guidance carrier without transcript address (HOST-013)"
            |> Result.map Some
        | _ -> Ok None

    let private messageRoleIsInternal (msg: obj) : bool =
        if isNull msg then
            false
        else
            let agent =
                messageInfo msg
                |> Option.bind (fun info -> tryUnboxString info?agent)
                |> Option.orElseWith (fun () -> tryUnboxString msg?agent)

            let role =
                agent
                |> Option.bind HostSessionContext.roleOf
                |> Option.orElseWith (fun () ->
                    messageInfo msg
                    |> Option.bind (fun info -> tryUnboxString info?role)
                    |> Option.orElseWith (fun () -> tryUnboxString msg?role)
                    |> Option.bind Roles.tryParseRole)

            role |> Option.exists Roles.isInternal

    // ── 入口 ─────────────────────────────────────────────────────────────────

    /// HOST-013 commit 顺序（fail closed）：
    ///
    /// 1. 读 durable history
    /// 2. 内存 strip（只有 durable 记录能解释的 synthetic 才允许删除）
    /// 3. 决定本轮候选 placement（仅当该 placement 尚不存在）
    /// 4. 内存构造候选 fact
    /// 5. 内存渲染完整 wire（replay，校验全部 anchor）
    /// 6. append durable fact —— 失败 fail closed，禁止忽略后照发
    /// 7. 返回已校验的渲染消息
    ///
    /// 同一 placement 重复进入只 replay，不 append 新 fact。
    let private tryInjectCore
        (journal: AgentJournal option)
        (sessionId: string option)
        (markerText: string)
        (rawMessages: obj list)
        : Task<Result<obj list * bool, string>> =
        taskResult {
            let ledger = PairGuidanceOccurrences.ledger journal sessionId
            let suffixes = ledger.History |> List.map (fun pair -> pair.MarkerText)

            let! captures =
                match journal, sessionId with
                | Some durable, Some sid -> captureCursorResults durable (SessionId.create sid) rawMessages
                | _ ->
                    Task.FromResult(
                        Ok
                            { HasTrace = false
                              Results = Map.empty }
                    )

            let! rawMessages =
                rawMessages
                |> List.traverseResultM (stripCapturedResult captures CursorPresentationOwner.PairGuidance suffixes)

            let lang =
                if markerText.Contains "以" || markerText.Contains "我" || markerText.Contains "结对" then
                    Some ProviderLanguage.SimplifiedChinese
                else
                    Some ProviderLanguage.English

            let realMessages =
                rawMessages
                |> List.filter (isPairProgrammingThought >> not)
                |> sanitizeActiveToolCalls lang

            let transcript =
                { Carrier = decideCurrentPlacement realMessages
                  SyntheticCallIds =
                    rawMessages
                    |> List.filter isPairProgrammingThought
                    |> List.choose syntheticCallIdOf
                  InternalAuthor = rawMessages |> List.exists messageRoleIsInternal }

            let replayMessages =
                replay captures (providerIdFromMessages realMessages) realMessages

            return!
                PairGuidanceOccurrences.occur
                    ledger
                    sessionId
                    (PairGuidanceOccurrences.isInternalSession journal sessionId)
                    markerText
                    transcript
                    replayMessages
        }

    // ── Pair guideline injection (migrated from PluginTransforms composition root) ──

    type private SessionTermination = SessionId -> string -> Task<Result<unit, string>>

    /// The four calibration numbers, when the delegator's estimate and the
    /// session start are both known; otherwise the round falls back to the
    /// elapsed-time datum alone.
    let private budgetText
        (journal: AgentJournal option)
        (projectionSessionIdOpt: string option)
        (elapsedMilliseconds: float option)
        (language: ProviderLanguage)
        : string option =
        match journal, projectionSessionIdOpt, elapsedMilliseconds with
        | Some durable, Some sessionId, Some elapsed ->
            let port = AgentJournalPortAdapter.forDelegatedToolEstimate durable

            DelegatedToolEstimateLedger.tryState port (SessionId.create sessionId)
            |> Option.map (fun state ->
                PairProgrammingCalibration.renderBudget
                    language
                    (DelegatedToolEstimateProjection.countedCallCount state)
                    (DelegatedToolEstimateProjection.remaining state)
                    elapsed)
        | _ -> None

    /// crash-reconciliation-018: the restart status guidance is one more
    /// instruction of the same marker; it is never a second physical payload.
    let private withRestartGuidance
        (restartGuidance: string option)
        (document: LlmFacing.Document)
        : LlmFacing.Document =
        match restartGuidance with
        | Some text when not (String.IsNullOrWhiteSpace text) -> LlmFacing.withInstruction text document
        | _ -> document

    let private composeMarkerDocument
        (journal: AgentJournal option)
        (projectionSessionIdOpt: string option)
        (elapsed: string option)
        (state: string option list)
        (calibration: string option)
        (guideline: string)
        (restartGuidance: string option)
        : Task<LlmFacing.Document> =
        task {
            let guidance: string option = None

            // Algorithm G2: every round that carries a PairHint also lists the
            // unresolved parked handles, so the AI is pressed to take a
            // position (unpark/abandon) without being forced to resume. The
            // parked site table and its per-session read-only query are owned
            // by lane-a-core (JsParkedSites.handlesForSession); a hint must
            // only mention handles parked by this session, never another
            // session's. A carrier-less round never reaches this point
            // (decideCurrentPlacement).
            let parkedHandles =
                match projectionSessionIdOpt with
                | Some sessionId -> JsParkedSites.handlesForSession sessionId
                | None -> []

            let resolveLanguage =
                match journal, projectionSessionIdOpt with
                | Some _, Some sessionId ->
                    Wanxiangshu.Participant.Provider.ProviderLanguageBinding.forSessionText sessionId
                | _ -> ProviderLanguage.SimplifiedChinese

            let parkedHint =
                if List.isEmpty parkedHandles then
                    []
                else
                    let language = resolveLanguage

                    [ ProviderProse.render language ParkedHintPath (Map [ "handles", String.Join(", ", parkedHandles) ]) ]

            let document =
                PairProgrammingCalibration.documentWithState guidance elapsed state calibration guideline
                |> LlmFacing.withInstructions parkedHint
                |> withRestartGuidance restartGuidance

            return document
        }

    let private terminateSessionIfPresent
        (terminateSession: SessionTermination)
        (projectionSessionIdOpt: string option)
        (reason: string)
        : Task =
        match projectionSessionIdOpt with
        | Some sessionId ->
            task {
                let! _ = terminateSession (SessionId.create sessionId) reason
                ()
            }
        | None -> Task.FromResult()

    /// Stable anchor markers are projected at their original message positions.
    let private wireTextOf (raw: obj) : string =
        ProviderWireDecode.rawPartsOf raw
        |> List.choose ProviderWireDecode.decodePart
        |> List.map (function
            | ProviderProjection.WireText text
            | ProviderProjection.WireReasoning text -> text
            | ProviderProjection.WireToolCall(_, name, args) -> name + " " + args
            | ProviderProjection.WireToolResult(_, result) -> result
            | ProviderProjection.WireMedia _ -> "")
        |> String.concat "\n"

    let private anchorMessageId (raw: obj) =
        if messageRole raw = "user" then
            ProviderWireDecode.hostMessageId raw
        else
            let parts = rawParts raw

            parts
            |> Array.mapi terminalGuidanceIndex
            |> Array.choose id
            |> Array.tryLast
            |> Option.bind (fun index ->
                ProviderWireDecode.hostMessageId raw
                |> Option.map (fun messageId ->
                    let part = parts.[index]
                    let callId = tryUnboxString part?callID |> Option.defaultValue (string index)
                    messageId + ":tool:" + callId))

    [<Emit("structuredClone($0)")>]
    let private copyAnchorSource (raw: obj) : obj = jsNative

    [<Emit("({ info: { id: $0, role: 'user', synthetic: true, source: 'carry-literal' }, parts: [{ type: 'text', text: $1 }] })")>]
    let private literalMessage (id: string) (text: string) : obj = jsNative

    [<Emit("""(function (previous, current) {
      if (previous.length === 0) return current;
      const boundary = previous[previous.length - 1];
      const identity = row => row?.info?.id ?? row?.id;
      const boundaryId = identity(boundary);
      const index = boundaryId === undefined
        ? previous.length - 1
        : current.findIndex(row => identity(row) === boundaryId);
      if (index < 0 || index >= current.length) {
        throw new Error('The compression request boundary is absent from the Host history');
      }
      const oldParts = boundary.parts ?? [];
      const newParts = current[index].parts ?? [];
      const added = [];
      for (let partIndex = 0; partIndex < Math.min(oldParts.length, newParts.length); partIndex++) {
        const before = oldParts[partIndex];
        const after = newParts[partIndex];
        if (JSON.stringify(before) === JSON.stringify(after)) continue;
        if (typeof before?.text === 'string' && typeof after?.text === 'string'
            && before.type === after.type && after.text.startsWith(before.text)) {
          const text = after.text.slice(before.text.length);
          if (text.length > 0) added.push({ ...after, text });
        } else {
          added.push(after);
        }
      }
      added.push(...newParts.slice(oldParts.length));
      const suffix = current.slice(index + 1);
      return added.length === 0 ? suffix : [{ ...current[index], parts: added }, ...suffix];
    })($0, $1)""")>]
    let private messagesAfterSnapshot (previous: obj array) (current: obj array) : obj array = jsNative

    [<Emit("""(function (raw, messageId, callId, prefixUntil, carry) {
      if ((raw.info?.id ?? raw.id) !== messageId) return raw;
      let changed = false;
      const parts = (raw.parts ?? []).map(part => {
        const state = part?.state;
        const input = state?.input;
        if (part?.type !== 'tool' || part.callID !== callId || typeof part.tool !== 'string'
            || !part.tool.startsWith('todowrite-for-') || state?.status !== 'completed'
            || input?.prefixUntil !== prefixUntil || !Array.isArray(input.carry)
            || input.carry.length !== carry.length
            || !carry.every((item, index) => Object.is(item, input.carry[index]))) return part;
        const cleanInput = { ...input };
        delete cleanInput.carry;
        changed = true;
        return { ...part, state: { ...state, input: cleanInput } };
      });
      return changed ? { ...raw, parts } : raw;
    })($0, $1, $2, $3, $4)""")>]
    let private stripCarry (raw: obj) (messageId: string) (callId: string) (prefixUntil: int) (carry: obj) : obj =
        jsNative

    /// A user message carries the anchor marker on its first text part (or a new
    /// leading text part); any other message carries it on its terminal result.
    let private markedUserAnchorParts (marker: string) (parts: obj array) : obj =
        let index =
            parts
            |> Array.tryFindIndex (fun part ->
                not (isNull part) && isString part?text && partTypeKind part = Some "text")

        match index with
        | Some index ->
            let projected = Array.copy parts
            let part = emitJsExpr parts.[index] "Object.assign({}, $0)"
            part?text <- box (marker + unbox<string> parts.[index]?text)
            projected.[index] <- part
            box projected
        | None ->
            let part = createObj [ "type", box "text"; "text", box marker ]
            box (Array.append [| part |] parts)

    let private markedTerminalAnchorParts (marker: string) (parts: obj array) : obj =
        match parts |> Array.mapi terminalGuidanceIndex |> Array.choose id |> Array.tryLast with
        | None -> markedUserAnchorParts marker parts
        | Some index ->
            let projected = Array.copy parts
            projected.[index] <- mapTerminalResultText (fun text -> marker + text) parts.[index]
            box projected

    let private markedAnchorParts (marker: string) (raw: obj) : obj =
        let parts = rawParts raw

        if messageRole raw = "user" then
            markedUserAnchorParts marker parts
        else
            markedTerminalAnchorParts marker parts

    let private markAnchors (messages: obj list) =
        messages
        |> List.mapFold
            (fun count raw ->
                if isNull raw?__contextAnchor then
                    raw, count
                else
                    let count = count + 1

                    let marker =
                        sprintf "[ANCHOR: %d]" (unbox<int> raw?__contextAnchor)
                        + cursorGuidanceSeparator

                    let cloned = emitJsExpr raw "Object.assign({}, $0)"
                    emitJsStatement cloned "delete $0.__contextAnchor"
                    cloned?parts <- markedAnchorParts marker raw
                    cloned, count)
            0
        |> fst

    let private selectAnchorView (sessionId: string) (messages: obj list) : obj list * string option =
        let input raw =
            { AnchorMessage.Id = anchorMessageId raw
              Text = wireTextOf raw
              Raw = copyAnchorSource raw }

        let inputs = messages |> List.map input

        let suffix =
            match AnchorLedger.checkpointSource sessionId with
            | None -> messages
            | Some previous ->
                messagesAfterSnapshot (previous |> List.map _.Raw |> List.toArray) (List.toArray messages)
                |> Array.toList

        let suffix =
            match AnchorLedger.checkpointCall sessionId with
            | None -> suffix
            | Some(messageId, callId, declaration) ->
                let carry = AnchorCarry.toValue declaration.Carry

                suffix
                |> List.map (fun raw -> stripCarry raw messageId callId declaration.PrefixUntil carry)

        let segments = AnchorLedger.project sessionId (suffix |> List.map input)
        AnchorLedger.remember sessionId inputs segments
        let declaration = AnchorLedger.takeView sessionId

        let report =
            AnchorView.report AnchorSurface.countTokens inputs (AnchorLedger.protectedPrefix sessionId) segments

        let projected =
            segments
            |> List.collect (fun segment ->
                segment.Messages
                |> List.mapi (fun index message ->
                    let raw =
                        if isNull message.Raw then
                            literalMessage (Option.get message.Id) message.Text
                        else
                            copyAnchorSource message.Raw

                    if index = 0 then
                        raw?__contextAnchor <- box segment.Anchor

                    raw))

        let description =
            declaration
            |> Option.map (fun declared ->
                sprintf
                    "Context updated: prefixUntil=%d; visibleSegments=%d; kept=%d inserted=%d dropped=%d; tokens total=%d used=%d cached=%d. The completed turn is retained."
                    declared.PrefixUntil
                    segments.Length
                    report.Kept
                    report.Inserted
                    report.Dropped
                    report.Total
                    report.Used
                    report.Cached)

        projected, description

    let applyAnchorView (sessionId: string) (messages: obj list) : obj list * string option =
        let projected, report = selectAnchorView sessionId messages
        markAnchors projected, report

    let private markDeliveredIfAnchored restartGuidance markRestartGuidanceDelivered anchored =
        // crash-reconciliation-018: the restart guidance is delivered
        // exactly once; a replayed placement never consumes it.
        if anchored && Option.isSome restartGuidance then
            markRestartGuidanceDelivered ()

    let private failClosedInject terminateSession projectionSessionIdOpt reason =
        task {
            Diagnostic.emit
                "host-013-fail-closed"
                [ "session_id", (defaultArg projectionSessionIdOpt ""); "result", reason ]

            do! terminateSessionIfPresent terminateSession projectionSessionIdOpt reason
        }

    let private applyInjectResult
        outObj
        projectionSessionIdOpt
        restartGuidance
        markRestartGuidanceDelivered
        terminateSession
        injectResult
        =
        task {
            match injectResult with
            | Ok(newMessages, anchored) ->
                markDeliveredIfAnchored restartGuidance markRestartGuidanceDelivered anchored

                let marked =
                    projectionSessionIdOpt
                    |> Option.map (fun _ -> markAnchors newMessages)
                    |> Option.defaultValue newMessages

                HostMessageProjection.replaceMessagesInPlace outObj marked
            | Error reason -> do! failClosedInject terminateSession projectionSessionIdOpt reason
        }

    let private injectPairProgrammingGuideline
        (journal: AgentJournal option)
        (projectionSessionIdOpt: string option)
        (sessionStartedAt: DateTimeOffset option)
        (clock: IClockPort)
        (terminateSession: SessionTermination)
        (language: ProviderLanguage)
        (restartGuidance: string option)
        (markRestartGuidanceDelivered: unit -> unit)
        (outObj: obj)
        : Task =
        task {
            let messages = unbox<obj array> outObj?messages |> Array.toList

            let tokenEconomy =
                projectionSessionIdOpt
                |> Option.bind ProviderTokenUse.tryFind
                |> Option.map (fun usage ->
                    PairProgrammingCalibration.renderTokenEconomy language usage.Cached usage.Uncached)

            let contextLimit =
                projectionSessionIdOpt
                |> Option.bind DeclaredContextLimit.tryFind
                |> Option.map (PairProgrammingCalibration.renderContextLimit language)

            let messages, anchorBlock =
                match projectionSessionIdOpt with
                | Some sessionId -> selectAnchorView sessionId messages
                | None -> messages, None

            let guideline = ProviderProse.render language PairProgrammingGuidelinePath Map.empty

            let elapsedMilliseconds =
                sessionStartedAt
                |> Option.map (fun startedAt -> (clock.UtcNow() - startedAt).TotalMilliseconds)

            let budget = budgetText journal projectionSessionIdOpt elapsedMilliseconds language

            // Four calibration numbers when the estimate exists, the elapsed
            // datum alone when it does not: the hint never states the same
            // number twice.
            let elapsed =
                match budget with
                | Some _ -> None
                | None ->
                    elapsedMilliseconds
                    |> Option.map (PairProgrammingCalibration.renderElapsed language)

            let! markerDocument =
                composeMarkerDocument
                    journal
                    projectionSessionIdOpt
                    elapsed
                    [ tokenEconomy; contextLimit ]
                    budget
                    guideline
                    restartGuidance

            let markerDocument =
                match anchorBlock with
                | Some text -> LlmFacing.withInstruction text markerDocument
                | None -> markerDocument

            let markerText = LlmFacing.render markerDocument

            let! injectResult = tryInjectCore journal projectionSessionIdOpt markerText messages

            do!
                applyInjectResult
                    outObj
                    projectionSessionIdOpt
                    restartGuidance
                    markRestartGuidanceDelivered
                    terminateSession
                    injectResult
        }

    let maybeInjectGuideline
        (journal: AgentJournal option)
        (projectionSessionIdOpt: string option)
        (sessionStartedAt: DateTimeOffset option)
        (clock: IClockPort)
        (terminateSession: SessionTermination)
        (language: ProviderLanguage)
        (restartGuidance: string option)
        (markRestartGuidanceDelivered: unit -> unit)
        (outObj: obj)
        : Task =
        if PairGuidanceOccurrences.isInternalSession journal projectionSessionIdOpt then
            Task.FromResult()
        else
            injectPairProgrammingGuideline
                journal
                projectionSessionIdOpt
                sessionStartedAt
                clock
                terminateSession
                language
                restartGuidance
                markRestartGuidanceDelivered
                outObj

    let tryInject
        (journal: AgentJournal option)
        (sessionId: string option)
        (markerText: string)
        (rawMessages: obj list)
        : Task<Result<obj list, string>> =
        task {
            let! result = tryInjectCore journal sessionId markerText rawMessages
            return Result.map fst result
        }
