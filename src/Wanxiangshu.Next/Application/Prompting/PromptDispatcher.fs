namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Host
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

[<RequireQualifiedAccess>]
module PromptDispatcher =

    let internal originLabel = PromptAuthority.originLabel

    /// PROMPT-007: whether the caller waits for PhysicalAccepted.
    ///
    /// Detached = fire-and-forget: claim, authority, persist, idempotence and error
    /// recording still run; the caller does not require a physical message id.
    /// Await = same send path; reserved for callers that bind an acceptance callback.
    [<RequireQualifiedAccess>]
    type AwaitMode =
        | Await
        | Detached

    /// The single PROMPT-005 sender.
    ///
    /// Holds no authority state. The previous version kept a `mutable authority`
    /// behind a lock and seeded it by folding *every* session's projection into
    /// one value, which had two consequences worth naming: a claim made in one
    /// session was visible in another, and the in-memory copy could disagree with
    /// the journal it was supposed to mirror. Both are gone because the state is
    /// gone — every read goes to the fold, which is the only writer.
    ///
    /// The journal is not optional. A dispatcher with nowhere to persist would
    /// report `Ok` for facts it silently dropped, and PROMPT-005 is a durability
    /// claim before it is a sequencing one.
    type Runtime(journal: AgentJournal) =

        member _.RuntimeId = AgentJournal.runtimeId journal

        /// PERSIST-008: one session's authority projection, addressed by key.
        member _.ProjectionFor(sessionId: SessionId) : PromptAuthority.PromptAuthorityProjection =
            AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
            |> Option.bind (fun session -> session.PromptAuthority)
            |> Option.defaultValue PromptAuthority.empty

        member internal _.Persist
            (sessionId: SessionId)
            (providerRun: ProviderRunIdentity option)
            (fact: AgentFact)
            : Result<unit, string> =
            AgentJournal.appendAgent (StreamId.Session sessionId) providerRun fact journal
            |> Result.map (fun _ -> ())
            |> Result.mapError (fun failure -> JournalAppendFailure.describe failure)

        /// PROMPT-004: an Authority Root takes effect.
        ///
        /// Returns `Result` rather than raising. The previous version raised
        /// `InvalidOperationException` on a persist failure, which turned a
        /// recoverable journal rejection into a crash in whichever host callback
        /// happened to be on the stack.
        ///
        /// REVIEW-007's review requirement is not written here. The fold derives
        /// it from this fact's `AuthorityKind`, so a HumanRoot cannot be recorded
        /// without its requirement appearing with it.
        member this.RegisterAuthority(profile: PromptAuthority.AuthorityExecutionProfile) : Result<unit, string> =
            AgentFact.AuthorityRootAccepted
                {| SessionId = profile.SessionId
                   LogicalRunId = profile.LogicalRunId
                   AuthorityRootUserMessageId = profile.AuthorityRootUserMessageId
                   AuthorityKind =
                    match profile.AuthorityKind with
                    | PromptAuthority.RootAuthorityKind.AgentOwnerRoot -> "AgentOwnerRoot"
                    | PromptAuthority.RootAuthorityKind.HumanRoot -> "HumanRoot"
                   SelectedAgent = profile.SelectedAgent
                   PeerAgent = profile.PeerAgent
                   CanonicalRole = PromptAuthority.roleLabel profile.CanonicalRole
                   SelectedTier = PromptAuthority.tierLabel profile.SelectedTier |}
            |> this.Persist profile.SessionId None

        /// PROMPT-002: a human root must name a managed agent. There is no
        /// default, because inferring one is how a human prompt silently acquires
        /// an agent nobody chose.
        member this.AcceptHumanRoot
            (sessionId: SessionId)
            (physicalMessageId: PhysicalUserMessageId)
            (explicitAgent: string option)
            : Result<PromptAuthority.AuthorityExecutionProfile, string> =
            match explicitAgent with
            | None -> Error "HumanRoot requires an explicit managed agent (fast-* / deep-*)"
            | Some agent ->
                PromptAuthorityRun.createAuthorityRoot
                    HostDigest.sha256Hex
                    this.RuntimeId
                    sessionId
                    PromptAuthority.RootAuthorityKind.HumanRoot
                    physicalMessageId
                    agent
                |> Result.bind (fun profile -> this.RegisterAuthority profile |> Result.map (fun () -> profile))

        /// PROMPT-005 `Abandoned`.
        ///
        /// Public because PROMPT-011's recovery is the second legitimate caller: a
        /// claim whose budget expired must be abandoned with
        /// `UnresolvedAfterRecovery`, and that happens at startup rather than inside
        /// a send. The send path's own abandon (`SendFailed`) stays private to it.
        member this.Abandon
            (key: PromptKey)
            (sessionId: SessionId)
            (reason: PromptAbandonReason)
            : Result<unit, string> =
            AgentFact.PluginPromptAbandoned
                {| PromptKey = key
                   SessionId = sessionId
                   Reason = reason |}
            |> this.Persist sessionId None

        /// PROMPT-005 `PhysicalAccepted` for an Authority Root claim.
        ///
        /// Two facts in order: the claim resolves, then the root takes effect. The
        /// order is the clause — an Authority Root may not take effect until a
        /// real physical message is proven, so `PhysicalAccepted` cannot come
        /// second.
        member internal this.AcceptPhysicalAgentOwnerRoot
            (key: PromptKey)
            (sessionId: SessionId)
            (physicalMessageId: PhysicalUserMessageId)
            (agent: string)
            : Result<PromptAuthority.AuthorityExecutionProfile, string> =
            PromptAuthorityRun.createAuthorityRoot
                HostDigest.sha256Hex
                this.RuntimeId
                sessionId
                PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                physicalMessageId
                agent
            |> Result.bind (fun profile ->
                AgentFact.PluginPromptPhysicalAccepted
                    {| PromptKey = key
                       SessionId = sessionId
                       PhysicalUserMessageId = physicalMessageId |}
                |> this.Persist sessionId None
                |> Result.bind (fun () -> this.RegisterAuthority profile)
                |> Result.map (fun () -> profile))

        member this.AcceptAgentOwnerRoot
            (key: PromptKey)
            (sessionId: SessionId)
            (physicalMessageId: PhysicalUserMessageId)
            : Result<PromptAuthority.AuthorityExecutionProfile, string> =
            let projection = this.ProjectionFor sessionId

            match Map.tryFind key projection.PendingClaims with
            | Some claim when
                claim.Origin = PromptAuthority.PromptOrigin.AuthorityRoot
                                   PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                ->
                match claim.EffectiveAgent with
                | None -> Error(sprintf "AgentOwnerRoot claim %s carries no effective agent" (PromptKey.value key))
                | Some agent -> this.AcceptPhysicalAgentOwnerRoot key sessionId physicalMessageId agent
            | Some _ -> Error(sprintf "PromptKey %s is not a pending AgentOwnerRoot" (PromptKey.value key))
            | None ->
                // Re-delivery of a message whose claim already resolved. Idempotent
                // only when the run it created is still the active one; otherwise
                // this is an unknown key and must fail closed (PROMPT-005).
                match projection.ActiveLogicalRun with
                | Some profile -> Ok profile
                | None -> Error(sprintf "Unknown AgentOwnerRoot claim: %s" (PromptKey.value key))

        /// PROMPT-003: a continuation reached physical acceptance. Returns the
        /// kind it was claimed as, read before the fact is written because writing
        /// it retires the claim.
        member this.AcceptContinuation
            (key: PromptKey)
            (sessionId: SessionId)
            (physicalMessageId: PhysicalUserMessageId)
            : Result<PromptAuthority.ContinuationKind option, string> =
            let kind =
                match Map.tryFind key (this.ProjectionFor sessionId).PendingClaims with
                | Some { Origin = PromptAuthority.PromptOrigin.Continuation c } -> Some c
                | _ -> None

            AgentFact.PluginPromptPhysicalAccepted
                {| PromptKey = key
                   SessionId = sessionId
                   PhysicalUserMessageId = physicalMessageId |}
            |> this.Persist sessionId None
            |> Result.map (fun () -> kind)

        /// The run a continuation would extend.
        ///
        /// `ActiveLogicalRun` only. The previous version fell back to
        /// `LastAuthorityProfile`, which let a continuation attach to a finished
        /// run — PROMPT-004 scopes continuations to the active run, and a stale
        /// profile is exactly the thing that must not substitute for one.
        member this.ActiveProfile(sessionId: SessionId) =
            (this.ProjectionFor sessionId).ActiveLogicalRun

        member this.ResolveOrigin
            (physicalMessageId: PhysicalUserMessageId)
            (promptKey: PromptKey option)
            (hostCompaction: bool)
            (sessionId: SessionId)
            : PromptAuthority.PromptOrigin =
            PromptAuthorityRun.resolveKnownOrigin
                physicalMessageId
                promptKey
                hostCompaction
                (this.ProjectionFor sessionId)

        /// FALLBACK-008: has this occasion already spent its one interaction repair.
        ///
        /// A read, not a claim. The previous `TryClaimInteractionRepair` mutated a
        /// `RepairClaims` set that no fact ever wrote, so the at-most-once guarantee
        /// lived only in process memory. The budget is now derived from
        /// `ClaimSequences`, which PROMPT-005 `Claimed` does write — so a repair
        /// claimed before a crash is still spent after it.
        member this.RepairAlreadyClaimed
            (profile: PromptAuthority.AuthorityExecutionProfile)
            (terminalProviderRun: ProviderRunIdentity)
            (repairKind: string)
            : bool =
            PromptAuthority.repairAlreadyClaimed
                profile.SessionId
                profile.LogicalRunId
                terminalProviderRun
                repairKind
                (this.ProjectionFor profile.SessionId)

        member internal _.Metadata (key: PromptKey) (origin: string) (logicalRunId: LogicalRunId option) =
            PromptMetadataCodec.create key origin logicalRunId

        /// EXEC-003 requires a terminal listener to exist before a prompt is sent.
        /// This registers the subscription without reacting to it; the reacting
        /// listener belongs to whoever awaits the agent.
        member internal _.SubscribeNoOp (port: ISessionHostPort) (sessionId: SessionId) =
            port.SubscribeTerminal(sessionId, (fun _ _ -> ()))

    let forJournal (journal: AgentJournal) = Runtime(journal)
