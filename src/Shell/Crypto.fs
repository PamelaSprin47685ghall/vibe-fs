module VibeFs.Shell.Crypto

open Fable.Core
open Fable.Core.JsInterop

[<Import("createHash", "node:crypto")>]
let private createHash (algorithm: string) : obj = jsNative

/// SHA-256 hex digest truncated to 16 characters — the stable fingerprint hasher.
let sha256HexTruncated (input: string) : string =
    let hash = createHash "sha256"
    hash?update(input) |> ignore
    hash?digest("hex")?slice(0, 16)
