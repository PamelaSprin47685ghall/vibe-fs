namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.Identity

module HostEventEnvelope =
    val unwrap: rawInput: obj -> obj
    val eventTypeOf: raw: obj -> string
    val trySessionId: raw: obj -> SessionId option
    val tryMessageSessionId: rawInput: obj -> SessionId option
