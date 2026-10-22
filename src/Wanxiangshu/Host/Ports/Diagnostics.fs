namespace Wanxiangshu.Host.Ports

/// HNI P-12: structured diagnostics sink supplied by the host.
type IDiagnosticSink =
    abstract Emit: operation: string * fields: (string * string) list -> unit
