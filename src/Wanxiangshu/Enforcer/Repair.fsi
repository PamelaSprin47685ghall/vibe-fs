namespace Wanxiangshu.Enforcer

open System
open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal

module EnforcerRepair =

    val RepairInstruction: string

    val tryOpenByBlogger:
        Wanxiangshu.Persistence.Journal.AgentJournal ->
        Wanxiangshu.Foundation.Identity.SessionId ->
        Wanxiangshu.Foundation.Identity.SessionId ->
            Wanxiangshu.Context.Companion.Blogger.Runtime.OpenBloggerRequest option

    val chronicleCallCount: obj list -> int
    val hasIncompleteBlogTool: obj list -> bool
    val hasCompletedBlogTool: obj list -> bool
    val hasAnyBlogToolPart: obj list -> bool
    val hasAbortedBlogAttempt: obj list -> bool
    val hasErroredBlogAttempt: obj list -> bool
    val withRepairInstruction: obj list -> string -> Wanxiangshu.Foundation.Identity.ProviderRunIdentity -> obj list
