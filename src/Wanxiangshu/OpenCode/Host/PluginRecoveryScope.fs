namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Execution.Session
open Wanxiangshu.Persistence.Journal

/// Family recovery coordination (PROMPT-011 + C5 + RECOVERY-FAMILY) and
/// attempt planning state for one plugin instance: recovery ports attachment,
/// per-session arming and per-provider-run attempt plans.
type PluginRecoveryScope(journal: AgentJournal option) =

    // DSL-MUTABLE: single-flight — per-session one-shot recovery arming latch
    // (HasFlight guard). ArmRecovery sets ArmedByAdvance after a non-fission-owner
    // turn failure; TryRecoveryArming checks the latch before planning a retry in
    // the next provider transform; ClearRecovery consumes it. One armed slot
    // produces at most one recovery attempt (FALLBACK-011). Process-local only:
    // RecoverySlot.afterRestart = NotArmed, so the latch intentionally forgets
    // arming across process boundaries — cross-process recovery is owned by
    // explicit /continue, not by automatic arming.
    //
    // PHYSICAL RESOURCE PROOF (not a ghost):
    //   Arming bridges two separate async entry points — turn observation
    //   (HostTurnObserver.observe → armRecoveryIfEligible, after a turn fails)
    //   and provider transform (XWire.applyTransform → applyNonReplicaTransform,
    //   before the next turn runs). There is no shared call chain to thread a
    //   CE closure through. It cannot be derived from durable facts:
    //   FallbackCursorAdvanced / FallbackExhausted track the cursor position and
    //   budget (durable, cross-process), but arming is deliberately NOT durable
    //   — RecoverySlot.fs: "Not persistent state, not a field on the cursor,
    //   never written to the journal. A local control-flow fact of one automatic
    //   recovery sequence." The type exists so the answer cannot be produced
    //   from a cursor alone (parked-cursor bug, FALLBACK-004). Single-flight:
    //   idempotent set (overwrites same value), consumed exactly once.
    member val RecoveryArming = Dictionary<string, SlotArming>()

    // DSL-MUTABLE: single-flight — per-provider-run attempt plan memo. Recorded
    // during the provider transform (planArmedWorkMainRetry / applyStrengthReplicaPlan
    // / RecordSquashPlan callback), consumed by reconcileAttempt when the turn
    // resolves (commitPromotablePrefixRebase on TurnCompleted, cleared on terminal),
    // and peeked read-only by StrengthSpeculate.planEvidence during the same
    // transform. One plan per (session, providerRun); cleared on terminal outcome
    // or session deletion.
    //
    // PHYSICAL RESOURCE PROOF (not a ghost):
    //   The plan bridges two separate async entry points — provider transform
    //   (PluginTransforms.fs → XWire.applyTransform, before the turn runs) and
    //   turn reconciliation (HostTurnObserver.observe → XWire.reconcileAttempt,
    //   after the turn completes). There is no shared call chain to thread a CE
    //   closure through. It cannot be derived from durable facts: the plan is a
    //   frozen decision at transform time (authority, cursor, projectionChoice,
    //   probe), and the projection state may advance between transform and
    //   reconciliation (new blog frames, cursor advances). Recomputing at
    //   reconciliation would yield a different plan, breaking
    //   PrefixRebaseCommitted promotion. Single-flight: one plan per provider
    //   run, consumed exactly once on terminal outcome.
    member val AttemptPlans = Dictionary<string, AttemptPlan>()

    /// Ordinary business entry never performs cross-process recovery. The permit
    /// only certifies this process's join attempt and intentionally carries no old
    /// durable closure members. Explicit session /continue owns future resume.
    member _.RequireFamilyRecovery(root: SessionId) : Task<FamilyRecovery> =
        let sequence =
            journal
            |> Option.map (AgentJournal.revision >> JournalRevision.value)
            |> Option.defaultValue 0L

        Task.FromResult(FamilyRecovery.FamilyReady(FamilyRecoveryPermit.currentProcess root sequence))

    member this.EnsureRecoveryDone(root: SessionId) : Task<FamilyRecovery> = this.RequireFamilyRecovery root

    member this.ArmRecovery(sessionId: SessionId) =
        this.RecoveryArming.[SessionId.value sessionId] <- RecoverySlot.afterFailureAdvance

    member this.TryRecoveryArming(sessionId: SessionId) =
        match this.RecoveryArming.TryGetValue(SessionId.value sessionId) with
        | true, arming -> Some arming
        | false, _ -> None

    member this.RecordAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) (plan: AttemptPlan) =
        this.AttemptPlans.[SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun] <- plan

    member this.TryAttemptPlan (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        let key =
            SessionId.value sessionId + "\u001f" + ProviderRunIdentity.value providerRun

        match this.AttemptPlans.TryGetValue(key) with
        | true, plan -> Some plan
        | false, _ -> None

    /// Session deletion drops arming and attempt plans for this session.
    member this.ClearSession(sessionId: string) =
        this.RecoveryArming.Remove sessionId |> ignore
        this.ClearAttemptPlansFor sessionId

    /// One-shot arming consumption (FALLBACK-012): drops arming only; attempt
    /// plans stay until the turn resolves them.
    member this.ClearRecovery(sessionId: string) =
        this.RecoveryArming.Remove sessionId |> ignore

    /// Drops attempt plans whose key prefix matches (used for a session and
    /// for its linked Blogger keys during session deletion).
    member this.ClearAttemptPlansFor(key: string) =
        this.AttemptPlans.Keys
        |> Seq.filter (fun planKey -> planKey.StartsWith(key + "\u001f", StringComparison.Ordinal))
        |> Seq.toList
        |> List.iter (fun planKey -> this.AttemptPlans.Remove planKey |> ignore)
