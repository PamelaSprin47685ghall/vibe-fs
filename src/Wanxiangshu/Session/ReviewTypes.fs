namespace Wanxiangshu.Session

/// Port used by ReviewController / HostReviewGuard to read the current tree hash.
/// Distinct from OpenCode.GitTreePort adapters that wrap this shape.
[<Struct>]
type GitTreePort = { GetTreeHash: unit -> string }
