namespace Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

open Wanxiangshu.Foundation

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
        /// GrandRewrite §6.10: exactly one responsibility-shaped Ultra Example.
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

    let private tryProject (roleName: string) (capabilities: Set<ToolPermission>) =
        let jsCapabilities = JsCapability.ofToolCapabilities capabilities

        if Set.isEmpty jsCapabilities then
            None
        else
            Some(toolNameFor roleName, jsCapabilities, membersFor jsCapabilities)

    let renderBaseClass (prose: JsCanonicalDescription.Prose) (capabilities: Set<JsCapability>) : string =
        JsCanonicalDescription.runtimeBaseClass prose capabilities

    let renderPublicBaseClass (prose: JsCanonicalDescription.Prose) (capabilities: Set<JsCapability>) : string =
        JsCanonicalDescription.publicBaseClass prose capabilities

    let renderDescription
        (prose: JsCanonicalDescription.Prose)
        (roleName: string)
        (capabilities: Set<JsCapability>)
        : string =
        JsCanonicalDescription.render prose roleName (toolNameFor roleName) capabilities

    let renderExamples
        (prose: JsCanonicalDescription.Prose)
        (roleName: string)
        (capabilities: Set<JsCapability>)
        : string list =
        JsCanonicalDescription.ultraExample prose roleName capabilities
        |> Option.map (fun example -> [ example.Source ])
        |> Option.defaultValue []

    /// Deterministic projection: an Attempt profile with no filesystem
    /// capability gets no js-* surface at all (JS-001/004).
    let generate
        (roleName: string)
        (capabilities: Set<ToolPermission>)
        (prose: JsCanonicalDescription.Prose)
        : JsSurface option =
        match tryProject roleName capabilities with
        | None -> None
        | Some(toolName, jsCapabilities, members) ->
            Some
                { ToolName = toolName
                  RoleName = roleName
                  Capabilities = jsCapabilities
                  Members = members
                  Description = renderDescription prose roleName jsCapabilities
                  BaseClassSource = renderBaseClass prose jsCapabilities
                  Examples = renderExamples prose roleName jsCapabilities
                  RuntimeBindings =
                    members
                    |> List.map (fun fragment -> fragment.MemberName, fragment.RuntimeBindingKey)
                    |> Map.ofList }

    /// Generated-name gate: a js-* tool call is accepted iff its name is the
    /// surface this profile generates; any other name fails closed (JS-001).
    let isGeneratedToolName (roleName: string) (capabilities: Set<ToolPermission>) (toolName: string) : bool =
        tryProject roleName capabilities
        |> Option.map (fun (name, _, _) -> name = toolName)
        |> Option.defaultValue false

    /// Runtime member gate: a member invocation is accepted iff the member is
    /// present in this profile's surface; the returned binding key names the
    /// exact executor (JS-004 — forged calls have no binding).
    let memberBinding (roleName: string) (capabilities: Set<ToolPermission>) (memberName: string) : string option =
        tryProject roleName capabilities
        |> Option.bind (fun (_, _, members) ->
            members
            |> List.tryFind (fun fragment -> fragment.MemberName = memberName)
            |> Option.map (fun fragment -> fragment.RuntimeBindingKey))
