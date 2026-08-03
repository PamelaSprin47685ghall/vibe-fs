namespace Wanxiangshu.OpenCode

open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal

/// chat.message authority policy (PROMPT-004).
///
/// This is the only place a physical user message becomes authority. Raw host
/// payloads are decoded by PromptIngressCodec; unknown origins fail closed.
[<RequireQualifiedAccess>]
module PromptIngress =

    let private isValidAgent (value: string) =
        match PromptAuthority.parseAgentName value with
        | Ok _ -> true
        | Error _ -> false

    /// PROMPT-004 resolution order: what the journal already knows, then — only
    /// for a message the journal has never seen — an explicit managed agent makes
    /// it a HumanRoot.
    ///
    /// The order matters in one direction only. A message with a known origin must
    /// never be reclassified by its `agent` field, because a continuation the
    /// plugin itself sent also carries one, and treating that as a HumanRoot would
    /// start a new Logical Run inside the run it was continuing.
    let private resolveOrigin
        (runtime: PromptDispatcher.Runtime)
        (sessionId: SessionId)
        (message: PromptIngressCodec.DecodedMessage)
        (physicalMessageId: PhysicalUserMessageId)
        =
        match runtime.ResolveOrigin physicalMessageId message.PromptKey message.IsHostCompaction sessionId with
        | PromptAuthority.PromptOrigin.UnknownOrigin ->
            match message.ExplicitAgent with
            | Some agent when isValidAgent agent ->
                PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot
            | _ -> PromptAuthority.PromptOrigin.UnknownOrigin
        | resolved -> resolved

    let private handle
        (journal: AgentJournal option)
        (bindUserMessage: string -> string -> unit)
        (bindContinuationMessage: string -> string -> unit)
        (registerOwned: string -> unit)
        (onAuthorityRoot: (SessionId -> unit) option)
        (message: PromptIngressCodec.DecodedMessage)
        =
        // PROMPT-005: accepting a prompt is a durable act. Without a journal there
        // is nothing to accept into, and inventing an in-memory authority here
        // would let a Logical Run exist that no restart could rediscover.
        match journal, message.SessionId, message.PhysicalUserMessageId with
        | Some durable, Some sessionId, Some physicalMessageId ->
            let runtime = PromptDispatcher.forJournal durable
            let sessionKey = SessionId.value sessionId
            let messageKey = PhysicalUserMessageId.value physicalMessageId

            // AGENT-007: nothing is cached here. The accepted `AuthorityRootAccepted`
            // fact is the record of the role, and every consumer reads it back from
            // the projection — so there is no second copy to fall out of step.
            let acceptedRoot () =
                bindUserMessage sessionKey messageKey
                registerOwned sessionKey
                // New Authority Root may reopen Blogger after a prior join/return seal.
                onAuthorityRoot |> Option.iter (fun f -> f sessionId)

            // COMPANION-003: ONLY a HumanRoot's first prompt is captured as the
            // OpeningPromptRaw here. An AgentOwnerRoot (a forked child's first
            // prompt) carries the rendered transport envelope — assignment,
            // parent_work_record and instruction comments — so capturing it would
            // nest the parent LWR inside the child's opening and grow recursively
            // with every generation (EXEC-006). The fork path captures the child's
            // opening from the ORIGINAL assignment before rendering.
            let captureOpeningIfHumanRoot () =
                message.Text
                |> Option.iter (fun text -> XTraceCapture.captureOpening journal sessionId text [])

            match resolveOrigin runtime sessionId message physicalMessageId with
            | PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot ->
                match runtime.AcceptHumanRoot sessionId physicalMessageId message.ExplicitAgent with
                | Ok _ ->
                    acceptedRoot ()
                    captureOpeningIfHumanRoot ()
                | Error _ -> ()

            | PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot ->
                // An AgentOwnerRoot is only recognisable by its PromptKey. Without
                // one there is no claim to resolve, so this stays unknown rather
                // than being promoted on the strength of the `agent` field.
                match message.PromptKey with
                | Some key ->
                    match runtime.AcceptAgentOwnerRoot key sessionId physicalMessageId with
                    | Ok _ -> acceptedRoot ()
                    | Error _ -> ()
                | None -> ()

            | PromptAuthority.PromptOrigin.Continuation _ ->
                match message.PromptKey with
                | Some key ->
                    // `AcceptContinuation` writes PROMPT-005 `PhysicalAccepted` with
                    // this real physical id, and that is the entire record of the
                    // landing. Nothing further is written for review purposes:
                    // REVIEW-003 forbids a continuation's acceptance as confirmation
                    // evidence, since landing a prompt says nothing about whether a
                    // model consumed the challenge. That proof is the provider input
                    // seal (REVIEW-010).
                    match runtime.AcceptContinuation key sessionId physicalMessageId with
                    | Ok _ ->
                        bindContinuationMessage sessionKey messageKey
                        registerOwned sessionKey
                    | Error _ -> ()
                | None -> ()

            | PromptAuthority.PromptOrigin.HostInternal
            | PromptAuthority.PromptOrigin.UnknownOrigin -> ()
        | _ -> ()

    let createHook
        (journal: AgentJournal option)
        (bindUserMessage: string -> string -> unit)
        (bindContinuationMessage: string -> string -> unit)
        (registerOwned: string -> unit)
        (onAuthorityRoot: (SessionId -> unit) option)
        =
        fun (input: obj) (output: obj) ->
            PromptIngressCodec.decode input output
            |> handle journal bindUserMessage bindContinuationMessage registerOwned onAuthorityRoot
