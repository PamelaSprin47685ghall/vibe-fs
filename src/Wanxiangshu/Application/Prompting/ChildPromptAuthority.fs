namespace Wanxiangshu.OpenCode

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Domain
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Host
open Wanxiangshu.Kernel.Identity

/// Application ownership of linked-child prompt authority (rabbit §19).
/// Physical runtime cleanup must not decide who owns a Logical Run.
module ChildPromptAuthority =

    /// Register AgentOwnerRoot authority for one proven linked child, idempotently.
    /// The durable handle is the only source of TargetAgent; no role-to-agent rebuild.
    let ensureForLinkedChild
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : System.Threading.Tasks.Task<Result<unit, string>> =
        task {
            match journal with
            | None -> return Ok()
            | Some durable ->
                let snapshot = AgentJournal.snapshot durable

                match Map.tryFind turn.SessionId snapshot.AgentProjections.HandleByChildSession with
                | None -> return Ok()
                | Some handle ->
                    match PromptAuthorityLedger.activeProfile turn.SessionId snapshot.AgentProjections with
                    | Some _ -> return Ok()
                    | None ->
                        let runtime = PromptDispatcher.forJournal durable

                        match
                            PromptAuthorityRun.createAuthorityRoot
                                HostDigest.sha256Hex
                                runtime.RuntimeId
                                turn.SessionId
                                PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                                turn.PhysicalUserMessageId
                                handle.TargetAgent
                        with
                        | Error error -> return Error error
                        | Ok profile -> return! runtime.RegisterAuthority profile
        }
