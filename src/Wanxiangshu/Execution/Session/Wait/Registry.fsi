namespace Wanxiangshu.Execution.Session.Wait

type CausalWaitRegistry =
    new: ?historyCapacity: int -> CausalWaitRegistry
    member HistoryCapacity: int
    interface IWaitObserver
    interface IWaitSnapshotReader

type CausalWaitRuntime =
    new: ?historyCapacity: int -> CausalWaitRuntime
    member BindDiagnosticTarget: target: IWaitDiagnosticSink -> bool
    member Observer: IWaitObserver
    member SnapshotReader: IWaitSnapshotReader
    member HistoryCapacity: int

module CausalWaitProcess =
    val local: unit -> CausalWaitRuntime
