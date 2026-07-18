module Wanxiangshu.Hosts.Mux.DelegateCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.Dyn

/// Task service input payload. Owned by DelegateCodec so that the wire shape
/// lives in one place and Delegate.fs never touches field-name strings.
type DelegationContext =
    { workspaceId: WorkspaceId
      taskService: obj
      aiSettings: Wanxiangshu.Runtime.DelegatedAiSettings.DelegatedAiSettings
      experiments: obj
      parentRuntimeAiSettings: obj
      abortSignal: obj }

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

    match modelString with
    | Some m when m.Trim() <> "" -> o?("modelString") <- m
    | _ -> ()

    match thinkingLevel with
    | Some t when t.Trim() <> "" -> o?("thinkingLevel") <- t
    | _ -> ()

    if not (Dyn.isNullish parentRuntimeAiSettings) then
        o?("parentRuntimeAiSettings") <- parentRuntimeAiSettings

    o
