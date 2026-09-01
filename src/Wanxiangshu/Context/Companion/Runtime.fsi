namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity

type Companion =
    new: ?initialMemory: CompanionMemory * ?durable: ICompanionDurablePort * ?sessionId: SessionId -> Companion

    member Memory: CompanionMemory
    member RecordBloggerLinked: bloggerId: SessionId -> unit
    member RecordBloggerClosed: unit -> unit
    member RefreshXTrace: state: XTraceProjectionState -> unit
