namespace Wanxiangshu.Enforcer.Cycle

open Wanxiangshu.OpenCode
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Cycle input vocabulary: decode the Host-visible assistant snapshot into a
/// validated merged blog cycle. Pure — never appends to the journal.
module EnforcerCycleDecode =

    /// C4: commit-path UTF-8 safety bounds.
    let MaxBlogTextBytes = 512 * 1024
    let MaxEvidenceBytes = 128 * 1024

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

    /// The last assistant message of a transform snapshot and its parts.
    /// Host sets `time.completed` only when the run ends or is interrupted
    /// (SessionSnapshotPort). Outbound `messages.transform` creates the assistant
    /// shell first — completed is unset. ENFORCER-060 must not fire on that shell.
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

    let private messageParts (message: obj) : obj list =
        if isNull message?parts then
            []
        else
            unbox<obj array> message?parts |> Array.toList

    let private assistantStepFromMessage (message: obj) : (string * obj list * bool) option =
        let info = messageInfo message

        match messageRole info, messageIdOf info with
        | Some "assistant", Some messageId -> Some(messageId, messageParts message, assistantIsCompleted message)
        | _ -> None

    /// Last assistant terminal as (messageId, calls, completed); public so the
    /// Application-layer recovery probe can bind a claim to the same terminal.
    let lastAssistantStep (rawMessages: obj list) : (string * obj list * bool) option =
        rawMessages
        |> List.choose (fun message ->
            if isNull message then
                None
            else
                assistantStepFromMessage message)
        |> List.tryLast

    /// Decode a raw JS object into a string-keyed map (the codec's input shape).
    let private decodeObject (value: obj) : Map<string, obj> =
        if isNull value then
            Map.empty
        else
            let keys: string array = emitJsExpr value "Object.keys($0)"

            keys
            |> Array.fold (fun acc key -> Map.add key (emitJsExpr (value, key) "$0[$1]") acc) Map.empty

    let private decodeCanonicalCall
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
            Diagnostic.emit
                "enforcer-blog-call-invalid"
                [ "result", sprintf "ordinal=%d call_id=%s %s" ordinal (ToolCallId.value callId) reason ]

            None

    let private tryCanonicalCall
        (rules: EnforcerRule list)
        (ordinal: int, part: obj)
        : (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) option =
        blogCallFromPart part
        |> Option.bind (fun (callId, input) -> decodeCanonicalCall rules ordinal callId input)

    /// ENFORCER-042: (PartOrdinal, ToolCallId, CanonicalBlogCall) for one
    /// provider step, in provider-visible order. The ordinal is the part's
    /// index in the assistant message — the only ordering that survives
    /// parallel execution.
    ///
    /// ENFORCER-023: only calls that pass tip re-validation enter the list.
    /// Failed tip decode is a protocol skip (execute should already have
    /// rejected; defense in depth at transform).
    let extractCalls
        (rawMessages: obj list)
        : (string * (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list * bool) option =
        match lastAssistantStep rawMessages with
        | None -> None
        | Some(messageId, parts, completed) ->
            let rules = RuntimeResources.current().EnforcerRules

            let calls =
                parts
                |> List.mapi (fun ordinal part -> ordinal, part)
                |> List.choose (tryCanonicalCall rules)

            Some(messageId, calls, completed)

    let private validateBounds
        (cycle: EnforcerCycle.CanonicalCycle)
        (callId: ToolCallId)
        : Result<EnforcerCycle.CanonicalCycle * ToolCallId list, string> =
        if not (EnforcerCycle.isValidCycle cycle) then
            Error EmptyTextError
        elif LlmFacing.byteCount cycle.MergedText > MaxBlogTextBytes then
            Error(sprintf "blog cycle text exceeds MaxBlogTextBytes=%d" MaxBlogTextBytes)
        elif LlmFacing.byteCount cycle.MergedEvidence > MaxEvidenceBytes then
            Error(sprintf "blog cycle evidence exceeds MaxEvidenceBytes=%d" MaxEvidenceBytes)
        else
            Ok(cycle, [ callId ])

    let private validateSingleCall
        (calls: (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list)
        : Result<EnforcerCycle.CanonicalCycle * ToolCallId list, string> =
        match calls with
        | [ (_, callId, call) ] -> validateBounds (EnforcerCycle.ofCall call) callId
        | [] -> Error "blog cycle has no completed chronicle call (ENFORCER-043)"
        | _ -> Error "blog cycle must contain exactly one chronicle call (ENFORCER-042)"

    /// ENFORCER-043: after the raw exact-one gate, the provider run and one
    /// canonical chronicle call must both be provable; canonical text is non-empty.
    let validateCycle
        (messageId: string)
        (calls: (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list)
        : Result<EnforcerCycle.CanonicalCycle * ToolCallId list, string> =
        if String.IsNullOrWhiteSpace messageId then
            Error "blog cycle has no provable provider run (ENFORCER-043)"
        else
            validateSingleCall calls
