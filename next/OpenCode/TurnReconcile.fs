namespace Wanxiangshu.Next.OpenCode

open Wanxiangshu.Next.Kernel.Identity

/// Pure `snapshot + binding -> ReconciledTurn option`.
/// Unknown origin is None.  Uses CompletedTurnClassifier and TerminalSessionA
/// logic, but never reads the raw host payload.
module TurnReconcile =

    let private isAdmissionId (messageId: MessageId) =
        (MessageId.value messageId).StartsWith("accepted-")

    let private users (messages: SessionMessage list) =
        messages |> List.filter (fun message -> message.Role = "user")

    let private containsMessage (messages: SessionMessage list) (messageId: MessageId) =
        messages |> List.exists (fun message -> message.Id = messageId)

    let private resolveRoot (messages: SessionMessage list) (root: MessageId) =
        if containsMessage messages root then
            Some root
        elif isAdmissionId root then
            // The Host's async prompt endpoint returns a session-scoped admission
            // id, never a message identity. A newly registered AgentOwnerRoot is the
            // first physical user in that child transcript; this is causal
            // correlation, not text guessing.
            users messages |> List.tryHead |> Option.map (fun message -> message.Id)
        else
            None

    let private resolvePhysical
        (messages: SessionMessage list)
        (declaredRoot: MessageId)
        (root: MessageId)
        (physical: MessageId)
        (isContinuation: bool)
        =
        if isContinuation && isAdmissionId physical then
            users messages |> List.tryLast |> Option.map (fun message -> message.Id)
        elif physical = declaredRoot then
            Some root
        elif containsMessage messages physical then
            Some physical
        elif isAdmissionId physical then
            // A registered continuation appends exactly one new physical user.
            // Select the newest appended user while retaining the resolved
            // authority root separately.
            users messages |> List.tryLast |> Option.map (fun message -> message.Id)
        else
            None

    let private findAssistantAfter (messages: SessionMessage list) (userMessageId: MessageId) : SessionMessage option =
        let userId = MessageId.value userMessageId

        let rec skipUntilUser remaining =
            match remaining with
            | [] -> None
            | head :: tail when MessageId.value head.Id = userId -> Some tail
            | _ :: tail -> skipUntilUser tail

        match skipUntilUser messages with
        | None -> None
        | Some afterUser ->
            afterUser
            |> List.filter (fun message -> message.Role = "assistant")
            |> List.tryLast

    /// Reconcile a full snapshot against an active run binding.
    /// Returns None when the origin is unknown (no authority root) or no
    /// assistant message follows the current physical user message.
    let reconcile (messages: SessionMessage list) (binding: ActiveRunBinding) : ReconciledTurn option =
        match binding.RootUserMessageId with
        | None -> None
        | Some declaredRoot ->
            match resolveRoot messages declaredRoot with
            | None -> None
            | Some rootUserMessageId ->
                let declaredPhysical =
                    binding.PhysicalUserMessageId |> Option.defaultValue declaredRoot

                let isContinuation =
                    binding.ContinuationMessageIds.Contains(MessageId.value declaredPhysical)

                match resolvePhysical messages declaredRoot rootUserMessageId declaredPhysical isContinuation with
                | None -> None
                | Some physicalUserMessageId ->
                    let assistant =
                        findAssistantAfter messages physicalUserMessageId
                        |> Option.orElseWith (fun () ->
                            if physicalUserMessageId = rootUserMessageId then
                                None
                            else
                                findAssistantAfter messages rootUserMessageId)

                    match assistant with
                    | None -> None
                    | Some assistant ->
                        Some(
                            CompletedTurnClassifier.buildTurn
                                binding.SessionId
                                physicalUserMessageId
                                rootUserMessageId
                                assistant
                                binding.AgentRole
                                binding.Directory
                        )
