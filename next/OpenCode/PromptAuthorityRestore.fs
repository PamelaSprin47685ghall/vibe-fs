namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

module PromptAuthorityRestore =

    let private toProfile (sessionId: SessionId) (p: AuthorityProfileProjection) : PromptAuthority.AuthorityExecutionProfile =
        let model =
            match p.BaseProviderID, p.BaseModelID with
            | Some providerID, Some modelID ->
                Some
                    { providerID = providerID
                      modelID = modelID
                      variant = p.Variant }
            | _ -> None

        let kind =
            match p.AuthorityKind with
            | "AgentOwnerRoot" -> PromptAuthority.AgentOwnerRoot
            | _ -> PromptAuthority.HumanRoot

        { SessionId = sessionId
          LogicalRunId = p.LogicalRunId
          AuthorityRootUserMessageId = MessageId.create p.AuthorityRootUserMessageId
          AuthorityKind = kind
          Agent = p.Agent
          BaseModel = model
          Variant = p.Variant }

    /// Rebuild the in-memory authority projection from durable journal facts.
    /// Pending claims only reconstruct continuation kinds that still have an active run.
    let fromJournal (journal: AgentJournal) : PromptAuthority.PromptAuthorityProjection =
        let mutable projection = PromptAuthority.empty

        for KeyValue(sessionId, session) in (AgentJournal.snapshot journal).AgentProjections.Sessions do
            match session.PromptAuthority with
            | None -> ()
            | Some durable ->
                let last = durable.LastAuthorityProfile |> Option.map (toProfile sessionId)
                let active = durable.ActiveLogicalRun |> Option.map (toProfile sessionId)

                let pending =
                    durable.PendingClaims
                    |> Map.toList
                    |> List.choose (fun (promptKey, kindLabel) ->
                        match PromptAuthority.tryParseContinuationKind kindLabel, active with
                        | Some kind, Some profile ->
                            let key = PromptKeyRef.create promptKey

                            Some(
                                key,
                                PromptAuthority.claimContinuation
                                    key
                                    sessionId
                                    kind
                                    profile
                                    profile.BaseModel
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

                // Last session wins for single-runtime restore of the shared service cache.
                // Multi-session values still live in journal folds; ActiveProfile filters by session.
                projection <-
                    { LastAuthorityProfile = last |> Option.orElse projection.LastAuthorityProfile
                      ActiveLogicalRun = active |> Option.orElse projection.ActiveLogicalRun
                      PendingClaims = Map.fold (fun acc k v -> Map.add k v acc) projection.PendingClaims pending
                      AcceptedContinuationIds =
                        Map.fold (fun acc k v -> Map.add k v acc) projection.AcceptedContinuationIds accepted
                      RepairClaims = Set.union projection.RepairClaims repair }

        projection
