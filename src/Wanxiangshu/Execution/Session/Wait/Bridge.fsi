namespace Wanxiangshu.Execution.Session.Wait

module CausalWaitBridge =
    val toPlainObject: reader: IWaitSnapshotReader -> obj
    val writeSnapshot: workspace: string -> reader: IWaitSnapshotReader -> unit
