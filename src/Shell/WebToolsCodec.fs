module VibeFs.Shell.WebToolsCodec

open VibeFs.Kernel.Domain
open VibeFs.Shell.Dyn
open VibeFs.Shell.DynField

type WebsearchArgs = {
    Query: string
    NumResults: int
    WhatToSummarize: string
}

type WebfetchArgs = {
    Url: string
    ExtractMain: bool option
    PreferLlmsTxt: string option
    Prompt: string option
    Timeout: int option
}

let private validPreferLlmsTxt = Set [ "auto"; "always"; "never" ]

let private optPreferLlmsTxt (a: obj) (k: string) : Result<string option, DomainError> =
    match strField a k with
    | None -> Ok None
    | Some v when validPreferLlmsTxt.Contains v -> Ok (Some v)
    | Some _ -> Error (InvalidIntent ("webfetch", "prefer_llms_txt", "must be auto, always, or never"))

let decodeWebsearchArgs (args: obj) : Result<WebsearchArgs, DomainError> =
    match strField args "query" with
    | None -> Error (InvalidIntent ("websearch", "query", "must be a string"))
    | Some query ->
        let whatToSummarize = defaultArg (strField args "what_to_summarize") ""
        if whatToSummarize = "" then Error (InvalidIntent ("websearch", "what_to_summarize", "required"))
        else
            Ok {
                Query = query
                NumResults = defaultArg (optInt args "numResults") 10
                WhatToSummarize = whatToSummarize
            }

let decodeWebfetchArgs (args: obj) : Result<WebfetchArgs, DomainError> =
    match strField args "url" with
    | None -> Error (InvalidIntent ("webfetch", "url", "must be a string"))
    | Some url ->
        match optPreferLlmsTxt args "prefer_llms_txt" with
        | Error e -> Error e
        | Ok prefer ->
            Ok {
                Url = url
                ExtractMain = optBool args "extract_main"
                PreferLlmsTxt = prefer
                Prompt = strField args "prompt"
                Timeout = optInt args "timeout"
            }