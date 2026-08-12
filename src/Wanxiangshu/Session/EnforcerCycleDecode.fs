namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Cycle input vocabulary: decode the Host-visible assistant snapshot into a
/// validated merged blog cycle. Pure — never appends to the journal.
module EnforcerCycleDecode =

    /// C4: commit-path UTF-8 safety bounds.
    let MaxBlogTextBytes = 512 * 1024
    let MaxEvidenceBytes = 128 * 1024
    /// ENFORCER-042: defensive multi-call cap (protocol violation still merged).
    let MaxMergedToolCalls = 32

    /// Raw part object → completed `blog` call arguments.
    ///
    /// ENFORCER-041: identity comes from the part itself here (the transform
    /// boundary has no ToolContext), and the fold's replay path reads the same
    /// shape — the assistant message id IS the ProviderRunIdentity, exactly as
    /// XWire derives it (`ProviderRunIdentity.create assistant.Id`).
    let private blogCallFromPart (part: obj) : (ToolCallId * obj) option =
        if isNull part then
            None
        else
            let kind =
                if isNull part?``type`` then
                    ""
                else
                    unbox<string> part?``type``

            let name =
                if isNull part?tool then
                    if isNull part?name then "" else unbox<string> part?name
                else
                    unbox<string> part?tool

            if kind <> "tool" || name <> "chronicle" then
                None
            else
                let callId =
                    if isNull part?callID then
                        if isNull part?callId then
                            None
                        else
                            Some(unbox<string> part?callId)
                    else
                        Some(unbox<string> part?callID)

                let status =
                    if isNull part?state then
                        None
                    else
                        match part?state?status with
                        | null -> None
                        | value -> Some(unbox<string> value)

                match callId, status with
                | Some id, Some "completed" ->
                    let input =
                        if isNull part?state || isNull part?state?input then
                            createEmpty
                        else
                            part?state?input

                    Some(ToolCallId.create id, input)
                | _ -> None

    /// The last assistant message of a transform snapshot and its parts.
    /// Host sets `time.completed` only when the run ends or is interrupted
    /// (SessionSnapshotPort). Outbound `messages.transform` creates the assistant
    /// shell first — completed is unset. ENFORCER-060 must not fire on that shell.
    let private assistantIsCompleted (message: obj) : bool =
        if isNull message then
            false
        else
            let info = if isNull message?info then message else message?info

            let timeCompleted (source: obj) =
                if isNull source || isNull source?time then
                    null
                else
                    source?time?completed

            not (isNull (timeCompleted info)) || not (isNull (timeCompleted message))

    /// Last assistant terminal as (messageId, calls, completed); public so the
    /// Application-layer recovery probe can bind a claim to the same terminal.
    let lastAssistantStep (rawMessages: obj list) : (string * obj list * bool) option =
        rawMessages
        |> List.choose (fun message ->
            if isNull message then
                None
            else
                let info = if isNull message?info then message else message?info

                let role =
                    if isNull info then
                        None
                    else
                        (if isNull info?role then
                             None
                         else
                             Some(unbox<string> info?role))

                let id =
                    if isNull info || isNull info?id then
                        None
                    else
                        Some(unbox<string> info?id)

                match role, id with
                | Some "assistant", Some messageId ->
                    let parts =
                        if isNull message?parts then
                            []
                        else
                            unbox<obj array> message?parts |> Array.toList

                    Some(messageId, parts, assistantIsCompleted message)
                | _ -> None)
        |> List.tryLast

    /// Decode a raw JS object into a string-keyed map (the codec's input shape).
    let private decodeObject (value: obj) : Map<string, obj> =
        if isNull value then
            Map.empty
        else
            let keys: string array = emitJsExpr value "Object.keys($0)"

            keys
            |> Array.fold (fun acc key -> Map.add key (emitJsExpr (value, key) "$0[$1]") acc) Map.empty

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
                |> List.mapi (fun ordinal part -> ordinal, blogCallFromPart part)
                |> List.choose (fun (ordinal, parsed) ->
                    parsed
                    |> Option.bind (fun (callId, input) ->
                        match EnforcerCodec.decodeCall rules (decodeObject input) with
                        | Ok call -> Some(ordinal, callId, call)
                        | Error reason ->
                            // CTX-014: fold identity into result — no whitelist growth for
                            // protocol-skip diagnostics that are never recovery inputs.
                            Diagnostic.emit
                                "enforcer-blog-call-invalid"
                                [ "result",
                                  sprintf "ordinal=%d call_id=%s %s" ordinal (ToolCallId.value callId) reason ]

                            None))

            Some(messageId, calls, completed)

    /// ENFORCER-043: a cycle is valid when the provider run is provable, at
    /// least one call exists, the merged text is non-empty, and every
    /// ToolCallId is unique. Tip is required on each call (decode already).
    let validateCycle
        (messageId: string)
        (calls: (int * ToolCallId * EnforcerCodec.CanonicalBlogCall) list)
        : Result<EnforcerCycle.MergedCycle * ToolCallId list, string> =
        if String.IsNullOrWhiteSpace messageId then
            Error "blog cycle has no provable provider run (ENFORCER-043)"
        elif List.isEmpty calls then
            Error "blog cycle has no completed blog calls (ENFORCER-043)"
        elif List.length calls > MaxMergedToolCalls then
            Error(sprintf "blog cycle exceeds MaxMergedToolCalls=%d" MaxMergedToolCalls)
        else
            let callIds = calls |> List.map (fun (_, callId, _) -> callId)

            if List.length (List.distinct callIds) <> List.length calls then
                Error "blog cycle has duplicate ToolCallIds (ENFORCER-043)"
            else
                let merged =
                    EnforcerCycle.mergeCalls (calls |> List.map (fun (ordinal, _, call) -> ordinal, call))

                if merged.MultiCall then
                    // ENFORCER-042: multi-call is a protocol violation; still merge defensively.
                    Diagnostic.emit
                        "enforcer-protocol-violation"
                        [ "result",
                          "multiple blog calls in one provider step; tip = first by PartOrdinal (ENFORCER-025)"
                          "call_count", string (List.length calls) ]

                if not (EnforcerCycle.isValidCycle merged) then
                    Error "blog cycle merged text is empty after canonicalisation (ENFORCER-043)"
                elif SyntheticToml.byteCount merged.MergedText > MaxBlogTextBytes then
                    Error(sprintf "blog cycle text exceeds MaxBlogTextBytes=%d" MaxBlogTextBytes)
                elif SyntheticToml.byteCount merged.MergedEvidence > MaxEvidenceBytes then
                    Error(sprintf "blog cycle evidence exceeds MaxEvidenceBytes=%d" MaxEvidenceBytes)
                else
                    Ok(merged, callIds)
