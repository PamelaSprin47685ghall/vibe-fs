namespace Wanxiangshu.OpenCode

open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Identity

/// Application ownership of linked-child prompt authority (rabbit §19).
/// Physical runtime cleanup must not decide who owns a Logical Run.
module ChildPromptAuthority =

    /// Register AgentOwnerRoot authority for one proven linked child, idempotently.
    /// The durable handle is the only source of TargetAgent; no role-to-agent rebuild.
    let ensureForLinkedChild
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : Result<unit, string> =
        match journal with
        | None -> Ok()
        | Some durable ->
            let snapshot = AgentJournal.snapshot durable

            match Map.tryFind turn.SessionId snapshot.AgentProjections.HandleByChildSession with
            | None -> Ok()
            | Some handle ->
                match PromptAuthorityLedger.activeProfile turn.SessionId snapshot.AgentProjections with
                | Some _ -> Ok()
                | None ->
                    let runtime = PromptDispatcher.forJournal durable

                    PromptAuthorityRun.createAuthorityRoot
                        HostDigest.sha256Hex
                        runtime.RuntimeId
                        turn.SessionId
                        PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                        turn.PhysicalUserMessageId
                        handle.TargetAgent
                    |> Result.bind runtime.RegisterAuthority
