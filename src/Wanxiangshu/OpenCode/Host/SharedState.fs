namespace Wanxiangshu.OpenCode

open System.Collections.Generic
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
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// HOST-012: 跨实例共享状态——模块级单例，所有插件实例同一引用。
///
/// Host 的 `InstanceStore` 按 directory 实例化插件（worktree = 独立 project =
/// 第二个插件实例），而 fork→verdict 因果链跨越实例边界（主实例的 reverify
/// fork 的 worktree 子会话由 worktree 实例的工具处理）。这些集合被这条链的
/// 两端读写，per-instance 时必然失配——实测 deep-reviewer 的 `VerdictTool`
/// 读不到主实例注册的 `SessionParents`，REVIEW-008 fail closed 拒绝 verdict；
/// worktree 上 SetCurrentRequest 的 blogger flight 读不到 root 实例 BlogTool 的
/// HasFlight，会 AbortSession → Finality journal-work-log hang；guard nudge
/// 若按 RuntimeId 分槽，root+worktree 会对同一 occasion 各发一次
/// ReviewerVerdictRequired。
///
/// 每实例独有（不得放进这里）：AgentJournal（独立 runtimeId 文件）、Companions、
/// OwnedSessions、UserMessageBindings、hook 订阅、每实例 NudgeSent（非 guard）。
module SharedState =

    /// REVIEW-010: a seal candidate before its provider run exists (see
    /// `ReviewSeal.bindToRun`). Defined here so the shared dictionary can
    /// be typed before `ReviewSeal` compiles.
    ///
    /// REVIEW-013: the attempt's review scope is frozen here at transform
    /// time — the manager/barrier/tree this request started under. The judge
    /// tool submits with this frozen scope, so a barrier opened later can
    /// never re-identify an old attempt.
    type PendingSeal =
        { SessionId: SessionId
          ManagerSessionId: SessionId
          BarrierId: ReviewBarrierId
          GitTreeHash: GitTreeHash
          PhysicalUserMessageId: PhysicalUserMessageId
          SealDigest: SealDigest
          CanonicalVersion: int
          IncludedToolResultDigests: SealDigest list }

    let SessionParents = Dictionary<string, string>()
    let VerdictSessions = HashSet<string>()
    let SessionDirectories = Dictionary<string, string>()

    /// REVIEW-003 / HOST-012: missing-verdict + confirm-perfect guard nudge
    /// reservations. Root and worktree plugin instances each own a journal
    /// (distinct RuntimeId) and a per-scope NudgeSent set; keying a reservation
    /// by RuntimeId made the "process-wide" lock miss its twin, so the same
    /// ReviewerVerdictRequired prose was always delivered twice. Session +
    /// continuation kind + occasion + reason — never RuntimeId.
    let ReviewGuardNudgeGate = obj ()
    let ReviewGuardNudges = HashSet<string>()

    /// Unit-test isolation only: production must not wipe cross-instance reservations.
    let clearReviewGuardNudgesForTests () =
        lock ReviewGuardNudgeGate (fun () -> ReviewGuardNudges.Clear())

    /// REVIEW-010 deferred-binding candidates (challenge requests), shared
    /// across instances like the other cross-instance state: the transform that
    /// parks a candidate and the tool that binds it may run under different
    /// plugin instances (orchestrator worktree).
    let PendingReviewSeals = Dictionary<string, PendingSeal>()

    /// The ROOT workspace, set by whichever plugin instance boots first (the
    /// main workspace loads before the manager worktrees). Worktree instances
    /// pin their blogger companions here so the blogger's system prompt
    /// (Host instruction loading from the session directory) survives the
    /// manager worktree release at publish.
    // DSL-MUTABLE: resource — process-local root workspace pin for worktree plugin instances
    let mutable RootWorkspace: string option = None

    /// Physical Blogger flight ownership (HasFlight / SetCurrentRequest).
    ///
    /// Same cross-instance rule as SessionParents: the worktree plugin materializes
    /// the companion request (SetCurrentRequest) while the blogger session itself
    /// lives under RootWorkspace, so BlogTool runs on the root plugin instance.
    /// Per-instance flights made HasFlight miss → AbortSession → no BlogObservationCommitted
    /// → Finality hung on journal-work-log (orchestrator-publish frontier).
    let BloggerFlightGate = obj ()
    let BloggerFlights = Dictionary<string, BloggerRequestContext>()

    /// Unit-test isolation only: production Dispose must not wipe cross-instance flights.
    let clearBloggerFlightsForTests () =
        lock BloggerFlightGate (fun () -> BloggerFlights.Clear())
