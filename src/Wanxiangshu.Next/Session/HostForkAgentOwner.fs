namespace Wanxiangshu.Next.Session

open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

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
