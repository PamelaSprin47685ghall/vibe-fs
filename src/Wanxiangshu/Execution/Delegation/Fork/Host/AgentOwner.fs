namespace Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
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

open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

module HostForkAgentOwner =

    /// PROMPT-005: the first prompt of an AgentOwnerRoot work unit.
    ///
    /// Returns the `PromptKey`, not a message id. At send time no physical message
    /// exists — that is the whole point of the four-fact protocol — and the key is
    /// what lets the caller recognise the message when `chat.message` delivers it.
    ///
    /// A `None` journal is an error rather than a direct-send fallback. The old
    /// fallback issued a real prompt with `Metadata = None`, so it carried no
    /// PromptKey: PROMPT-011 had no anchor to recover it by, and PromptIngress
    /// could only ever classify the reply as UnknownOrigin.
    let sendFirstPrompt
        (sessions: ISessionHostPort)
        (journal: AgentJournal option)
        (childId: SessionId)
        (agent: string)
        (directory: string option)
        (prompt: string)
        : Task<Result<PromptKey, string>> =
        task {
            match journal with
            | None -> return Error "No journal: an AgentOwnerRoot prompt cannot be claimed"
            | Some durable ->
                let dispatcher = PromptDispatcher.forJournal durable
                // PROMPT-007 Detached: child owner root does not wait for PhysicalAccepted.
                return!
                    dispatcher.SendAgentOwnerRoot
                        sessions
                        childId
                        prompt
                        agent
                        directory
                        PromptDispatcher.AwaitMode.Detached
                        None
        }
