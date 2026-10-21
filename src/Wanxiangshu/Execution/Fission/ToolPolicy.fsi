namespace Wanxiangshu.Execution.Fission

open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider

/// The product policy of the `fission` tool: its prose vocabulary, its prose
/// rendering and its office entitlement.
///
/// The decision is product policy, so it lives in the product lane. The host
/// adapter owns only the wire encoding of these facts: which semantic path a
/// rejection is reported under, and how the prose is carried to the host.
[<RequireQualifiedAccess>]
module FissionToolPolicy =

    /// The semantic prose paths (resource keys) of the `fission` tool.
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/fission/description"

        [<Literal>]
        val TooFew: string = "tool/fission/too-few"

        [<Literal>]
        val InvalidOrigin: string = "tool/fission/invalid-origin"

        [<Literal>]
        val Capacity: string = "tool/fission/capacity"

        [<Literal>]
        val AlreadyActive: string = "tool/fission/already-active"

        [<Literal>]
        val Unavailable: string = "tool/fission/unavailable"

    /// The user-facing description of the tool, in the given language.
    val describe: language: ProviderLanguage -> string

    /// The instruction lines rendered for a semantic path — the prose a
    /// refusal is reported with.
    val instructions: language: ProviderLanguage -> path: string -> string list

    /// The office entitlement of the tool: only a role that holds
    /// `ToolPermission.Fission` may run it.
    val isAllowedRole: role: Role -> bool
