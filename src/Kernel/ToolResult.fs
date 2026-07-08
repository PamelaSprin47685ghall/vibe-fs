module Wanxiangshu.Kernel.ToolResult

open Wanxiangshu.Kernel.Domain

type ToolResult = { Text: string }

let wireEncodeToolError (context: string) (error: DomainError) : string =
    $"{context} failed: {formatDomainError error}"

let wireEncodeResult (result: Result<string, DomainError>) : string =
    match result with
    | Ok text -> text
    | Error err -> wireEncodeToolError "Tool" err
