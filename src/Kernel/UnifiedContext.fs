module VibeFs.Kernel.UnifiedContext

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel

/// The cross-tool execution context: which session, where, and an optional
/// abort handle.  Carried by reference through tool calls.
type UnifiedContext =
    { sessionID: string
      directory: string
      abortSignal: obj option
      parentSessionID: string option }

/// Look up the first present key among several aliases (camelCase / snake_case
/// variants hosts emit).  Pure over the dynamic object.
let private firstPresent (keys: string list) (ctx: obj) : string option =
    keys |> List.tryPick (fun key ->
        let value = ctx?(key)
        if Dyn.isNullish value then None else Some(string value))

/// Resolve a host-supplied context object, throwing on missing required fields.
let resolveUnifiedContext (raw: obj) : UnifiedContext =
    if Dyn.isNullish raw then failwith "Invalid context: must be an object"
    let sessionID =
        firstPresent [ "sessionID"; "sessionId"; "session_id" ] raw
        |> Option.defaultWith (fun () -> failwith "Missing required context field: sessionID")
    let directory =
        firstPresent [ "directory"; "cwd"; "workspaceDir"; "workspace_dir"; "workingDirectory" ] raw
        |> Option.defaultWith (fun () -> failwith "Missing required context field: directory")
    let abortSignal = let v = raw?("abortSignal") in if Dyn.isNullish v then None else Some v
    let parentSessionID = firstPresent [ "parentSessionID"; "parentSessionId"; "parent_session_id" ] raw
    { sessionID = sessionID; directory = directory; abortSignal = abortSignal; parentSessionID = parentSessionID }

/// Build a context from explicit arguments.
let createUnifiedContext (sessionID: string) (directory: string)
                         (abortSignal: obj option) (parentSessionID: string option) : UnifiedContext =
    { sessionID = sessionID; directory = directory; abortSignal = abortSignal; parentSessionID = parentSessionID }
