namespace Wanxiangshu.Sphinx.V2.Hosts.OpenCode

open System.Threading.Tasks

/// Adapter 能力的 JS 原生只读投影；不读取会话、不取得 receipt、不暴露 Adapter 对象。
module Surface =
    /// 调用真实 OpenCodeHostPort.Capabilities；sessions 仅用于构造，不会在此查询中调用。
    val capabilities: unit -> string array

    val createDispatchProbe: directory: string -> owners: obj -> owner: string -> Task<obj>
    val startDispatch: probe: obj -> _inquiry: string -> request: obj -> obj
    val dispatchAdmission: execution: obj -> Task<obj>
    val dispatchCompletion: execution: obj -> Task<obj>
    val hostRecording: probe: obj -> obj
    val awaitHostPrompts: probe: obj -> count: int -> Task
    val returnHostOutcome: probe: obj -> session: string -> index: int -> outcome: obj -> bool
    val disposeDispatchProbe: probe: obj -> unit
    val confirmHostPhysical: probe: obj -> index: int -> physical: string -> Task<obj>

    val settleHostTerminal:
        probe: obj ->
        session: string ->
        physical: string ->
        root: string ->
        providerRun: string ->
        text: string ->
            Task<bool>
