namespace Wanxiangshu.Sphinx.V2.Hosts.OpenCode

open Wanxiangshu.OpenCode

/// Adapter 能力的 JS 原生只读投影；不读取会话、不取得 receipt、不暴露 Adapter 对象。
module Surface =
    /// 调用真实 OpenCodeHostPort.Capabilities；sessions 仅用于构造，不会在此查询中调用。
    val capabilities: sessions: ISessionHostPort -> string array
