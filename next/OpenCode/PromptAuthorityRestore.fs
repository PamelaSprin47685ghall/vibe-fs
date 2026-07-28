namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

module PromptAuthorityRestore =

    let private toProfile
        (sessionId: SessionId)
        (p: AuthorityProfileProjection)
        : Result<PromptAuthority.AuthorityExecutionProfile, string> =
        let kind =
            match p.AuthorityKind with
            | "AgentOwnerRoot" -> PromptAuthority.AgentOwnerRoot
            | _ -> PromptAuthority.HumanRoot

        match ManagedAgent.parse p.SelectedAgent with
        | Error err -> Error(ManagedAgent.formatParseError err)
        | Ok selected ->
            let peer = ManagedAgent.peer selected

            let canonicalRole =
                match PromptAuthority.tryParseRole p.CanonicalRole with
                | Some role -> role
                | None -> selected.Role

            let selectedTier =
                match PromptAuthority.tryParseTier p.SelectedTier with
                | Some tier -> tier
                | None -> selected.Tier

            let peerAgent =
                if String.IsNullOrWhiteSpace p.PeerAgent then
                    peer.Name
                else
                    p.PeerAgent

            Ok
                { SessionId = sessionId
                  LogicalRunId = p.LogicalRunId
                  AuthorityRootUserMessageId = MessageId.create p.AuthorityRootUserMessageId
                  AuthorityKind = kind
                  SelectedAgent = selected.Name
                  PeerAgent = peerAgent
                  CanonicalRole = canonicalRole
                  SelectedTier = selectedTier }

    /// Rebuild the in-memory authority projection from durable journal facts.
    /// Pending claims only reconstruct continuation kinds that still have an active run.
    let fromJournal (journal: AgentJournal) : PromptAuthority.PromptAuthorityProjection =
        let mutable projection = PromptAuthority.empty

        for KeyValue(sessionId, session) in (AgentJournal.snapshot journal).AgentProjections.Sessions do
            match session.PromptAuthority with
            | None -> ()
            | Some durable ->
                let last =
                    durable.LastAuthorityProfile
                    |> Option.bind (fun p ->
                        match toProfile sessionId p with
                        | Ok profile -> Some profile
                        | Error _ -> None)

                let active =
                    durable.ActiveLogicalRun
                    |> Option.bind (fun p ->
                        match toProfile sessionId p with
                        | Ok profile -> Some profile
                        | Error _ -> None)

                let pending =
                    durable.PendingClaims
                    |> Map.toList
                    |> List.choose (fun (promptKey, kindLabel) ->
                        match PromptAuthority.tryParseContinuationKind kindLabel, active with
                        | Some kind, Some profile ->
                            let key = PromptKeyRef.create promptKey

                            Some(
                                key,
                                PromptAuthority.claimContinuation key sessionId kind profile profile.SelectedAgent
                            )
                        | _ -> None)
                    |> Map.ofList

                let accepted =
                    durable.AcceptedContinuationIds
                    |> Map.toList
                    |> List.choose (fun (messageId, kindLabel) ->
                        match PromptAuthority.tryParseContinuationKind kindLabel with
                        | Some kind -> Some(MessageId.create messageId, kind)
                        | None -> None)
                    |> Map.ofList

                let repair = durable.RepairClaims |> Set.ofList

                projection <-
                    { LastAuthorityProfile = last |> Option.orElse projection.LastAuthorityProfile
                      ActiveLogicalRun = active |> Option.orElse projection.ActiveLogicalRun
                      PendingClaims = Map.fold (fun acc k v -> Map.add k v acc) projection.PendingClaims pending
                      AcceptedContinuationIds =
                        Map.fold (fun acc k v -> Map.add k v acc) projection.AcceptedContinuationIds accepted
                      RepairClaims = Set.union projection.RepairClaims repair }

        projection
