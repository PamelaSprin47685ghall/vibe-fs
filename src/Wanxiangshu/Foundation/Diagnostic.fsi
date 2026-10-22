namespace Wanxiangshu.Foundation

module Diagnostic =
    type Sink = string -> (string * string) list -> unit
    val installSink: sink: Sink -> unit
    val clearSink: unit -> unit
    val validateOperation: operation: string -> bool
    val diagnosticsVisible: unit -> bool
    val redactText: value: string -> string
    val emit: operation: string -> fields: (string * string) list -> unit
    val fatal: operation: string -> fields: (string * string) list -> unit
