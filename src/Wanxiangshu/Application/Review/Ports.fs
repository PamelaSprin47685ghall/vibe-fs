namespace Wanxiangshu.Review

open System.Threading.Tasks
open Wanxiangshu.Kernel.Identity

/// Review-facing tree capability. Infrastructure owns the physical Git adapter;
/// Application only sees the read contract.
[<Struct>]
type GitTreePort = { GetTreeHash: unit -> string }

/// Physical reviewer capabilities used by one review barrier drive.
type ReviewHostPort =
    { ForkReviewer: unit -> Task<Result<SessionId, string>>
      AwaitReviewer: unit -> Task<Result<unit, string>> }
