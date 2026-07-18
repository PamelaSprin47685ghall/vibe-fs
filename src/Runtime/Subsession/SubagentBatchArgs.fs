module Wanxiangshu.Runtime.SubagentBatchArgs

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.SubagentIntentsCodec

let private validateCoderIntents (toolName: string) (intents: obj) : Result<CoderIntent list, DomainError> =
    match parseCoderIntents intents with
    | Error msg -> Error(ParseError("intents", msg))
    | Ok [] -> Error(InvalidIntent(toolName, "intents", "must be a non-empty array"))
    | Ok intents -> Ok intents

let validateCoderBatchArgs (toolName: string) (args: obj) : Result<CoderIntent list, DomainError> =
    let raw = intentsRawFromArgs args
    validateCoderIntents toolName raw

let validateInvestigatorBatchArgs (toolName: string) (args: obj) : Result<InvestigatorIntent list, DomainError> =
    let raw = intentsRawFromArgs args

    match parseInvestigatorIntents raw with
    | Error msg -> Error(ParseError("intents", msg))
    | Ok [] -> Error(InvalidIntent(toolName, "intents", "must be a non-empty array"))
    | Ok intents -> Ok intents
