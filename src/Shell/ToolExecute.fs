module VibeFs.Shell.ToolExecute

open Fable.Core
open VibeFs.Kernel.Domain
open VibeFs.Kernel.SubagentIntents
open VibeFs.Kernel.ToolArgs
open VibeFs.Kernel.ToolResult
open VibeFs.Shell.ToolArgsDecode

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