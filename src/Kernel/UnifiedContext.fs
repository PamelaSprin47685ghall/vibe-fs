module VibeFs.Kernel.UnifiedContext

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Boundary

/// The cross-tool execution context: which session, where, and an optional
/// abort handle.  Carried by reference through tool calls.
type UnifiedContext =
    { sessionId: SessionId
      directory: string
      abortSignal: obj option
      parentSessionId: SessionId option }

let sessionIdValue (context: UnifiedContext) = Id.sessionIdValue context.sessionId
let parentSessionIdValue (context: UnifiedContext) = context.parentSessionId |> Option.map Id.sessionIdValue

/// Look up the first present key among several aliases (camelCase / snake_case
/// variants hosts emit).  Pure over the dynamic object.
let private firstPresent (keys: string list) (ctx: obj) : string option =
    keys |> List.tryPick (fun key ->
        let value = ctx?(key)
        if Dyn.isNullish value then None else Some(string value))

let private requireSessionId fieldName rawValue =
    match Id.sessionId rawValue with
    | Ok sessionId -> sessionId
    | Error _ -> failwith $"Missing required context field: {fieldName}"

let private optionalSessionId rawValue =
    match rawValue with
    | None -> None
    | Some value -> Id.sessionId value |> Result.toOption

/// Resolve a host-supplied context object, throwing on missing required fields.
let resolveUnifiedContext (raw: obj) : UnifiedContext =
    if Dyn.isNullish raw then failwith "Invalid context: must be an object"
    let sessionId =
        firstPresent [ "sessionID"; "sessionId"; "session_id" ] raw
        |> Option.map (requireSessionId "sessionID")
        |> Option.defaultWith (fun () -> failwith "Missing required context field: sessionID")
    let directory =
        firstPresent [ "directory"; "cwd"; "workspaceDir"; "workspace_dir"; "workingDirectory" ] raw
        |> Option.defaultWith (fun () -> failwith "Missing required context field: directory")
    let abortSignal = let v = raw?("abortSignal") in if Dyn.isNullish v then None else Some v
    let parentSessionId = firstPresent [ "parentSessionID"; "parentSessionId"; "parent_session_id" ] raw |> optionalSessionId
    { sessionId = sessionId; directory = directory; abortSignal = abortSignal; parentSessionId = parentSessionId }

/// Build a context from explicit arguments.
let createUnifiedContext (sessionId: SessionId) (directory: string)
                         (abortSignal: obj option) (parentSessionId: SessionId option) : UnifiedContext =
    { sessionId = sessionId; directory = directory; abortSignal = abortSignal; parentSessionId = parentSessionId }
