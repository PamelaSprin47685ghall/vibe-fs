namespace Wanxiangshu.Context.Companion

open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

module internal CompanionHostBlogger =
    type BloggerDeps =
        { Sessions: ISessionHostPort
          PrimaryId: SessionId
          Durable: ICompanionDurablePort option
          EnsureBlogger: unit -> Task<SessionId>
          Gate: obj
          Companion: Companion
          Journal: AgentJournal option
          Participant: string }

    val startFromContext: deps: BloggerDeps -> ctx: BloggerRequestContext -> Task<Result<PromptKey, string>>
