namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel

/// The generated js-ROLE surface for one Attempt profile. Deterministic:
/// same capabilities → same bytes (JS-002; fast/deep identical).
type JsSurface =
    {
        ToolName: string
        RoleName: string
        Capabilities: Set<JsCapability>
        Members: JsCapabilityFragment list
        Description: string
        BaseClassSource: string
        /// Canonical examples, one per present member, in member order.
        Examples: string list
        /// member name → runtime binding key; the runtime gate checks the
        /// invoked member against this exact map (JS-004).
        RuntimeBindings: Map<string, string>
    }

module JsToolGenerator =

    let membersFor (capabilities: Set<JsCapability>) : JsCapabilityFragment list =
        capabilities
        |> Set.toList
        |> List.sortBy JsCapability.order
        |> List.choose (fun capability -> Map.tryFind capability JsFragmentRegistry.byCapability)

    let toolNameFor (roleName: string) : string = "js-" + roleName.ToLowerInvariant()

    let renderBaseClass (capabilities: Set<JsCapability>) : string =
        JsCanonicalDescription.runtimeBaseClass capabilities

    let renderPublicBaseClass (capabilities: Set<JsCapability>) : string =
        JsCanonicalDescription.publicBaseClass capabilities

    let renderDescription (roleName: string) (capabilities: Set<JsCapability>) : string =
        JsCanonicalDescription.render (toolNameFor roleName) capabilities

    let renderExamples (capabilities: Set<JsCapability>) : string list =
        JsCanonicalDescription.filteredExamples capabilities
        |> List.map (fun example -> example.Source)

    /// Deterministic projection: an Attempt profile with no filesystem
    /// capability gets no js-* surface at all (JS-001/004).
    let generate (roleName: string) (capabilities: Set<ToolPermission>) : JsSurface option =
        let jsCapabilities = JsCapability.ofToolCapabilities capabilities

        if Set.isEmpty jsCapabilities then
            None
        else
            let members = membersFor jsCapabilities

            Some
                { ToolName = toolNameFor roleName
                  RoleName = roleName
                  Capabilities = jsCapabilities
                  Members = members
                  Description = renderDescription roleName jsCapabilities
                  BaseClassSource = renderBaseClass jsCapabilities
                  Examples = renderExamples jsCapabilities
                  RuntimeBindings =
                    members
                    |> List.map (fun fragment -> fragment.MemberName, fragment.RuntimeBindingKey)
                    |> Map.ofList }

    /// Generated-name gate: a js-* tool call is accepted iff its name is the
    /// surface this profile generates; any other name fails closed (JS-001).
    let isGeneratedToolName (roleName: string) (capabilities: Set<ToolPermission>) (toolName: string) : bool =
        generate roleName capabilities
        |> Option.map (fun surface -> surface.ToolName = toolName)
        |> Option.defaultValue false

    /// Runtime member gate: a member invocation is accepted iff the member is
    /// present in this profile's surface; the returned binding key names the
    /// exact executor (JS-004 — forged calls have no binding).
    let memberBinding (roleName: string) (capabilities: Set<ToolPermission>) (memberName: string) : string option =
        generate roleName capabilities
        |> Option.bind (fun surface -> Map.tryFind memberName surface.RuntimeBindings)
