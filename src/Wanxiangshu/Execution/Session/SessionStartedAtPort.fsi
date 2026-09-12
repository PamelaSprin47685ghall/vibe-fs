namespace Wanxiangshu.Execution.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

type SessionStartedAtPort =
    { TryStartedAt: SessionId -> DateTimeOffset option
      Bind: SessionId -> DateTimeOffset -> Task<Result<DateTimeOffset, string>> }
