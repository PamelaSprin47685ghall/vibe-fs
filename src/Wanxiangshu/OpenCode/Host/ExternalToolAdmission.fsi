namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

/// Runtime admission for Host-declared external tools (capability-enforcement-007).
///
/// A Host-declared MCP tool holds no office capability token. Under
/// capability-enforcement-006 only the domain capability matrix grants tool
/// authority, and capability-enforcement-007 forbids a second permission table
/// per technical source — so the projection advertises the namespace without
/// granting it, and this gate is where the same policy closes at execution.
///
/// A session with no canonical office role is not admitted by this plugin at
/// all; its Host tool surface is the Host's own business.
[<RequireQualifiedAccess>]
module ExternalToolAdmission =
    /// The session identity the office authority is resolved against. An
    /// external tool call carries no review or delegation contract, so the
    /// context stays minimal.
    val before: runtime: ToolRuntimeScope -> sessionId: string -> toolName: string -> Task<unit>
