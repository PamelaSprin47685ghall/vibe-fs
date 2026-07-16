module Wanxiangshu.Runtime.OpencodeClientCodec

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.Dyn

let getClientFromPluginCtx (ctx: obj) : Result<obj, DomainError> =
    let client = Dyn.get ctx "client"

    if Dyn.isNullish client then
        Error(InvalidIntent("plugin", "client", "missing"))
    else
        Ok client

let getSessionApiFromClient (client: obj) : Result<obj, DomainError> =
    let session = Dyn.get client "session"

    if Dyn.isNullish session then
        Error(InvalidIntent("plugin", "session", "missing"))
    else
        Ok session

let getConfigApiFromClient (client: obj) : Result<obj, DomainError> =
    let config = Dyn.get client "config"

    if Dyn.isNullish config then
        Error(InvalidIntent("plugin", "config", "missing"))
    else
        Ok config
