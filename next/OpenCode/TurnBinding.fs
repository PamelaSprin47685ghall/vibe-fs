namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel

/// Root / physical / continuation bindings for a logical run.
///
/// No Host payload reading. Bindings are explicit in-memory facts or recovered
/// from the durable PromptAuthority projection.
///
/// `ContinuationMessageIds` stays a set of raw wire addresses: it is matched
/// against transcript addresses during reconcile, where role is not yet known.
module TurnBinding =

    let private canonicalRoleOf (role: Role) : AgentRole option =
        Wanxiangshu.Next.Session.AgentRoleIdentity.roleOfString (PromptAuthority.roleLabel role)

    /// Build an ActiveRunBinding from the journal PromptAuthority projection.
    ///
    /// Directory is not a journal fact; it must be supplied by the host when the
    /// run is registered.
    ///
    /// `ActiveLogicalRun` exists only after an Authority Root was accepted, so
    /// `Some run` already carries a real root. The old blank-string guard was a
    /// sentinel check that PROMPT-001's typed identity removes.
    ///
    /// Physical stays `None` when nothing is bound. It is not backfilled from the
    /// root, because PROMPT-002 makes promotion one-way and there is deliberately
    /// no `AuthorityRootUserMessageId -> PhysicalUserMessageId` inverse. Reconcile
    /// resolves the missing physical against the root's own wire address instead.
    let fromProjection
        (sessionId: SessionId)
        (projection: PromptAuthority.PromptAuthorityProjection)
        (userBindings: Dictionary<string, PhysicalUserMessageId>)
        (continuationIds: Set<string>)
        : ActiveRunBinding option =
        match projection.ActiveLogicalRun with
        | None -> None
        | Some run ->
            let physical =
                match userBindings.TryGetValue(SessionId.value sessionId) with
                | true, bound -> Some bound
                | false, _ -> None

            Some
                { SessionId = sessionId
                  RunId = None
                  AuthorityRootUserMessageId = Some run.AuthorityRootUserMessageId
                  PhysicalUserMessageId = physical
                  ContinuationMessageIds = continuationIds
                  AgentRole = canonicalRoleOf run.CanonicalRole
                  Directory = None }

    /// Mutable store for in-memory bindings. Durable recovery uses the journal.
    type Store() =
        let gate = obj ()
        let userMessageBindings = Dictionary<string, PhysicalUserMessageId>()
        let activeBindings = Dictionary<string, ActiveRunBinding>()
        let continuationMessageIds = Dictionary<string, Set<string>>()

        member _.UserMessageBindings = userMessageBindings

        /// Bind a new authority root (human or agent owner).
        ///
        /// The caller hands over the physical message that opened the run; the root
        /// is derived from it by PROMPT-002 promotion, never parsed separately.
        member _.BindUserMessage(sessionId: SessionId, physical: PhysicalUserMessageId, ?agentRole: AgentRole) =
            lock gate (fun () ->
                let key = SessionId.value sessionId
                let root = PhysicalUserMessageId.promoteToAuthorityRoot physical
                userMessageBindings.[key] <- physical

                match activeBindings.TryGetValue(key) with
                | true, binding ->
                    activeBindings.[key] <-
                        { binding with
                            AuthorityRootUserMessageId = Some root
                            PhysicalUserMessageId = Some physical
                            AgentRole = agentRole |> Option.orElse binding.AgentRole }
                | false, _ ->
                    activeBindings.[key] <-
                        { SessionId = sessionId
                          RunId = None
                          AuthorityRootUserMessageId = Some root
                          PhysicalUserMessageId = Some physical
                          ContinuationMessageIds = Set.empty
                          AgentRole = agentRole
                          Directory = None })

        /// Bind a continuation physical message to the active logical run.
        member _.BindContinuationUserMessage(sessionId: SessionId, physical: PhysicalUserMessageId) =
            lock gate (fun () ->
                let key = SessionId.value sessionId
                let address = PhysicalUserMessageId.value physical

                let continuations =
                    match continuationMessageIds.TryGetValue(key) with
                    | true, set -> set.Add address
                    | false, _ -> Set.singleton address

                continuationMessageIds.[key] <- continuations
                userMessageBindings.[key] <- physical

                match activeBindings.TryGetValue(key) with
                | true, binding ->
                    activeBindings.[key] <-
                        { binding with
                            PhysicalUserMessageId = Some physical
                            ContinuationMessageIds = continuations }
                | false, _ ->
                    activeBindings.[key] <-
                        { SessionId = sessionId
                          RunId = None
                          AuthorityRootUserMessageId = None
                          PhysicalUserMessageId = Some physical
                          ContinuationMessageIds = continuations
                          AgentRole = None
                          Directory = None })

        /// Register a host-provided active run (e.g. child session start).
        member _.BindActiveRun(binding: ActiveRunBinding) =
            lock gate (fun () ->
                let key = SessionId.value binding.SessionId

                let continuations =
                    match continuationMessageIds.TryGetValue(key) with
                    | true, set -> binding.ContinuationMessageIds + set
                    | false, _ -> binding.ContinuationMessageIds

                activeBindings.[key] <-
                    { binding with
                        ContinuationMessageIds = continuations })

        /// Resolve the best binding: explicit store first, then durable projection.
        /// The explicit host-provided role and directory are always preserved.
        member _.ActiveRunBinding(sessionId: SessionId, ?projection: AgentProjectionSet) : ActiveRunBinding option =
            lock gate (fun () ->
                let key = SessionId.value sessionId

                let fromExplicit () =
                    match activeBindings.TryGetValue(key) with
                    | true, binding when binding.AuthorityRootUserMessageId.IsSome -> Some binding
                    | true, binding when Option.isSome binding.PhysicalUserMessageId -> Some binding
                    | _ -> None

                let continuations =
                    match continuationMessageIds.TryGetValue(key) with
                    | true, set -> set
                    | false, _ -> Set.empty

                match projection with
                | None -> fromExplicit ()
                | Some proj ->
                    let projected =
                        match Map.tryFind sessionId proj.Sessions with
                        | None -> None
                        | Some session ->
                            match session.PromptAuthority with
                            | None -> None
                            | Some authority -> fromProjection sessionId authority userMessageBindings continuations

                    match fromExplicit (), projected with
                    | Some binding, Some p when binding.AuthorityRootUserMessageId.IsNone ->
                        Some
                            { binding with
                                AuthorityRootUserMessageId = p.AuthorityRootUserMessageId
                                PhysicalUserMessageId =
                                    (binding.PhysicalUserMessageId |> Option.orElse p.PhysicalUserMessageId)
                                ContinuationMessageIds = binding.ContinuationMessageIds + continuations }
                    | Some binding, _ ->
                        Some
                            { binding with
                                ContinuationMessageIds = binding.ContinuationMessageIds + continuations }
                    | None, Some p ->
                        Some
                            { p with
                                ContinuationMessageIds = p.ContinuationMessageIds + continuations }
                    | None, None -> None)

        /// Latest physical user message for the active logical run.
        member _.TryPhysicalUserMessage(sessionId: SessionId) : PhysicalUserMessageId option =
            lock gate (fun () ->
                match activeBindings.TryGetValue(SessionId.value sessionId) with
                | true, binding -> binding.PhysicalUserMessageId
                | false, _ ->
                    match userMessageBindings.TryGetValue(SessionId.value sessionId) with
                    | true, bound -> Some bound
                    | false, _ -> None)

        /// Remove all state for a session (session.deleted or explicit cleanup).
        member _.ClearSession(sessionId: SessionId) =
            lock gate (fun () ->
                let key = SessionId.value sessionId
                userMessageBindings.Remove(key) |> ignore
                activeBindings.Remove(key) |> ignore
                continuationMessageIds.Remove(key) |> ignore)
