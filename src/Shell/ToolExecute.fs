module Wanxiangshu.Shell.ToolExecute

open Fable.Core
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Shell.ToolArgsDecode

/// Single-line tool output heuristic shared by Opencode/Mux tool.execute.after hooks.
let isNetworkErrorText (text: string) : bool =
    if System.String.IsNullOrWhiteSpace text then false
    elif text.Contains("\n") then false
    else
        let lower = text.ToLowerInvariant()
        lower.Contains("error") && lower.Contains("network")

let wireDecodeFailure (toolName: string) (error: DomainError) : string =
    wireEncodeToolError toolName error

let wireDomainFailure (context: string) (error: DomainError) : string =
    wireEncodeToolError context error

let mapDecodeError (toolName: string) (result: Result<'T, DomainError>) : Result<'T, string> =
    result |> Result.mapError (wireDecodeFailure toolName)

let runDecodedToWire
    (toolName: string)
    (decoded: Result<DecodedToolInvocation, DomainError>)
    (onTyped: ToolArgs -> JS.Promise<string>)
    (onCoder: CoderIntent list -> JS.Promise<string>)
    (onInvestigator: InvestigatorIntent list -> JS.Promise<string>)
    : JS.Promise<string> =
    promise {
        match decoded with
        | Error err -> return wireDecodeFailure toolName err
        | Ok (Typed ta) -> return! onTyped ta
        | Ok (CoderBatch intents) -> return! onCoder intents
        | Ok (InvestigatorBatch intents) -> return! onInvestigator intents
    }