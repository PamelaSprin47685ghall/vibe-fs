namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal
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

/// EXEC-016: join-capable roles must join outstanding work before terminal idle.
module HostJoinGuard =

    [<RequireQualifiedAccess>]
    type JoinGuardNudgeOutcome =
        | Sent of PromptKey
        | AlreadyOutstanding
        | Failed of reason: string

    let private processNudgeKeys = HashSet<string>()

    let private hasOutstandingJoinClaim (journal: AgentJournal) (targetSessionId: SessionId) =
        AgentProjection.tryFind targetSessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.PromptAuthority)
        |> Option.map (fun authority ->
            authority.PendingClaims
            |> Map.exists (fun _ claim ->
                claim.Origin = PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.JoinGuard))
        |> Option.defaultValue false

    let private nudgeKey (runtimeId: RuntimeId) (targetSessionId: SessionId) =
        sprintf "join-guard:%s:%s" (RuntimeId.value runtimeId) (SessionId.value targetSessionId)

    /// Send JoinGuard Continuation. Dedupes on durable PendingClaims + process key.
    let nudge
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        (sessionId: SessionId)
        (directory: string option)
        : Task<JoinGuardNudgeOutcome> =
        task {
            match journal with
            | None -> return JoinGuardNudgeOutcome.Failed "Join guard nudge requires an AgentJournal"
            | Some durable ->
                let key = nudgeKey (AgentJournal.runtimeId durable) sessionId

                let reserved =
                    lock processNudgeKeys (fun () ->
                        if
                            hasOutstandingJoinClaim durable sessionId
                            || nudgeKeys.Contains key
                            || processNudgeKeys.Contains key
                        then
                            false
                        else
                            nudgeKeys.Add key |> ignore
                            processNudgeKeys.Add key |> ignore
                            true)

                if not reserved then
                    return JoinGuardNudgeOutcome.AlreadyOutstanding
                else
                    let releaseKey () =
                        lock processNudgeKeys (fun () ->
                            nudgeKeys.Remove key |> ignore
                            processNudgeKeys.Remove key |> ignore)

                    let! sent =
                        HostSessionNudge.sendContinuation
                            sessionPort
                            sessionId
                            (ProviderProse.documentFor sessionId RuntimeNudge.BackgroundJoin Map.empty)
                            PromptAuthority.ContinuationKind.JoinGuard
                            directory
                            (Some durable)

                    match sent with
                    | Ok promptKey -> return JoinGuardNudgeOutcome.Sent promptKey
                    | Error error ->
                        releaseKey ()
                        return JoinGuardNudgeOutcome.Failed error
        }
