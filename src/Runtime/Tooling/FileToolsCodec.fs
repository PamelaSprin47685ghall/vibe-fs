module Wanxiangshu.Runtime.FileToolsCodec

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.DynField

type ReadArgs =
    { Path: string
      Offset: int option
      Limit: int option }

type WriteArgs = { FilePath: string; Content: string }

let decodeReadArgs (args: obj) : Result<ReadArgs, DomainError> =
    let path = strField args "path" |> Option.orElse (strField args "filePath")

    match path with
    | None -> Error(InvalidIntent("read", "path", "read path required"))
    | Some path when System.String.IsNullOrWhiteSpace path -> Error(InvalidIntent("read", "path", "read path required"))
    | Some path ->
        Ok
            { Path = path
              Offset = optInt args "offset"
              Limit = optInt args "limit" }

let readArgsForHost (decoded: ReadArgs) : obj =
    let fields = ResizeArray [| "path", box decoded.Path |]
    decoded.Offset |> Option.iter (fun o -> fields.Add("offset", box o))
    decoded.Limit |> Option.iter (fun l -> fields.Add("limit", box l))
    createObj (fields.ToArray())

let decodeWriteArgs (args: obj) : Result<WriteArgs, DomainError> =
    let filePathOpt = strField args "file_path" |> Option.orElse (strField args "filePath")

    if filePathOpt.IsNone then
        Error(InvalidIntent("write", "file_path", "missing required parameter"))
    elif not (hasField args "content") then
        Error(InvalidIntent("write", "content", "missing required parameter"))
    else
        let filePath = defaultArg filePathOpt ""
        let content = defaultArg (strField args "content") ""

        if System.String.IsNullOrWhiteSpace filePath then
            Error(InvalidIntent("write", "file_path", "must not be empty"))
        else
            Ok
                { FilePath = filePath
                  Content = content }
