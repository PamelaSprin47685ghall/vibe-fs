namespace Wanxiangshu.Host.Ports

open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// HNI P-04: product tools contributed to a host.
///
/// `Session` and `CallId` are required: a host that cannot name the calling
/// session, or that does not give this call a call id, refuses the call at its
/// own boundary and never constructs an invocation. `ProviderRun` is the
/// provider run the host observed for THIS call; it is absent when the host
/// observed none, and is never resolved from the session's current binding.
type ToolInvocation =
    { Session: SessionId
      CallId: ToolCallId
      ProviderRun: ProviderRunIdentity option
      Workspace: WorkspacePath
      Args: CanonicalJsonText
      Cancellation: CancellationToken
      Progress: (string -> unit) option }

type ToolOutcome =
    { Text: string
      Truncated: bool
      IsError: bool
      Details: CanonicalJsonText option }

/// The authority a tool call is admitted under, as the product declares it.
///
/// capability-enforcement-006 keeps the two sources apart in the type: an
/// office tool is decided by the public role, an internal leaf tool
/// (Bookkeeper's `todowrite-for-bookkeeper`) by owner-held attachment
/// evidence, because its session holds no public authority profile at all.
///
/// The role is the neutral `AgentRole`, and the role that reaches the
/// predicate is the one the product projected from the established authority
/// profile — never a role label the host asserted. The predicate is a product
/// closure, so only the product decides what a role may do; a role the
/// product does not know admits nothing.
[<RequireQualifiedAccess>]
type ToolAdmission =
    | OfficeRole of (AgentRole -> bool)
    | PrivateAttachment of (ToolInvocation -> bool)

/// One product tool, as the host registers it. `Schema` is the product's own
/// JSON-schema document: one schema, both hosts.
///
/// `Admission` is the authority the product declares for this tool. A tool
/// that declares none is refused exactly like a tool whose office role does
/// not fit: absence is a refusal, never a pass.
type ToolSpec =
    { Name: string
      Description: string
      Schema: JsonSchema
      Admission: ToolAdmission option
      Execute: ToolInvocation -> Task<ToolOutcome> }

/// The registration port: a host takes the product's tools and mounts them.
type IToolRegistrar =
    abstract Register: tools: ToolSpec list -> Result<unit, HostError>
