namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity

/// HOST-012: 跨实例共享状态——模块级单例，所有插件实例同一引用。
///
/// Host 的 `InstanceStore` 按 directory 实例化插件（worktree = 独立 project =
/// 第二个插件实例），而 fork→verdict 因果链跨越实例边界（主实例的 reverify
/// fork 的 worktree 子会话由 worktree 实例的工具处理）。这三个集合被这条链的
/// 两端读写，per-instance 时必然失配——实测 deep-reviewer 的 `VerdictTool`
/// 读不到主实例注册的 `SessionParents`，REVIEW-008 fail closed 拒绝 verdict。
///
/// 每实例独有（不得放进这里）：AgentJournal（独立 runtimeId 文件）、Companions、
/// OwnedSessions、UserMessageBindings、hook 订阅。
module SharedState =

    /// REVIEW-010: a seal candidate before its provider run exists (see
    /// `ReviewSeal.bindToRun`). Defined here so the shared dictionary can
    /// be typed before `ReviewSeal` compiles.
    type PendingSeal =
        { SessionId: SessionId
          PhysicalUserMessageId: PhysicalUserMessageId
          SealDigest: SealDigest
          CanonicalVersion: int
          IncludedToolResultDigests: SealDigest list }

    let SessionParents = Dictionary<string, string>()
    let VerdictSessions = HashSet<string>()
    let SessionDirectories = Dictionary<string, string>()

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
    let mutable RootWorkspace: string option = None
