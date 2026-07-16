module Wanxiangshu.Runtime.OpencodeSessionSpawnCodec

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.DynField

let decodeChildSessionIdFromCreateResult (createResult: obj) : Result<string, DomainError> =
    if Dyn.isNullish createResult then
        Error(InvalidIntent("session", "id", "missing"))
    else
        let data = Dyn.get createResult "data"

        if Dyn.isNullish data then
            Error(InvalidIntent("session", "id", "missing"))
        else
            match strField data "id" with
            | None -> Error(InvalidIntent("session", "id", "missing"))
            | Some id ->
                let trimmed = id.Trim()

                if trimmed = "" then
                    Error(InvalidIntent("session", "id", "missing"))
                else
                    Ok trimmed
