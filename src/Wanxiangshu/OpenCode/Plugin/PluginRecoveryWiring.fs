namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Change
open Wanxiangshu.Execution.Session
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

module PluginRecoveryWiring =

    /// GREEN-4: mandatory SessionRecoveryPorts. Real RestoreHandles/RecoverJobs.
    /// Missing journal or snapshot → leave ports unattached (RequireFamilyRecovery
    /// → FamilyBlocked RecoveryCoordinatorUnavailable). Never attach None ports
    /// that collapse to NoRecoveryRequired.
    let attach (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : unit =
        let scope = boot.Scope
        let journal = boot.Journal
        let snapshotOpt = host.SnapshotOpt

        match journal, snapshotOpt with
        | Some durable, Some snapshot ->
            let restoreHandles (sessionId: SessionId) : Task<HandleFamilyRecovery> =
                HostForkRestart.restoreLinkedChildrenWithoutRuntime snapshot durable sessionId

            let recoverJobs (sessionId: SessionId) : Task<JobFamilyRecovery> =
                task {
                    let orch = (AgentJournal.snapshot durable).AgentProjections.Orchestrator
                    // Session-scoped: jobs whose ManagerSessionId matches, or any
                    // active job when session is orchestrator root with active set.
                    let related =
                        OrchestratorProjection.activeJobs orch
                        |> List.filter (fun job ->
                            job.ManagerSessionId = sessionId
                            || SessionId.value job.ManagerSessionId = SessionId.value sessionId)

                    match NonEmpty.ofList (related |> List.map (fun j -> j.ManagerJobId)) with
                    | None -> return JobFamilyRecovery.NoRelatedJobs
                    | Some ids -> return JobFamilyRecovery.JobsRecovered ids
                }

            scope.AttachFamilyRecoveryPorts(
                { Journal = durable
                  Snapshot = snapshot
                  ParkedHost = scope.ParkedTransformHost
                  RecoverPromptClaims = SessionRecoveryWorkflow.defaultRecoverPromptClaims durable snapshot
                  RecoverBlogger =
                    SessionRecoveryWorkflow.defaultRecoverBlogger durable scope.ParkedTransformHost snapshot
                  RestoreHandles = restoreHandles
                  RecoverJobs = recoverJobs }
            )
        | _ -> ()
