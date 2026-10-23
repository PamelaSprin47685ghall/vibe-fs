namespace Wanxiangshu.Mission.Planning

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host

module PlanPath =

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readFileSync (path: string, encoding: string) : string = jsNative

    let validateWorkId (workId: string) : Result<string, string> =
        if String.IsNullOrWhiteSpace workId then
            Error "Work ID cannot be empty or whitespace"
        elif
            workId.Contains("..")
            || workId.Contains("/")
            || workId.Contains("\\")
            || workId.Contains("\u0000")
            || workId.IndexOf('\u0000') >= 0
        then
            Error "Insecure work ID: path traversal or escape characters detected"
        elif workId.StartsWith(".") then
            Error "Insecure work ID: leading dot detected"
        else
            Ok workId

    let canonicalWorkKey (workId: string) : Result<string, string> = validateWorkId workId

    let private verifyPathPrefix (prefix: string) (fullPath: string) : Result<string, string> =
        if fullPath.StartsWith prefix then
            Ok fullPath
        else
            Error "Path traversal detected: resolved path escapes plan directory"

    let private buildPlanPath (root: string) (key: string) : Result<string, string> =
        if String.IsNullOrWhiteSpace root then
            Error "Root path cannot be empty"
        else
            let normalizedRoot = root.Replace('\\', '/').TrimEnd('/')
            let fullPath = normalizedRoot + "/plan/" + key + "/plan.md"
            let expectedPrefix = normalizedRoot + "/plan/" + key + "/"
            verifyPathPrefix expectedPrefix fullPath

    let resolvePlanPath (root: string) (workId: string) : Result<string, string> =
        canonicalWorkKey workId |> Result.bind (buildPlanPath root)

    let private safeRead (path: string) : Result<string, string> =
        try
            Ok(readFileSync (path, "utf8"))
        with ex ->
            Error(sprintf "Failed to read plan file: %s" ex.Message)

    let private readPlanFile (path: string) : Result<string, string> =
        if not (existsSync path) then
            Error(sprintf "Plan file not found: %s" path)
        else
            safeRead path

    let readPlan (root: string) (workId: string) : Result<string, string> =
        resolvePlanPath root workId |> Result.bind readPlanFile
