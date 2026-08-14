namespace Wanxiangshu.Execution.Delegation.Fork.Host

open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Threading.Tasks
open Wanxiangshu.OpenCode
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
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal

/// EXEC-002 busy-agent nudge, as a PROMPT-003 Continuation.
module HostForkBusyNudge =

    /// Continuation of the child's active Logical Run. Never creates a new
    /// Authority Root / RunId / completion.
    ///
    /// No journal means no Dispatcher, and PROMPT-005 admits no second sender: this
    /// used to fall through to `sessions.SendChildPromptFireAndForget`, which reaches
    /// the Host prompt endpoint directly with no claim, no PromptKey and no recovery
    /// anchor — the exact bypass package A removed elsewhere. It fails closed instead.
    let send
        (sessions: ISessionHostPort)
        (_parentId: SessionId)
        (journal: AgentJournal option)
        (childId: SessionId)
        (_role: Role)
        (agent: string)
        (directory: string option)
        (prompt: string)
        : Task<Result<unit, string>> =
        task {
            match journal with
            | None ->
                return Error "Busy nudge requires an AgentJournal: PROMPT-005 admits no sender outside the Dispatcher"
            | Some j ->
                let snapshot = AgentJournal.snapshot j

                match PromptAuthorityLedger.activeProfile childId snapshot.AgentProjections with
                | None -> return Error "Busy nudge requires ActiveLogicalRun on child session"
                | Some profile ->
                    // In-flight nudge must keep the handle's managed agent.
                    // Replacing it with the fallback Peer would switch Deep → Fast
                    // mid-conversation (prefix break + unjustified downgrade).
                    // Empty / unknown names keep SelectedAgent; they never follow
                    // the cursor and never invent fast-ROLE.
                    let busyAgent =
                        let trimmed = if String.IsNullOrWhiteSpace agent then "" else agent.Trim()

                        if trimmed = profile.SelectedAgent || trimmed = profile.PeerAgent then
                            trimmed
                        else
                            profile.SelectedAgent

                    let rt = PromptDispatcher.forJournal j

                    // Busy-nudge requirement only: commentize multi-line guidance.
                    // Do not wrap an already-rendered ForkChildPayload on this path.
                    let syntheticPrompt = SyntheticToml.document [ prompt ] []

                    // PROMPT-007 Detached: busy nudge does not wait for PhysicalAccepted.
                    let! sent =
                        rt.SendContinuation
                            sessions
                            childId
                            syntheticPrompt
                            PromptAuthority.ContinuationKind.BusyAgentNudge
                            profile
                            busyAgent
                            directory
                            PromptDispatcher.AwaitMode.Detached
                            None

                    match sent with
                    | Ok _ -> return Ok()
                    | Error err -> return Error err
        }

    let sender sessions parentId journal directoryOf =
        fun agentId childId (role: Role) agent prompt ->
            send sessions parentId journal childId role agent (directoryOf agentId) prompt
