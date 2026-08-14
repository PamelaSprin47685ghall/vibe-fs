namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

type OpencodeModel =
    { providerID: string
      modelID: string
      variant: string option }

type OpencodeTextPart =
    { id: string
      ``type``: string
      text: string
      synthetic: bool option }

type OpencodeToolCallPart =
    { id: string
      ``type``: string
      callID: string
      tool: string
      args: obj option }

type OpencodeCompactionPart =
    { id: string
      ``type``: string
      auto: bool
      overflow: bool }

type OpencodeUserMessage =
    { id: string
      role: string
      sessionID: string
      agent: string option
      model: OpencodeModel option
      parts: obj list }

type OpencodeAssistantMessage =
    { id: string
      parentID: string option
      role: string
      sessionID: string
      agent: string option
      providerID: string option
      modelID: string option
      summary: bool option
      error: obj option
      parts: obj list }

type OpencodeHookInput =
    { sessionID: string
      messageID: string option
      agent: string option
      model: OpencodeModel option }

type OpencodeToolExecuteInput =
    { tool: string
      sessionID: string
      callID: string }

type OpencodeToolExecuteOutput = { mutable args: obj }
