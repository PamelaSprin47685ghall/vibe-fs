module Wanxiangshu.Hosts.Mux.DelegateCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity

module Dyn = Wanxiangshu.Runtime.Dyn

/// Task service input payload. Owned by DelegateCodec so that the wire shape
/// lives in one place and Delegate.fs never touches field-name strings.
type DelegationContext =
    { workspaceId: WorkspaceId
      taskService: obj
      aiSettings: Wanxiangshu.Runtime.DelegatedAiSettings.DelegatedAiSettings
      experiments: obj
      parentRuntimeAiSettings: obj
      abortSignal: obj }

/// Build a stable dispatch identity carried on Mux delegate create/continue.
let newDispatchIdentity (kind: string) (workspaceId: WorkspaceId) (logicalTurnId: string) : obj =
    let dispatchId = "dispatch-" + System.Guid.NewGuid().ToString("N")

    createObj
        [ "schemaVersion", box 1
          "dispatchId", box dispatchId
          "workspaceId", box (Id.workspaceIdValue workspaceId)
          "kind", box kind
          "logicalTurnId", box logicalTurnId
          "attempt", box 0 ]

/// Build the `create`-call input object for the Mux task service.
let buildCreateInput
    (workspaceId: WorkspaceId)
    (agentId: string)
    (prompt: string)
    (title: string)
    (modelString: string option)
    (thinkingLevel: string option)
    (parentRuntimeAiSettings: obj)
    (experiments: obj)
    : obj =
    let o = createObj []
    o?("parentWorkspaceId") <- Id.workspaceIdValue workspaceId
    o?("kind") <- "agent"
    o?("agentId") <- agentId
    o?("prompt") <- prompt
    o?("title") <- title
    o?("experiments") <- experiments
    o?("dispatchIdentity") <- newDispatchIdentity "subsession_turn" workspaceId title

    match modelString with
    | Some m when m.Trim() <> "" -> o?("modelString") <- m
    | _ -> ()

    match thinkingLevel with
    | Some t when t.Trim() <> "" -> o?("thinkingLevel") <- t
    | _ -> ()

    if not (Dyn.isNullish parentRuntimeAiSettings) then
        o?("parentRuntimeAiSettings") <- parentRuntimeAiSettings

    o

/// Build continue-call options with the same dispatch identity surface.
let buildContinueOpts
    (workspaceId: WorkspaceId)
    (childTaskId: string)
    (abortSignal: obj)
    : obj =
    let o = createObj []
    o?("requestingWorkspaceId") <- Id.workspaceIdValue workspaceId
    o?("abortSignal") <- abortSignal
    o?("backgroundOnMessageQueued") <- false
    o?("dispatchIdentity") <- newDispatchIdentity "subsession_turn" workspaceId childTaskId
    o
