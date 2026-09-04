namespace Wanxiangshu.Execution.Session.Wait

module CausalWaitBridge =
    val target: workspace: string -> IWaitDiagnosticSink
    val toPlainObject: snapshot: DiagnosticWaitSnapshot -> obj
    val writeSnapshot: workspace: string -> snapshot: DiagnosticWaitSnapshot -> unit
