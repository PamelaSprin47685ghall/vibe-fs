namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Persistence.Journal.JournalOutcome
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Interaction.Authority
open FsToolkit.ErrorHandling

[<RequireQualifiedAccess>]
module DelegationJournalAdapter =
    let fromAgentJournal (journal: AgentJournal) : AgentJournalPort =
        { AppendExecutionFact =
            fun sessionId fact ->
                task {
                    match!
                        AgentJournal.appendAgent (StreamId.Session sessionId) None (AgentFact.Execution fact) journal
                    with
                    | Ok _ -> return Ok()
                    | Error failure -> return Error(JournalAppendFailure.describe failure)
                }
          RefreshProjection = fun () -> AgentJournal.refreshCurrent journal
          HandleProjection = fun sessionId -> AgentJournal.handleProjection journal sessionId
          ReadBlob = fun blobRef -> journal.Writer.BlobWriter.Read blobRef
          WriteBlob =
            fun content ->
                task {
                    match! journal.WriteBlob content with
                    | Ok receipt -> return Ok(receipt.BlobRef, receipt.BlobDigest)
                    | Error err -> return Error err
                }
          Sha256 = HostDigest.sha256Hex }

module TaskDeliveryJournal =
    let tryFind (journal: AgentJournal) sessionId providerRun =
        TaskDelivery.tryFind sessionId providerRun (AgentJournal.snapshot journal).AgentProjections.Deliveries

    let commit (journal: AgentJournal) (witness: TaskDeliveryWitness) =
        task {
            match TaskDelivery.validate HostDigest.sha256Hex witness with
            | Error reason -> return Error reason
            | Ok() ->
                let! appended =
                    AgentJournal.appendAgent
                        (StreamId.Session witness.Fence.SessionId)
                        (Some witness.Fence.ProviderRun)
                        (AgentFact.Host(HostFactCases.TaskDeliveryCommitted witness))
                        journal

                return appended |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
        }

    let commitChild journal sessionId physicalUserMessageId providerRun toolCallId formalText declaration view =
        taskResult {
            let projection = (AgentJournal.snapshot journal).AgentProjections

            let! binding =
                Map.tryFind sessionId projection.HandleByChildSession
                |> Result.requireSome "delivery requires the child's durable binding"

            let! profile =
                PromptAuthorityProjectionQueries.activeProfile sessionId projection
                |> Result.requireSome "delivery requires the active task authority"

            let! parentId, _, _ =
                PromptAuthority.identitySeedOwner profile.IdentitySeed
                |> Result.requireSome "delivery requires the original task owner"

            let! work =
                AgentJournal.handleProjection journal parentId
                |> HandleProjection.tryAdmittedWork
                    { Handle = binding.Handle
                      ChildSessionId = sessionId
                      AuthorityRoot = profile.AuthorityRootUserMessageId }
                |> Result.mapError (sprintf "delivery work admission is invalid: %A")

            let! started =
                projection.ChatExecutions
                |> ChatExecutionProjection.nonTerminal
                |> List.choose _.startedEvidence
                |> List.filter (fun started ->
                    started.Accepted.SessionId = sessionId
                    && started.Accepted.PhysicalUserMessageId = physicalUserMessageId)
                |> function
                    | [ started ] -> Ok started
                    | _ -> Error "delivery requires one exact active provider execution"

            let accepted = started.Accepted

            do!
                Result.requireTrue
                    "delivery provider does not own the active task"
                    (accepted.LogicalRunId = AdmittedWork.logicalRunId work
                     && accepted.LogicalRunId = profile.LogicalRunId
                     && accepted.AuthorityRootUserMessageId = profile.AuthorityRootUserMessageId
                     && accepted.IdentitySeed = profile.IdentitySeed)

            let witness =
                { Fence =
                    { SessionId = sessionId
                      LogicalRunId = accepted.LogicalRunId
                      AuthorityRoot = accepted.AuthorityRootUserMessageId
                      PhysicalUserMessageId = accepted.PhysicalUserMessageId
                      ProviderRun = providerRun
                      ToolCallId = toolCallId
                      Task = TaskDeliveryTask.ChildWork(HandleId.describe binding.Handle) }
                  FormalText = formalText
                  ViewDeclaration = declaration
                  View = view
                  MaterialDigest = HostDigest.sha256Hex view
                  RetirementId = None }

            do! commit journal witness
            return witness, profile.CanonicalRole
        }
