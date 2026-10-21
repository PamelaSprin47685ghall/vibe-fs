namespace Wanxiangshu.Execution.Tool

open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Repository.Programming.Js

module ToolAdmissions =

    let join: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.Join)

    let horizon: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.Horizon)

    let fission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.Fission)

    let review: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ role ->
            OfficeCapability.isAllowed role ToolPermission.ReviewAssessment
            || OfficeCapability.isAllowed role ToolPermission.Review)

    let deliver: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ role ->
            OfficeCapability.isAllowed role ToolPermission.Finality
            || OfficeCapability.isAllowed role ToolPermission.Deliver)

    let bashHoneypot: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.BashHoneypot)

    let assume: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ role -> OfficeCapability.admitsUtility role "assume")

    let fetch: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.Fetch)

    /// fork is the manager's own delegation tool; resume also answers the plan
    /// role, and commission is the orchestrator's hand-off.
    let fork: ToolAdmission = ToolAdmission.OfficeRole(fun _ r -> r = Role.Manager)

    let resume: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> r = Role.Manager || r = Role.Plan)

    let commission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> r = Role.Orchestrator)

    let programmingFor (roleName: string) : ToolAdmission =
        // Same predicate as StaticTools.jsPermission and JsToolGenerator: the
        // role's office permissions must project a non-empty programming surface
        // (JsPlan expands inside ofToolCapabilities). No second permission list.
        ToolAdmission.OfficeRole(fun _ r ->
            (string r).ToLowerInvariant() = roleName.ToLowerInvariant()
            && JsCapability.admitsProgrammingSurface (OfficeCapability.permissions r))
