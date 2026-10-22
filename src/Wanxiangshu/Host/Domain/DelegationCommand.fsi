// HNI P-15 (domain seam): what a delegation command answers; tier B, never
// mirrored to the TypeScript port surface.
//
// The capabilities the fork / resume / commission commands bind to live in
// the product record `Execution/Delegation/Fork/DelegationEnvironment.fs(i)`.
namespace Wanxiangshu.Host.Domain

/// What a delegation command answers. Both cases are one instruction for the
/// calling participant; the host decides how to put it on its wire.
[<RequireQualifiedAccess>]
type DelegationReply =
    /// The charge was carried, sent as guidance, or commissioned.
    | Placed of instruction: string
    /// The command refused, or the placement failed or is uncertain.
    | Refused of instruction: string
