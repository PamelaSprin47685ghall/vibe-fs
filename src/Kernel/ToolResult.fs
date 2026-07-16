module Wanxiangshu.Kernel.ToolResult

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality

type ToolResult = { Text: string }

let wireEncodeToolError (context: string) (error: DomainError) : string =
    $"{context} failed: {formatDomainError error}"

let wireEncodeResult (result: Result<string, DomainError>) : string =
    match result with
    | Ok text -> text
    | Error err -> wireEncodeToolError "Tool" err
