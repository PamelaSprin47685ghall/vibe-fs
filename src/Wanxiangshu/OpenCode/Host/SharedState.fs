namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime

/// HOST-012: 跨实例共享状态——模块级单例，所有插件实例同一引用。
///
/// Host 的 `InstanceStore` 按 directory 实例化插件（worktree = 独立 project =
/// 第二个插件实例）。跨实例的 parent/directory registry 与 Blogger flight
/// 必须共享，否则 worktree 实例无法与 root 实例观察到同一物理会话家族。
///
/// 每实例独有（不得放进这里）：AgentJournal（独立 runtimeId 文件）、Companions、
/// OwnedSessions、UserMessageBindings、hook 订阅、每实例 NudgeSent（非 guard）。
module SharedState =

    // DSL-MUTABLE: resource — cross-instance session parent map
    let SessionParents = Dictionary<string, string>()
    // DSL-MUTABLE: resource — cross-instance session directory map
    let SessionDirectories = Dictionary<string, string>()


    /// Physical Blogger flight ownership (HasFlight / ClaimCurrentRequest).
    ///
    /// Same cross-instance rule as SessionParents: the worktree plugin materializes
    /// the companion request (ClaimCurrentRequest) while the blogger session itself
    /// lives under the root workspace, so BlogTool runs on the root plugin instance.
    /// Per-instance flights made HasFlight miss → AbortSession → no BlogObservationCommitted
    /// → Finality hung on journal-work-log (orchestrator-publish frontier).
    let BloggerFlightGate = obj ()
    // DSL-MUTABLE: resource — cross-instance blogger flight ownership registry
    let BloggerFlights = Dictionary<string, BloggerRequestContext>()
    // DSL-MUTABLE: resource — cross-instance per-Blogger materialization admission
    let BloggerMaterializationAdmission = BloggerMaterializationAdmission()

    /// Unit-test isolation only: production Dispose must not wipe cross-instance flights.
    let clearBloggerFlightsForTests () =
        lock BloggerFlightGate (fun () -> BloggerFlights.Clear())
