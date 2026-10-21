namespace Wanxiangshu.Execution.Tool

open Wanxiangshu.Host

/// capability-enforcement: the declared authority of every product tool, as
/// product policy. Host adapters attach these to the tools they register; they
/// never decide one themselves.
module ToolAdmissions =
    val join: ToolAdmission
    val horizon: ToolAdmission
    val fission: ToolAdmission
    val review: ToolAdmission
    val deliver: ToolAdmission
    val bashHoneypot: ToolAdmission
    val assume: ToolAdmission
    val fetch: ToolAdmission

    /// A role's generated programming tool (`todowrite-for-<role>`): the role's
    /// office permissions must project a non-empty programming surface.
    val programmingFor: roleName: string -> ToolAdmission
