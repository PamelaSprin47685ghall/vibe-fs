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
        let Description = "tool/fission/description"

        [<Literal>]
        let TooFew = "tool/fission/too-few"

        [<Literal>]
        let InvalidOrigin = "tool/fission/invalid-origin"

        [<Literal>]
        let Capacity = "tool/fission/capacity"

        [<Literal>]
        let AlreadyActive = "tool/fission/already-active"

        [<Literal>]
        let Unavailable = "tool/fission/unavailable"

    /// The user-facing description of the tool, in the given language.
    let describe (language: ProviderLanguage) : string =
        ProviderProse.render language Path.Description Map.empty

    /// The instruction lines rendered for a semantic path — the prose a
    /// refusal is reported with.
    let instructions (language: ProviderLanguage) (path: string) : string list =
        ProviderProse.instructionLines language path Map.empty

    /// The office entitlement of the tool: only a role that holds
    /// `ToolPermission.Fission` may run it.
    let isAllowedRole (role: Role) : bool =
        OfficeCapability.isAllowed role ToolPermission.Fission
