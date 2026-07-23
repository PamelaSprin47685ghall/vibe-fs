module Wanxiangshu.Tests.SubagentOutputTranscriptTests

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.SubsessionTranscript
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Tests.Assert

let private fail (msg: string) = check msg false

// ── tryBuildLatestAssistantEvidence: user-message boundary isolation ──

/// Build a raw transcript message object with the given role and optional text parts.
/// Uses Wanxiangshu.Runtime.Dyn so the shape matches what SubsessionTranscript.Dyn.get expects.
let private makeMsgWithId (id: string) (role: string) (textParts: string list) : obj =
    let parts =
        textParts
        |> List.map (fun t -> createObj [ "type", box "text"; "text", box t ])
        |> Array.ofList

    createObj [ "id", box id; "info", createObj [ "role", box role ]; "parts", box parts ]

let private makeMsg (role: string) (textParts: string list) : obj = makeMsgWithId "" role textParts

/// (a) User message exists but no assistant after the last user → None.
/// Bug: tryBuildLatestAssistantEvidence scans the full transcript and returns the
/// pre-user stale assistant instead of respecting the user boundary.
let userThenNoAssistantReturnsNone () =
    let msgs =
        [| makeMsg "assistant" [ "old answer before user" ]
           makeMsg "user" [ "what is 2+2?" ] |]
        |> Array.map box

    match tryBuildLatestAssistantEvidence msgs with
    | None -> check "user with no trailing assistant → None" true
    | Some _ ->
        // Under the bug, the pre-user stale assistant is returned unconditionally.
        fail "expected None when no assistant after last user"

/// (b) Old assistant before last user, new assistant after it → only post-user text.
/// Bug: old pre-user assistant is returned instead of the new one.
let newAssistantAfterUserReturnsNewText () =
    let msgs =
        [| makeMsg "assistant" [ "stale answer" ]
           makeMsg "user" [ "new question" ]
           makeMsg "assistant" [ "fresh answer" ] |]
        |> Array.map box

    match tryBuildLatestAssistantEvidence msgs with
    | None -> fail "expected Some evidence after user+assistant"
    | Some ev ->
        match ev.Assistant with
        | AssistantSnapshot(_, _, text) -> equal "only post-user assistant text returned" "fresh answer" text
        | other -> fail ("expected AssistantSnapshot, got " + string other)

/// (c) No user message → latest assistant text returned (full-history fallback path).
let noUserReturnsLatestAssistant () =
    let msgs =
        [| makeMsg "assistant" [ "first reply" ]
           makeMsg "assistant" [ "second reply" ] |]
        |> Array.map box

    match tryBuildLatestAssistantEvidence msgs with
    | None -> fail "expected Some evidence from assistant-only transcript"
    | Some ev ->
        match ev.Assistant with
        | AssistantSnapshot(_, _, text) -> equal "latest assistant when no user boundary" "second reply" text
        | other -> fail ("expected AssistantSnapshot, got " + string other)

let anchorUsesAbsoluteIndex () =
    let msgs =
        [| makeMsgWithId "u1" "user" [ "first question" ]
           makeMsg "assistant" [ "stale answer" ]
           makeMsgWithId "u2" "user" [ "second question" ] |]
        |> Array.map box

    match buildTurnEvidence msgs (AnchorByUserMessageId "u2") with
    | Ok(evidence: CurrentTurnEvidence) ->
        match evidence.Assistant with
        | NoAssistant -> check "current user anchor excludes stale assistant" true
        | other -> fail ("expected NoAssistant, got " + string other)
    | Error err -> fail err.Message

let anchorIncludesOnlyCurrentAssistant () =
    let msgs =
        [| makeMsgWithId "u1" "user" [ "first question" ]
           makeMsg "assistant" [ "stale answer" ]
           makeMsgWithId "u2" "user" [ "second question" ]
           makeMsg "assistant" [ "fresh answer" ] |]
        |> Array.map box

    match buildTurnEvidence msgs (AnchorByUserMessageId "u2") with
    | Ok(evidence: CurrentTurnEvidence) ->
        match evidence.Assistant with
        | AssistantSnapshot(_, _, text) -> equal "current user anchor includes fresh assistant" "fresh answer" text
        | other -> fail ("expected AssistantSnapshot, got " + string other)
    | Error err -> fail err.Message

let oldUserAnchorIsRejected () =
    let msgs =
        [| makeMsgWithId "u1" "user" [ "first question" ]
           makeMsg "assistant" [ "stale answer" ]
           makeMsgWithId "u2" "user" [ "second question" ] |]
        |> Array.map box

    match buildTurnEvidence msgs (AnchorByUserMessageId "u1") with
    | Error _ -> check "old user anchor is rejected" true
    | Ok _ -> fail "expected old user anchor to be rejected"

let run () =
    userThenNoAssistantReturnsNone ()
    newAssistantAfterUserReturnsNewText ()
    noUserReturnsLatestAssistant ()
    anchorUsesAbsoluteIndex ()
    anchorIncludesOnlyCurrentAssistant ()
    oldUserAnchorIsRejected ()
