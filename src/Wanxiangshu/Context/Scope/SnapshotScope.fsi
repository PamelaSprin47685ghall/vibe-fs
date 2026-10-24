namespace Wanxiangshu.Context.Scope

open Wanxiangshu.Foundation.HostSession

/// 只做持久化会话快照到 HNI 中立 scope 视图的编解码。
module SnapshotScope =
    val filterSnapshot: messages: SessionMessage list -> obj array
