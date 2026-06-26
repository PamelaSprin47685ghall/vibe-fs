module Wanxiangshu.Shell.OpencodeClientCodec

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.Dyn

let getClientFromPluginCtx (ctx: obj) : Result<obj, DomainError> =
    let client = Dyn.get ctx "client"
    if Dyn.isNullish client then Error (InvalidIntent ("plugin", "client", "missing"))
    else Ok client

let getSessionApiFromClient (client: obj) : Result<obj, DomainError> =
    let session = Dyn.get client "session"
    if Dyn.isNullish session then Error (InvalidIntent ("plugin", "session", "missing"))
    else Ok session