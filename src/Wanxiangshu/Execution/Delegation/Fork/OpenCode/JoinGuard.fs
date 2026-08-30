namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open Wanxiangshu.OpenCode
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
        | AdmissionRejected of QuiescencePermitFailure
        | Superseded
        | NotSent
        | Failed of reason: string

    // DSL-MUTABLE: single-flight — one nudge per key across process
    let private processNudgeKeys = HashSet<string>()

    let private hasOutstandingJoinClaim
        (journal: AgentJournal)
        (targetSessionId: SessionId)
        (terminalProviderRun: ProviderRunIdentity)
        =
        let payloadDigest =
            PromptAuthority.gateNudgePayloadDigest RuntimeNudge.BackgroundJoin terminalProviderRun

        AgentProjection.tryFind targetSessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.PromptAuthority)
        |> Option.map (fun authority ->
            authority.PendingClaims
            |> Map.exists (fun _ claim ->
                claim.Origin = PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.JoinGuard
                && claim.PayloadDigest = payloadDigest))
        |> Option.defaultValue false

    /// One reservation per exact terminal occasion. A fresh ProviderRun is a
    /// fresh reminder opportunity while outstanding work still exists.
    let private nudgeKey (targetSessionId: SessionId) (terminalProviderRun: ProviderRunIdentity) =
        sprintf "join-guard:%s:%s" (SessionId.value targetSessionId) (ProviderRunIdentity.value terminalProviderRun)

    let private reserveNudge
        (durable: AgentJournal)
        (nudgeKeys: HashSet<string>)
        (sessionId: SessionId)
        (terminalProviderRun: ProviderRunIdentity)
        (key: string)
        : bool =
        lock processNudgeKeys (fun () ->
            if
                hasOutstandingJoinClaim durable sessionId terminalProviderRun
                || nudgeKeys.Contains key
                || processNudgeKeys.Contains key
            then
                false
            else
                nudgeKeys.Add key |> ignore
                processNudgeKeys.Add key |> ignore
                true)

    let private sendReservedNudge
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (nudgeKeys: HashSet<string>)
        (physicalAdmission: unit -> Result<unit, QuiescencePermitFailure>)
        (releaseAdmission: unit -> Result<unit, QuiescencePermitFailure>)
        (key: string)
        (sessionId: SessionId)
        (terminalProviderRun: ProviderRunIdentity)
        (directory: string option)
        : Task<JoinGuardNudgeOutcome> =
        task {
            let releaseKey () =
                lock processNudgeKeys (fun () ->
                    nudgeKeys.Remove key |> ignore
                    processNudgeKeys.Remove key |> ignore)

            let! sent =
                HostSessionNudge.trySendGateContinuationWithAdmission
                    physicalAdmission
                    releaseAdmission
                    sessionPort
                    sessionId
                    (ProviderProse.documentFor sessionId RuntimeNudge.BackgroundJoin Map.empty)
                    PromptAuthority.ContinuationKind.JoinGuard
                    directory
                    (Some durable)
                    RuntimeNudge.BackgroundJoin
                    terminalProviderRun
                    PromptDispatcher.AwaitMode.Await

            match sent with
            | HostSessionNudge.IdleContinuationOutcome.Sent promptKey -> return JoinGuardNudgeOutcome.Sent promptKey
            | HostSessionNudge.IdleContinuationOutcome.AlreadyAdmitted ->
                return JoinGuardNudgeOutcome.AlreadyOutstanding
            | HostSessionNudge.IdleContinuationOutcome.AdmissionRejected failure ->
                releaseKey ()
                return JoinGuardNudgeOutcome.AdmissionRejected failure
            | HostSessionNudge.IdleContinuationOutcome.Retired ->
                releaseKey ()
                return JoinGuardNudgeOutcome.Superseded
            | HostSessionNudge.IdleContinuationOutcome.NotSent _ ->
                releaseKey ()
                return JoinGuardNudgeOutcome.NotSent
            | HostSessionNudge.IdleContinuationOutcome.Failed error ->
                releaseKey ()
                return JoinGuardNudgeOutcome.Failed error
        }

    let private nudgeWithJournal
        (sessionPort: ISessionHostPort)
        (durable: AgentJournal)
        (nudgeKeys: HashSet<string>)
        (physicalAdmission: unit -> Result<unit, QuiescencePermitFailure>)
        (releaseAdmission: unit -> Result<unit, QuiescencePermitFailure>)
        (sessionId: SessionId)
        (terminalProviderRun: ProviderRunIdentity)
        (directory: string option)
        : Task<JoinGuardNudgeOutcome> =
        task {
            let key = nudgeKey sessionId terminalProviderRun
            let reserved = reserveNudge durable nudgeKeys sessionId terminalProviderRun key

            if not reserved then
                return JoinGuardNudgeOutcome.AlreadyOutstanding
            else
                return!
                    sendReservedNudge
                        sessionPort
                        durable
                        nudgeKeys
                        physicalAdmission
                        releaseAdmission
                        key
                        sessionId
                        terminalProviderRun
                        directory
        }

    /// Send JoinGuard Continuation. The business caller has already proven that
    /// background work remains outstanding. Transport dedupes only the exact
    /// terminal occasion and consumes the fresh idle permit at physical send.
    let nudge
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        (physicalAdmission: unit -> Result<unit, QuiescencePermitFailure>)
        (releaseAdmission: unit -> Result<unit, QuiescencePermitFailure>)
        (sessionId: SessionId)
        (terminalProviderRun: ProviderRunIdentity)
        (directory: string option)
        : Task<JoinGuardNudgeOutcome> =
        task {
            match journal with
            | None -> return JoinGuardNudgeOutcome.Failed "Join guard nudge requires an AgentJournal"
            | Some durable ->
                return!
                    nudgeWithJournal
                        sessionPort
                        durable
                        nudgeKeys
                        physicalAdmission
                        releaseAdmission
                        sessionId
                        terminalProviderRun
                        directory
        }
