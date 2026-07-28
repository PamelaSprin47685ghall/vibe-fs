namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel

/// Root / physical / continuation bindings for a logical run.
/// No Host payload reading.  Bindings are explicit in-memory facts or
/// recovered from the durable PromptAuthority projection.
module TurnBinding =

    let private canonicalRoleOf (role: Role) : AgentRole option =
        Wanxiangshu.Next.Session.AgentRoleHelpers.roleOfString (PromptAuthority.roleLabel role)

    /// Build an ActiveRunBinding from the journal PromptAuthority projection.
    /// Directory is not a journal fact; it must be supplied by the host when the
    /// run is registered.
    let fromProjection
        (sessionId: SessionId)
        (projection: PromptAuthority.PromptAuthorityProjection)
        (userBindings: Dictionary<string, MessageId>)
        (continuationIds: Set<string>)
        : ActiveRunBinding option =
        match projection.ActiveLogicalRun with
        | None -> None
        | Some run when String.IsNullOrWhiteSpace(MessageId.value run.AuthorityRootUserMessageId) -> None
        | Some run ->
            let root = run.AuthorityRootUserMessageId

            let physical =
                match userBindings.TryGetValue(SessionId.value sessionId) with
                | true, mid -> mid
                | false, _ -> root

            Some
                { SessionId = sessionId
                  RunId = None
                  RootUserMessageId = Some root
                  PhysicalUserMessageId = Some physical
                  ContinuationMessageIds = continuationIds
                  AgentRole = canonicalRoleOf run.CanonicalRole
                  Directory = "" }

    /// Mutable store for in-memory bindings.  Durable recovery uses the journal.
    type Store() =
        let gate = obj ()
        let userMessageBindings = Dictionary<string, MessageId>()
        let activeBindings = Dictionary<string, ActiveRunBinding>()
        let continuationMessageIds = Dictionary<string, Set<string>>()

        member _.UserMessageBindings = userMessageBindings

        /// Bind a new authority root (human or agent owner).
        member _.BindUserMessage(sessionId: SessionId, userMessageId: MessageId, ?agentRole: AgentRole) =
            lock gate (fun () ->
                let key = SessionId.value sessionId
                userMessageBindings.[key] <- userMessageId

                match activeBindings.TryGetValue(key) with
                | true, binding ->
                    activeBindings.[key] <-
                        { binding with
                            RootUserMessageId = Some userMessageId
                            PhysicalUserMessageId = Some userMessageId
                            AgentRole = agentRole |> Option.orElse binding.AgentRole }
                | false, _ ->
                    activeBindings.[key] <-
                        { SessionId = sessionId
                          RunId = None
                          RootUserMessageId = Some userMessageId
                          PhysicalUserMessageId = Some userMessageId
                          ContinuationMessageIds = Set.empty
                          AgentRole = agentRole
                          Directory = "" })

        /// Bind a continuation physical message to the active logical run.
        member _.BindContinuationUserMessage(sessionId: SessionId, messageId: MessageId) =
            lock gate (fun () ->
                let key = SessionId.value sessionId

                let continuations =
                    match continuationMessageIds.TryGetValue(key) with
                    | true, set -> set.Add(MessageId.value messageId)
                    | false, _ -> Set.singleton (MessageId.value messageId)

                continuationMessageIds.[key] <- continuations

                match activeBindings.TryGetValue(key) with
                | true, binding ->
                    activeBindings.[key] <-
                        { binding with
                            PhysicalUserMessageId = Some messageId
                            ContinuationMessageIds = continuations }
                | false, _ ->
                    activeBindings.[key] <-
                        { SessionId = sessionId
                          RunId = None
                          RootUserMessageId = None
                          PhysicalUserMessageId = Some messageId
                          ContinuationMessageIds = continuations
                          AgentRole = None
                          Directory = "" })

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
                    | true, binding when binding.RootUserMessageId.IsSome -> Some binding
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
                    | Some binding, Some p when binding.RootUserMessageId.IsNone ->
                        Some
                            { binding with
                                RootUserMessageId = p.RootUserMessageId
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
        member _.TryPhysicalUserMessage(sessionId: SessionId) : MessageId option =
            lock gate (fun () ->
                match activeBindings.TryGetValue(SessionId.value sessionId) with
                | true, binding -> binding.PhysicalUserMessageId
                | false, _ ->
                    match userMessageBindings.TryGetValue(SessionId.value sessionId) with
                    | true, mid -> Some mid
                    | false, _ -> None)

        /// Remove all state for a session (session.deleted or explicit cleanup).
        member _.ClearSession(sessionId: SessionId) =
            lock gate (fun () ->
                let key = SessionId.value sessionId
                userMessageBindings.Remove(key) |> ignore
                activeBindings.Remove(key) |> ignore
                continuationMessageIds.Remove(key) |> ignore)
