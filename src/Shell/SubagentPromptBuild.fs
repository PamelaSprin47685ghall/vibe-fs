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
