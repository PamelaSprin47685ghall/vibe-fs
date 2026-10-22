namespace Wanxiangshu.OpenCode

open Wanxiangshu.Host.Ports

/// HNI P-12: the OpenCode adapter for the neutral diagnostics sink.
///
/// The host call is `client.app.log` — the OpenCode SDK verb that writes a
/// log entry to the server logs (`POST /log`). The neutral port carries no
/// severity and no error channel: every diagnostic is written at `info`, and
/// a failed log write is dropped rather than thrown into product code.
module DiagnosticsPort =

    /// The sink over the OpenCode SDK client (`client.app.log`).
    type SdkDiagnosticSink =
        new: client: obj -> SdkDiagnosticSink
        interface IDiagnosticSink

    /// The sink for the given host wiring, or `None` when the wiring names no
    /// host that exposes a log verb.
    val create: input: obj -> IDiagnosticSink option
