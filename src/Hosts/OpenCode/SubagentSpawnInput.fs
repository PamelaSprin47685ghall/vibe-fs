module Wanxiangshu.Hosts.Opencode.SubagentSpawnInput

open Fable.Core
open Fable.Core.JsInterop

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.OpencodeContextCodec
open Wanxiangshu.Runtime.OpencodeSessionPromptCodec
open Wanxiangshu.Hosts.Opencode.SubagentTypes

let getAbortSignal (context: obj) : obj = getAbortSignalFromContext context

let invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

let buildPromptBody (options: SubagentLaunchOptions) childID : obj =
    let body =
        box
            {| agent = options.agent
               parts =
                [| box
                       {| ``type`` = "text"
                          text = options.prompt |} |] |}

    let body =
        if Dyn.isNullish options.tools then
            body
        else
            Dyn.withKey body "tools" options.tools

    let body =
        match options.aiSettings.modelString with
        | None -> body
        | Some modelString ->
            let payload = createObj [ "modelString", box modelString ]

            match tryDecodePromptModelFromPayload payload with
            | Some model -> Dyn.withKey body "model" model
            | None -> body

    let body =
        match options.aiSettings.thinkingLevel with
        | Some level when level.Trim() <> "" -> Dyn.withKey body "variant" (box level)
        | _ -> body

    createObj [ "path", box {| id = childID |}; "body", body ]
