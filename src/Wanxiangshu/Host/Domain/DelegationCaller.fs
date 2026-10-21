// HNI P-14 (domain seam): what every delegation-family command needs about
// its caller; tier B, never mirrored to the TypeScript port surface.
namespace Wanxiangshu.Host.Domain

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// The tool call that invoked a delegation-family command, as the host saw
/// it. `Session` is None when the host could not name the calling session.
type DelegationCaller =
    { Session: SessionId option
      ProviderRunId: ProviderRunIdentity option
      ToolCallId: ToolCallId option }

/// Host-neutral port every delegation-family command (fork, resume,
/// commission, horizon, join) needs about its caller. A host binds one
/// instance to one tool call; every member answers for that caller.
type IDelegationCallerHost =
    abstract Caller: DelegationCaller
    abstract Journal: AgentJournal option
    /// The logical owner of a physical session (the session itself when it has none).
    abstract LogicalOwnerFor: sessionId: SessionId -> SessionId
    abstract EnsureWorkDevOpsBound: parentSessionId: SessionId -> Task<unit>
    /// The caller's fork runtime; Error when the caller cannot own children.
    abstract ForkRuntime: unit -> Result<HostForkRuntime, string>
