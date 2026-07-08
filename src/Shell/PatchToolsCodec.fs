module Wanxiangshu.Shell.PatchToolsCodec

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.DynField

type ApplyPatchFields = { PatchText: string }

let decodeApplyPatchFields (args: obj) : Result<ApplyPatchFields, DomainError> =
    if Dyn.typeIs args "string" then
        let s = string args

        if System.String.IsNullOrWhiteSpace s then
            Error(InvalidIntent("apply_patch", "patchText", "required"))
        else
            Ok { PatchText = s }
    else
        let patchText =
            [ "patchText"; "patch"; "text" ]
            |> List.tryPick (fun k -> strField args k |> Option.filter (fun s -> s <> ""))

        match patchText with
        | None -> Error(InvalidIntent("apply_patch", "patchText", "required"))
        | Some text -> Ok { PatchText = text }
