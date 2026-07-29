namespace Wanxiangshu.Next.OpenCode

open Fable.Core
open Fable.Core.JsInterop

/// Host crypto adapter used to derive stable prompt and Logical Run identities.
/// Authority rules receive this function as a dependency and remain pure.
module HostPromptHash =

    [<Import("createHash", "node:crypto")>]
    let private createHash: string -> obj = jsNative

    let sha256 (input: string) : string =
        let hash = createHash "sha256"
        hash?update (box input) |> ignore
        unbox<string> (hash?digest (box "hex"))
