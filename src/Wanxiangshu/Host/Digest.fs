namespace Wanxiangshu.Host

open Fable.Core
open Fable.Core.JsInterop

/// The single Host crypto adapter.
///
/// Six modules each imported `node:crypto` and wrote their own three-line
/// sha256-hex wrapper: prompt identity, runtime path, Git tree, executor ids,
/// the publish lock key, and the Companion prefix digest. Identical knowledge,
/// six times, with four different local names for it.
///
/// Pure domains never call this. They take `sha256: string -> string` as a
/// parameter, which is why `Domain/` has no `node:crypto` import and stays
/// testable without a Host (VERIFY-008).
module HostDigest =

    [<Import("createHash", "node:crypto")>]
    let private createHash: string -> obj = jsNative

    /// Lowercase hex SHA-256 of a UTF-8 string.
    ///
    /// The one hash function this codebase has. Digests appear in durable
    /// facts (REVIEW-010 seals, COMPANION-011 prefix digests) and in derived
    /// identities, so a second implementation that differed in encoding would
    /// invalidate stored evidence rather than merely disagree.
    let sha256Hex (input: string) : string =
        let hash = createHash "sha256"
        hash?update (box input) |> ignore
        unbox<string> (hash?digest (box "hex"))
