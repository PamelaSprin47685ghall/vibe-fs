namespace Wanxiangshu.Execution.Session.Recovery

open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

module SessionRecoveryWorkflow =
    type SessionRecoveryPorts =
        { Journal: AgentJournal
          Snapshot: ISessionSnapshotPort
          BloggerHost: IBloggerRuntimeHost
          RecoverPromptClaims: SessionId -> Task<SessionRecovery>
          RecoverBlogger: SessionId -> Task<SessionRecovery>
          RestoreHandles: SessionId -> Task<HandleFamilyRecovery>
          RecoverJobs: SessionId -> Task<JobFamilyRecovery> }

    val defaultRecoverBlogger:
        journal: AgentJournal ->
        host: IBloggerRuntimeHost ->
        snapshot: ISessionSnapshotPort ->
            (SessionId -> Task<SessionRecovery>)

    val defaultRecoverPromptClaims:
        journal: AgentJournal -> snapshot: ISessionSnapshotPort -> (SessionId -> Task<SessionRecovery>)

    val recoverFamilyDirect: ports: SessionRecoveryPorts -> parentSession: SessionId -> Task<FamilyRecovery>
