module VibeFs.Shell.FileToolsCodec

open Fable.Core.JsInterop
open VibeFs.Kernel.Domain
open VibeFs.Shell.Dyn
open VibeFs.Shell.DynField

type ReadArgs = {
    Path: string
    Offset: int option
    Limit: int option
}

type WriteArgs = {
    FilePath: string
    Content: string
}

let decodeReadArgs (args: obj) : Result<ReadArgs, DomainError> =
    match strField args "path" with
    | None -> Error (InvalidIntent ("read", "path", "read path required"))
    | Some path when System.String.IsNullOrWhiteSpace path ->
        Error (InvalidIntent ("read", "path", "read path required"))
    | Some path ->
        Ok {
            Path = path
            Offset = optInt args "offset"
            Limit = optInt args "limit"
        }

let readArgsForHost (decoded: ReadArgs) : obj =
    let fields = ResizeArray [| "path", box decoded.Path |]
    decoded.Offset |> Option.iter (fun o -> fields.Add("offset", box o))
    decoded.Limit |> Option.iter (fun l -> fields.Add("limit", box l))
    createObj (fields.ToArray())

let decodeWriteArgs (args: obj) : Result<WriteArgs, DomainError> =
    if not (hasField args "file_path") then
        Error (InvalidIntent ("write", "file_path", "missing required parameter"))
    elif not (hasField args "content") then
        Error (InvalidIntent ("write", "content", "missing required parameter"))
    else
        let filePath = defaultArg (strField args "file_path") ""
        let content = defaultArg (strField args "content") ""
        if System.String.IsNullOrWhiteSpace filePath then
            Error (InvalidIntent ("write", "file_path", "must not be empty"))
        else
            Ok { FilePath = filePath; Content = content }