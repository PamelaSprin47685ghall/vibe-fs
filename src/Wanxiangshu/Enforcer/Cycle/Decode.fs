namespace Wanxiangshu.Enforcer.Cycle

open System
open Fable.Core.JsInterop
open Wanxiangshu.Enforcer
open Wanxiangshu.Foundation
open Wanxiangshu.Resources
open Wanxiangshu.Foundation.Identity

/// Cycle input vocabulary: decode the Host-visible assistant snapshot into a
/// validated merged blog cycle. Pure — never appends to the journal.
module EnforcerCycleDecode =

    [<Literal>]
    let EmptyTextError =
        "blog cycle text is empty after canonicalisation (ENFORCER-043)"

    let private optUnboxString (value: obj) : string option =
        if isNull value then None else Some(unbox<string> value)

    let private stringOrEmpty (value: obj) : string =
        match optUnboxString value with
        | None -> ""
        | Some text -> text

    let private callIdOf (part: obj) : string option =
        match optUnboxString part?callID with
        | Some id -> Some id
        | None -> optUnboxString part?callId

    let private statusOf (part: obj) : string option =
        if isNull part?state then
            None
        else
            optUnboxString part?state?status

    let private inputOf (part: obj) : obj =
        if isNull part?state || isNull part?state?input then
            createEmpty
        else
            part?state?input

    let private toolNameOf (part: obj) : string =
        match optUnboxString part?tool with
        | Some tool -> tool
        | None -> stringOrEmpty part?name

    let private completedBlogInput (part: obj) : (ToolCallId * obj) option =
        match callIdOf part, statusOf part with
        | Some id, Some "completed" -> Some(ToolCallId.create id, inputOf part)
        | _ -> None

    /// Raw part object → completed `blog` call arguments.
    ///
    /// ENFORCER-041: identity comes from the part itself here (the transform
    /// boundary has no ToolContext), and the fold's replay path reads the same
    /// shape — the assistant message id IS the ProviderRunIdentity, exactly as
    /// XWire derives it (`ProviderRunIdentity.create assistant.Id`).
    let private blogCallFromPart (part: obj) : (ToolCallId * obj) option =
        if isNull part then
            None
        elif stringOrEmpty part?``type`` <> "tool" || toolNameOf part <> "chronicle" then
            None
        else
            completedBlogInput part

    let private messageInfo (message: obj) : obj =
        if isNull message?info then message else message?info

    let private timeCompleted (source: obj) =
        if isNull source || isNull source?time then
            null
        else
            source?time?completed

    /// Host sets `time.completed` only when a step ends or is interrupted
    /// (SessionSnapshotPort).
    let private assistantIsCompleted (message: obj) : bool =
        if isNull message then
            false
        else
            let info = messageInfo message
            not (isNull (timeCompleted info)) || not (isNull (timeCompleted message))

    let private messageRole (info: obj) : string option =
        if isNull info then None else optUnboxString info?role

    let private messageIdOf (info: obj) : string option =
        if isNull info then None
        elif isNull info?id then None
        else Some(unbox<string> info?id)

    let private parentIdOf (info: obj) : string option =
        if isNull info then None else optUnboxString info?parentID

    let private messageParts (message: obj) : obj list =
        if isNull message?parts then
            []
        else
            unbox<obj array> message?parts |> Array.toList

    /// One Host assistant message: the provider step it recorded.
    type AssistantStep =
        { MessageId: string
          Parts: obj list
          Completed: bool }

    /// Where the provider step being built stands relative to the physical
    /// user message it answers. Host `prompt.ts` answers the latest user
    /// message (`MessageV2.latest`), stamps every assistant of that loop with
    /// `parentID = user.id`, and runs the transform before the outbound
    /// assistant exists, so the transform input never holds the step being built.
    [<RequireQualifiedAccess>]
    type StepPosition =
        /// The transform input has no physical user message.
        | NoRequest
        /// First step of the physical message: nothing answers it yet. An
        /// assistant at the history tail answers an older physical message.
        | First of physical: PhysicalUserMessageId
        /// A later step of the same Host loop: `previous` is the latest
        /// assistant whose parentID is this physical message.
        | After of physical: PhysicalUserMessageId * previous: AssistantStep

    let private roleOf (message: obj) : string option = messageRole (messageInfo message)

    let private present (rawMessages: obj list) : obj list =
        rawMessages |> List.filter (fun message -> not (isNull message))

    let private assistantStepOf (message: obj) : AssistantStep =
        { MessageId = messageIdOf (messageInfo message) |> Option.defaultValue ""
          Parts = messageParts message
          Completed = assistantIsCompleted message }

    let private positionAfter (messages: obj list) (userId: string) : StepPosition =
        let physical = PhysicalUserMessageId.create userId

        let previous =
            messages
            |> List.filter (fun message ->
                roleOf message = Some "assistant"
                && parentIdOf (messageInfo message) = Some userId)
            |> List.tryLast

        match previous with
        | Some message -> StepPosition.After(physical, assistantStepOf message)
        | None -> StepPosition.First physical

    let stepPosition (rawMessages: obj list) : StepPosition =
        let messages = present rawMessages

        let latestUser =
            messages
            |> List.filter (fun message -> roleOf message = Some "user")
            |> List.tryLast
            |> Option.bind (messageInfo >> messageIdOf)

        match latestUser with
        | None -> StepPosition.NoRequest
        | Some userId -> positionAfter messages userId

    /// The history-tail assistant, whatever physical message it answers. Only
    /// for classifying one given assistant message, never for step position.
    let latestAssistant (rawMessages: obj list) : AssistantStep option =
        present rawMessages
        |> List.filter (fun message -> roleOf message = Some "assistant")
        |> List.tryLast
        |> Option.map assistantStepOf

    /// Decode a raw JS object into a string-keyed map (the codec's input shape).
    let private decodeObject (value: obj) : Map<string, obj> =
        if isNull value then
            Map.empty
        else
            let keys: string array = emitJsExpr value "Object.keys($0)"

            keys
            |> Array.fold (fun acc key -> Map.add key (emitJsExpr (value, key) "$0[$1]") acc) Map.empty

    let private decodeCanonicalCall
        (emitDiagnostic: string -> (string * string) list -> unit)
        (rules: EnforcerRule list)
        (ordinal: int)
        (callId: ToolCallId)
        (input: obj)
        : (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) option =
        match EnforcerCodec.decodeCall rules (decodeObject input) with
        | Ok call -> Some(ordinal, callId, call)
        | Error reason ->
            // CTX-014: fold identity into result — no whitelist growth for
            // protocol-skip diagnostics that are never recovery inputs.
            emitDiagnostic
                "enforcer-blog-call-invalid"
                [ "result", sprintf "ordinal=%d call_id=%s %s" ordinal (ToolCallId.value callId) reason ]

            None

    let private tryCanonicalCall
        (emitDiagnostic: string -> (string * string) list -> unit)
        (rules: EnforcerRule list)
        (ordinal: int, part: obj)
        : (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) option =
        blogCallFromPart part
        |> Option.bind (fun (callId, input) -> decodeCanonicalCall emitDiagnostic rules ordinal callId input)

    /// ENFORCER-042: (PartOrdinal, ToolCallId, CanonicalBlogCall) for one
    /// provider step, in provider-visible order. The ordinal is the part's
    /// index in the assistant message — the only ordering that survives
    /// parallel execution.
    ///
    /// ENFORCER-023: only calls that pass tip re-validation enter the list.
    /// Failed tip decode is a protocol skip (execute should already have
    /// rejected; defense in depth at transform).
    let callsOf
        (emitDiagnostic: string -> (string * string) list -> unit)
        (step: AssistantStep)
        : (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list =
        let rules = RuntimeResources.current().EnforcerRules

        step.Parts
        |> List.mapi (fun ordinal part -> ordinal, part)
        |> List.choose (tryCanonicalCall emitDiagnostic rules)

    /// ENFORCER-042 / behavior-diagnosis-009: raw chronicle call cardinality
    /// of one provider step, counted by tool identity alone — before any
    /// completion or decode filtering. A second undecodable call still makes
    /// the cardinality two.
    let chronicleCallCount (step: AssistantStep) : int =
        step.Parts
        |> List.filter (fun part ->
            not (isNull part)
            && stringOrEmpty part?``type`` = "tool"
            && toolNameOf part = "chronicle")
        |> List.length

    let private validateBounds
        (cycle: EnforcerCycle.CanonicalCycle)
        (callId: ToolCallId)
        : Result<EnforcerCycle.CanonicalCycle * ToolCallId list, string> =
        if not (EnforcerCycle.isValidCycle cycle) then
            Error EmptyTextError
        else
            EnforcerCycle.validateContentBounds LlmFacing.byteCount cycle.MergedText cycle.MergedEvidence
            |> Result.mapError EnforcerCycle.contentBoundsError
            |> Result.map (fun _ -> cycle, [ callId ])

    let private validateSingleCall
        (calls: (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list)
        : Result<EnforcerCycle.CanonicalCycle * ToolCallId list, string> =
        match calls with
        | [ (_, callId, call) ] -> validateBounds (EnforcerCycle.ofCall call) callId
        | [] -> Error "blog cycle has no completed chronicle call (ENFORCER-043)"
        | _ -> Error "blog cycle must contain exactly one chronicle call (ENFORCER-042)"

    /// ENFORCER-043 / behavior-diagnosis-009: the raw exact-one gate runs
    /// BEFORE decode filtering — a step whose raw chronicle call count is not
    /// exactly one is a protocol breach however many of those calls decode.
    /// After that gate the provider run and one canonical chronicle call must
    /// both be provable; canonical text is non-empty.
    let validateCycle
        (messageId: string)
        (rawCallCount: int)
        (calls: (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list)
        : Result<EnforcerCycle.CanonicalCycle * ToolCallId list, string> =
        if rawCallCount <> 1 then
            Error(sprintf "blog cycle chronicle call count = %d; expected exactly one (ENFORCER-042)" rawCallCount)
        elif String.IsNullOrWhiteSpace messageId then
            Error "blog cycle has no provable provider run (ENFORCER-043)"
        else
            validateSingleCall calls
