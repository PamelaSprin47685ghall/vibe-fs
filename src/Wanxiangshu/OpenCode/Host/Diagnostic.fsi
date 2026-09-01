namespace Wanxiangshu.OpenCode

open Wanxiangshu.OpenCode.Host

module Diagnostic =
    val AllowedFields: Set<string>
    val emit: operation: string -> fields: (string * string) list -> unit
    val emitCausal: record: CausalDiagnosticRecord -> unit
    val fatal: operation: string -> fields: (string * string) list -> unit
