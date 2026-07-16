module Wanxiangshu.Hosts.Opencode.SystemTransform

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.ChatTransformOutputCodec

let systemTransform (directory: string) (_input: obj) (output: obj) : JS.Promise<unit> =
    promise { setSystemOutputToDirectory directory output }
