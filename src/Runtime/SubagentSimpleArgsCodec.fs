module Wanxiangshu.Runtime.SubagentSimpleArgsCodec

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.Dyn

type BrowserArgs = { Intent: string }

type ContinueArgs = { Iterator: string; Prompt: string }

let decodeIntentField (toolName: string) (fieldName: string) (args: obj) : Result<string, DomainError> =
    let v = Dyn.get args fieldName

    if Dyn.isNullish v then
        Error(InvalidIntent(toolName, fieldName, "must be a string"))
    else
        Ok(string v)

let private strArrayField (a: obj) (k: string) : string array =
    let v = Dyn.get a k

    if Dyn.isNullish v then
        [||]
    elif Dyn.isArray v then
        (v :?> obj array) |> Array.map string
    else
        [| string v |]

let decodeBrowserArgs (args: obj) : Result<BrowserArgs, DomainError> =
    decodeIntentField "browser" "intent" args
    |> Result.map (fun intent -> { Intent = intent })

let decodeContinueArgs (args: obj) : Result<ContinueArgs, DomainError> =
    let iteratorRes =
        let i = Dyn.get args "iterator"
        let iStr = if Dyn.isNullish i then "" else (string i).Trim()
        let cleanStr = iStr.Replace("\"", "").Replace("'", "").Trim()

        if cleanStr = "" then
            Error(InvalidIntent("continue", "iterator", "must be a string"))
        else
            Ok cleanStr

    iteratorRes
    |> Result.bind (fun iter ->
        let pr = Dyn.get args "prompt"

        if Dyn.isNullish pr then
            Error(InvalidIntent("continue", "prompt", "must be a string"))
        else
            let promptStr = string pr
            Ok { Iterator = iter; Prompt = promptStr })

let decodeIntentsField (toolName: string) (args: obj) : Result<obj, DomainError> =
    let v = Dyn.get args "intents"

    if Dyn.isNullish v then
        Error(InvalidIntent(toolName, "intents", "required"))
    else
        Ok v
