namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

/// Factory for the single runtime PromptAuthorityService.
module PromptDispatcher =

    let private services = Dictionary<string, PromptAuthorityService>()
    let private serviceGate = obj ()

    let forRuntime (runtimeId: string) (journal: AgentJournal option) =
        let key =
            match journal with
            | Some j -> "journal:" + RuntimeId.value (AgentJournal.runtimeId j)
            | None -> "ephemeral:" + runtimeId

        lock serviceGate (fun () ->
            match services.TryGetValue key with
            | true, service -> service
            | false, _ ->
                let service = PromptAuthorityService(runtimeId, ?journal = journal)
                services.[key] <- service
                service)

    let forJournal (journal: AgentJournal) =
        forRuntime (RuntimeId.value (AgentJournal.runtimeId journal)) (Some journal)

    let ephemeral () =
        forRuntime ("ephemeral-" + Guid.NewGuid().ToString("N")) None

    let ephemeralNamed (runtimeId: string) =
        forRuntime runtimeId None

    /// Backward-compatible constructor used by tests that only need a local
    /// dispatcher. Production code must pass the runtime journal service.
    type Dispatcher(?journal: AgentJournal) =
        inherit PromptAuthorityService(
            (match journal with
             | Some j -> RuntimeId.value (AgentJournal.runtimeId j)
             | None -> "test-runtime"),
            ?journal = journal
        )
