namespace Wanxiangshu.OpenCode

open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Physical runtime cleanup before Application turn observation (rabbit §19).
/// Prompt-authority registration for a linked child stays here as Host-side
/// durable preparation — not business outcome routing.
module TurnRuntimePreparation =

    /// Dispose executor runtime for the turn session and ensure linked-child
    /// prompt authority is registered when TerminalPolicy exposes one.
    let prepare (journal: AgentJournal option) (disposeExecutorRuntime: string -> unit) (turn: ReconciledTurn) =
        let sessionKey = SessionId.value turn.SessionId
        disposeExecutorRuntime sessionKey

        TerminalPolicy.tryLinkedChild journal sessionKey
        |> Option.iter (fun record ->
            HostSessionNudge.ensureAgentOwnerAuthority
                journal
                turn.SessionId
                turn.PhysicalUserMessageId
                record.TargetAgent
            |> ignore)
