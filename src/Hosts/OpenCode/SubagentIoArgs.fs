module Wanxiangshu.Hosts.Opencode.SubagentIoArgs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.DelegatedAiSettings
open Wanxiangshu.Hosts.Opencode.SubagentTypes

let buildSubagentOptions
    (agent: string)
    (title: string)
    (prompt: string)
    (directory: string)
    (sessionID: string)
    (tools: obj)
    : SubagentLaunchOptions =
    { agent = agent
      title = title
      prompt = prompt
      directory = directory
      sessionID = sessionID
      tools = tools
      aiSettings =
        { modelString = None
          thinkingLevel = None } }
