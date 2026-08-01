namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic

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

    let SessionParents = Dictionary<string, string>()
    let VerdictSessions = HashSet<string>()
    let SessionDirectories = Dictionary<string, string>()
