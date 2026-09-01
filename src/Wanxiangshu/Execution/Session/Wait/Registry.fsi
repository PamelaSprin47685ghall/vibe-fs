namespace Wanxiangshu.Execution.Session.Wait

type CausalWaitRegistry =
    new: ?historyCapacity: int -> CausalWaitRegistry
    member HistoryCapacity: int
    interface IWaitObserver
    interface IWaitSnapshotReader

module CausalWaitHub =
    val reader: IWaitSnapshotReader
    val observer: IWaitObserver
    val snapshot: unit -> DiagnosticWaitSnapshot
    val frontiers: unit -> CausalFrontier list
    val setWorkspace: directory: string option -> unit
    val writeToWorkspace: unit -> unit
