namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

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
