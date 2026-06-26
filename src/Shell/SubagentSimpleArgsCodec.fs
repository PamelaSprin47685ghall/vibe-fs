module Wanxiangshu.Shell.SubagentSimpleArgsCodec

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.Dyn

type MeditatorArgs = { Intent: string; Files: string array }

type BrowserArgs = { Intent: string }

let decodeIntentField (toolName: string) (fieldName: string) (args: obj) : Result<string, DomainError> =
    let v = Dyn.get args fieldName
    if Dyn.isNullish v then Error (InvalidIntent (toolName, fieldName, "must be a string"))
    else Ok(string v)

let private strArrayField (a: obj) (k: string) : string array =
    let v = Dyn.get a k
    if Dyn.isNullish v then [||]
    elif Dyn.isArray v then (v :?> obj array) |> Array.map string
    else [| string v |]

let decodeMeditatorArgs (args: obj) : Result<MeditatorArgs, DomainError> =
    decodeIntentField "meditator" "intent" args
    |> Result.map (fun intent -> { Intent = intent; Files = strArrayField args "files" })

let decodeBrowserArgs (args: obj) : Result<BrowserArgs, DomainError> =
    decodeIntentField "browser" "intent" args |> Result.map (fun intent -> { Intent = intent })

let decodeIntentsField (toolName: string) (args: obj) : Result<obj, DomainError> =
    let v = Dyn.get args "intents"
    if Dyn.isNullish v then Error (InvalidIntent (toolName, "intents", "required"))
    else Ok v