module VibeFs.Shell.OpencodeSessionSpawnCodec

open VibeFs.Kernel.Domain
open VibeFs.Shell.Dyn
open VibeFs.Shell.DynField

let decodeChildSessionIdFromCreateResult (createResult: obj) : Result<string, DomainError> =
    if Dyn.isNullish createResult then
        Error (InvalidIntent ("session", "id", "missing"))
    else
        let data = Dyn.get createResult "data"
        if Dyn.isNullish data then
            Error (InvalidIntent ("session", "id", "missing"))
        else
            match strField data "id" with
            | None -> Error (InvalidIntent ("session", "id", "missing"))
            | Some id ->
                let trimmed = id.Trim()
                if trimmed = "" then Error (InvalidIntent ("session", "id", "missing"))
                else Ok trimmed