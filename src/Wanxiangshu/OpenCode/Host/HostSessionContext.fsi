namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation

module HostSessionContext =
    val roleOf: agent: string -> Role option
    val read: raw: obj -> string * string option
