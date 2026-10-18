namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Requirement.Grounding

module RequirementGroundingGate =

    let private emptyDecision =
        { NeedsGrounding = false
          Requested = 0
          Packages = [] }

    let private requestMatched
        (journal: AgentJournal option)
        (workspace: string)
        (sessionId: string)
        (paths: string list)
        : Task<Result<RequirementGroundingDecision, string>> =
        match journal with
        | None -> Task.FromResult(Error "requirement grounding requires a durable journal")
        | Some durable ->
            let port = AgentJournalPortAdapter.forRequirementGrounding durable
            RequirementGroundingRuntime.requestPaths port workspace (SessionId.create sessionId) paths

    let private request
        (journal: AgentJournal option)
        (workspace: string)
        (sessionId: string)
        (paths: string list)
        : Task<Result<RequirementGroundingDecision, string>> =
        let matched =
            if List.isEmpty paths then
                []
            else
                GroundingCatalog.snapshotsForPaths workspace paths

        if List.isEmpty matched then
            Task.FromResult(Ok emptyDecision)
        else
            requestMatched journal workspace sessionId paths

    let private observeReads
        (journal: AgentJournal option)
        (workspace: string)
        (sessionId: string)
        (reads: GroundingFileRead list)
        : Task<Result<RequirementGroundingDecision, string>> =
        match journal with
        | None -> Task.FromResult(Error "requirement grounding requires a durable journal")
        | Some durable ->
            let port = AgentJournalPortAdapter.forRequirementGrounding durable
            RequirementGroundingRuntime.observeFileReads port workspace (SessionId.create sessionId) reads

    let private ignoreDecision (operation: unit -> Task<Result<RequirementGroundingDecision, string>>) : Task<unit> =
        task {
            try
                let! _ = operation ()
                return ()
            with _ ->
                return ()
        }

    let decideMutation
        (journal: AgentJournal option)
        (workspace: string)
        (sessionId: string)
        (paths: string list)
        : Task<Result<RequirementGroundingDecision, string>> =
        request journal workspace sessionId paths

    let decideRead
        (journal: AgentJournal option)
        (workspace: string)
        (sessionId: string)
        (reads: GroundingFileRead list)
        : Task<Result<RequirementGroundingDecision, string>> =
        if List.isEmpty reads then
            Task.FromResult(Ok emptyDecision)
        else
            observeReads journal workspace sessionId reads

    let before
        (journal: AgentJournal option)
        (workspace: string option)
        (toolInput: obj)
        (toolOutput: obj)
        : Task<unit> =
        ignore (journal, workspace, toolInput, toolOutput)
        Task.FromResult(())

    let after
        (journal: AgentJournal option)
        (workspace: string option)
        (toolInput: obj)
        (toolOutput: obj)
        : Task<unit> =
        ignore (journal, workspace, toolInput, toolOutput)
        Task.FromResult(())

    let programObservation
        (journal: AgentJournal option)
        (workspace: string)
        (sessionId: string)
        (reads: GroundingFileRead list)
        (effectPaths: string list)
        : Task<unit> =
        task {
            if not (List.isEmpty reads) then
                do! ignoreDecision (fun () -> decideRead journal workspace sessionId reads)

            if not (List.isEmpty effectPaths) then
                do! ignoreDecision (fun () -> decideMutation journal workspace sessionId effectPaths)
        }
