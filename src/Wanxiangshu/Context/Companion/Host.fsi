namespace Wanxiangshu.Context.Companion

open System
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

type CompanionHost =
    new:
        primaryId: SessionId *
        sessions: ISessionHostPort *
        ?durable: ICompanionDurablePort *
        ?onBloggerCreated: (SessionId -> unit) *
        ?restoredBloggerId: string *
        ?journal: AgentJournal *
        ?bloggerDirectory: string *
        ?satelliteRuntime: SatelliteRuntime ->
            CompanionHost

    member EnsureBloggerAsync: unit -> Task<SessionId>
    member StartFromContext: ctx: BloggerRequestContext -> Task<Result<PromptKey, string>>
    member InvalidateBloggerCache: unit -> unit
    member Memory: CompanionMemory
    member RefreshXTrace: state: XTraceProjectionState -> unit
    member BloggerSession: SessionId option
    member PrimarySessionId: SessionId
    member CloseBloggerAsync: unit -> Task

    interface IDisposable
