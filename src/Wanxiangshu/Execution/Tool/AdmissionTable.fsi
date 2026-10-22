namespace Wanxiangshu.Execution.Tool

open Wanxiangshu.Host.Ports

/// capability-enforcement-006: which declared authority each product tool name
/// carries, so a consumer can tell an office tool from an internal leaf without
/// guessing from the tool name.
module ToolAdmissionTable =

    /// knowledge-reuse-006 / capability-enforcement-006: the Bookkeeper is an internal leaf whose
    /// prompt is HostInternal, so its session never holds a public office Role.
    /// Its authority is the owner-held transaction attachment itself.
    val bookkeeper: ToolAdmission

    /// capability-enforcement-006: the authority the execute gate resolves for a tool.
    val tryAdmission: specName: string -> ToolAdmission option

    /// capability-enforcement-006: the internal-leaf decision for a session that holds no public
    /// office profile at all. An office tool is never admitted this way.
    val privateAttachmentAdmits: specName: string -> invocation: ToolInvocation -> bool

    /// The internal-leaf decision asked by session id alone, for a surface
    /// that holds no call fence. It answers the same attachment fact the
    /// invocation-based decision reads.
    val privateAttachmentAdmitsForSession: specName: string -> sessionId: string -> bool

    /// AGENT-007 role gate, delegates to owner-defined tool admissions.
    /// The role reaches the product predicate as the neutral AgentRole the
    /// product projected from the established authority profile.
    val officeRolePredicate: specName: string -> role: AgentRole -> bool
