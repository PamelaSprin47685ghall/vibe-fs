namespace Wanxiangshu.Context.Companion

open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity

type BlogText = string

type CompanionMemory =
    { Blog: BlogProjectionState
      EffectiveFrames: BlogText option
      BloggerSessionId: SessionId option
      XTrace: XTraceProjectionState }

type ICompanionDurablePort =
    abstract Load: sessionId: SessionId -> Task<Result<CompanionMemory option, string>>

    abstract LinkBlogger:
        sessionId: SessionId * bloggerSessionId: SessionId * bloggerAgent: string -> Task<Result<unit, string>>

    abstract CloseBlogger: sessionId: SessionId -> Task<Result<unit, string>>
