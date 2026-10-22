// HNI P-14 (domain seam): what every delegation-family command needs about
// its caller; tier B, never mirrored to the TypeScript port surface.
//
// The capabilities the command runs against live in the product record
// `Execution/Delegation/Fork/DelegationEnvironment.fs(i)`; this file carries
// only the host's contribution about the caller.
namespace Wanxiangshu.Host.Domain

open Wanxiangshu.Foundation.Identity

/// The tool call that invoked a delegation-family command, as the host saw
/// it. `Session` is None when the host could not name the calling session.
type DelegationCaller =
    { Session: SessionId option
      ProviderRunId: ProviderRunIdentity option
      ToolCallId: ToolCallId option }
