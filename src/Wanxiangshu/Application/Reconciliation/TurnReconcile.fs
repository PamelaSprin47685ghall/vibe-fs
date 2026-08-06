namespace Wanxiangshu.OpenCode

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Pure `snapshot + binding -> ReconciledTurn option`. Unknown origin is None.
///
/// Works in raw wire addresses and constructs typed identities only where the
/// role makes them meaningful: a `role=user` message becomes a
/// `PhysicalUserMessageId` (PROMPT-001), a `role=assistant` message becomes a
/// `ProviderRunIdentity` (HOST-010). Never reads the raw host payload.
module TurnReconcile =

    /// PROMPT-005: a transport receipt looks like `accepted-*` and is not a message
    /// address. `TransportReceipt.isAdmissionShaped` owns that test, so the prefix
    /// is not spelled a second time here.
    let private isAdmissionAddress (address: string) =
        TransportReceipt.isAdmissionShaped (TransportReceipt.create address)

    let private users (messages: SessionMessage list) =
        messages |> List.filter (fun message -> message.Role = "user")

    let private containsAddress (messages: SessionMessage list) (address: string) =
        messages |> List.exists (fun message -> message.Id = address)

    let private resolveRoot (messages: SessionMessage list) (declaredRoot: string) =
        if containsAddress messages declaredRoot then
            Some declaredRoot
        elif isAdmissionAddress declaredRoot then
            // The Host's async prompt endpoint returns a session-scoped admission
            // receipt, never a message address. A newly registered AgentOwnerRoot is
            // the first physical user in that child transcript; this is causal
            // correlation, not text guessing.
            users messages |> List.tryHead |> Option.map (fun message -> message.Id)
        else
            None

    let private resolvePhysical
        (messages: SessionMessage list)
        (declaredRoot: string)
        (root: string)
        (physical: string)
        (declaredPhysicalIsContinuationUser: bool)
        =
        if declaredPhysicalIsContinuationUser && isAdmissionAddress physical then
            users messages |> List.tryLast |> Option.map (fun message -> message.Id)
        elif physical = declaredRoot then
            Some root
        elif containsAddress messages physical then
            Some physical
        elif isAdmissionAddress physical then
            // A registered continuation appends exactly one new physical user.
            // Select the newest appended user while keeping the resolved authority
            // root separate.
            users messages |> List.tryLast |> Option.map (fun message -> message.Id)
        else
            None

    let private findAssistantAfter (messages: SessionMessage list) (userAddress: string) : SessionMessage option =
        let rec skipUntilUser remaining =
            match remaining with
            | [] -> None
            | head :: tail when head.Id = userAddress -> Some tail
            | _ :: tail -> skipUntilUser tail

        match skipUntilUser messages with
        | None -> None
        | Some afterUser ->
            afterUser
            |> List.filter (fun message -> message.Role = "assistant")
            |> List.tryLast

    /// Reconcile a full snapshot against an active run binding.
    ///
    /// `None` when the origin is unknown (no authority root) or no assistant
    /// message follows the current physical user message.
    ///
    /// With no bound physical message the root's own wire address is used: the
    /// Authority Root was promoted from a physical message, so that address exists
    /// in the transcript. The address is read out rather than converted back into a
    /// `PhysicalUserMessageId`, because PROMPT-002 makes the promotion one-way and
    /// there is deliberately no inverse.
    let reconcile (messages: SessionMessage list) (binding: ActiveRunBinding) : ReconciledTurn option =
        match binding.AuthorityRootUserMessageId with
        | None -> None
        | Some declaredRootId ->
            let declaredRoot = AuthorityRootUserMessageId.value declaredRootId

            match resolveRoot messages declaredRoot with
            | None -> None
            | Some root ->
                let declaredPhysical =
                    binding.PhysicalUserMessageId
                    |> Option.map PhysicalUserMessageId.value
                    |> Option.defaultValue declaredRoot

                let declaredPhysicalIsContinuationUser =
                    binding.ContinuationMessageIds.Contains declaredPhysical

                match
                    resolvePhysical messages declaredRoot root declaredPhysical declaredPhysicalIsContinuationUser
                with
                | None -> None
                | Some physical ->
                    let assistant =
                        findAssistantAfter messages physical
                        |> Option.orElseWith (fun () ->
                            if physical = root then
                                None
                            else
                                findAssistantAfter messages root)

                    match assistant with
                    | None -> None
                    | Some assistant ->
                        Some(
                            CompletedTurnClassifier.buildTurn
                                binding.SessionId
                                (PhysicalUserMessageId.create physical)
                                (AuthorityRootUserMessageId.create root)
                                assistant
                                binding.Role
                                binding.Directory
                        )
