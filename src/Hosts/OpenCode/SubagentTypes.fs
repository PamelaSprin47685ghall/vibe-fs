module Wanxiangshu.Hosts.Opencode.SubagentTypes

open Fable.Core
open Wanxiangshu.Runtime.DelegatedAiSettings

[<Global>]
type DOMException(message: string, name: string) =
    inherit System.Exception()

type SubagentLaunchOptions =
    { agent: string
      title: string
      prompt: string
      directory: string
      sessionID: string
      tools: obj
      aiSettings: DelegatedAiSettings }
