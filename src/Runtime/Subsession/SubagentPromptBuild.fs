module Wanxiangshu.Runtime.SubagentPromptBuild

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.WorkspaceFiles

let promptsFromCoderIntents (host: Host) (intents: CoderIntent list) : string list =
    promptsForParallelIntents host Coder intents

let promptsFromInspectorIntents (host: Host) (intents: InspectorIntent list) : string list =
    promptsForParallelIntents host Inspector intents

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
