module Wanxiangshu.Shell.SubagentPromptBuild

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Shell.WorkspaceFiles

let promptsFromCoderIntents (host: Host) (intents: CoderIntent list) : string list =
    promptsForParallelIntents host Coder intents

let promptsFromInvestigatorIntents (host: Host) (intents: InvestigatorIntent list) : string list =
    promptsForParallelIntents host Investigator intents

let parallelPromptsFromIntents
    (host: Host)
    (_toolLabel: string)
    (parse: obj -> Result<'a list, string>)
    (constructor: 'a list -> SubagentTaskKind)
    (intentsObj: obj)
    : Result<string list, DomainError> =
    match parse intentsObj with
    | Error message -> Error(ParseError("intents", message))
    | Ok intents -> Ok(promptsForParallelIntents host constructor intents)

let buildMeditatorSections (files: string array) (results: ReverieFileResult array) : MeditatorFileSection array =
    Array.zip files results
    |> Array.map (fun (file, r) -> { file = file; content = r.content })

let meditatorPromptFromFiles
    (host: Host)
    (cwd: string)
    (intent: string)
    (files: string array)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        let! readResults = readReverieFiles cwd (List.ofArray files)

        let sections =
            buildMeditatorSections files (List.toArray readResults) |> Array.toList

        return Ok(meditatorPromptText host intent sections)
    }
