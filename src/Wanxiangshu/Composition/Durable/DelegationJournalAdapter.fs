namespace Wanxiangshu.Composition.Durable

open System.Threading.Tasks

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
          Sha256 = HostDigest.sha256Hex
          AppendAuthorityTerminalSource =
            fun _ _ ->
                Task.FromResult(Error "AppendAuthorityTerminalSource is not supported by DelegationJournalAdapter") }

module TaskDeliveryJournal =
    let tryFindForWork journal sessionId logicalRunId authorityRoot physicalUserMessageId task =
        TaskDelivery.tryFindForWork
            sessionId
            logicalRunId
            authorityRoot
            physicalUserMessageId
            task
            (AgentJournal.snapshot journal).AgentProjections.Deliveries

    /// 精确围栏读取：调用方持有完整围栏时使用，围栏其余字段不一致即拒绝。
    let tryFindByFence (journal: AgentJournal) (fence: TaskDeliveryFence) =
        TaskDelivery.tryFindByFence fence (AgentJournal.snapshot journal).AgentProjections.Deliveries

    /// 围栏子集读取：调用方只掌握部分围栏字段时使用；
    /// 同 (SessionId, ProviderRun) 下多条见证不再整体丢弃证据，材料不一致时仍拒绝。
    let tryFindByFenceFields
        (journal: AgentJournal)
        (sessionId: Wanxiangshu.Foundation.Identity.SessionId)
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId option)
        (authorityRoot: Wanxiangshu.Foundation.Identity.AuthorityRootUserMessageId option)
        (physicalUserMessageId: Wanxiangshu.Foundation.Identity.PhysicalUserMessageId option)
        (logicalRunId: Wanxiangshu.Foundation.Identity.LogicalRunId option)
        (task: Wanxiangshu.Host.TaskDeliveryTask option)
        =
        TaskDelivery.tryFindByFenceFields
            sessionId
            providerRun
            toolCallId
            authorityRoot
            physicalUserMessageId
            logicalRunId
            task
            (AgentJournal.snapshot journal).AgentProjections.Deliveries

    /// 粗粒度读取：只知道 (SessionId, ProviderRun) 时委托围栏子集读取。
    let tryFind
        (journal: AgentJournal)
        (sessionId: Wanxiangshu.Foundation.Identity.SessionId)
        (providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        =
        tryFindByFenceFields journal sessionId providerRun None None None None None

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

    let prepareChild journal sessionId physicalUserMessageId providerRun toolCallId formalText declaration view =
        result {
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
                |> ChatExecutionProjection.byKey
                    { SessionId = sessionId
                      PhysicalUserMessageId = physicalUserMessageId }
                |> Option.bind (function
                    | ChatExecutionState.Started started
                    | ChatExecutionState.EndedAfterStart(started, ChatExecutionTerminalDisposition.Completed) ->
                        Some started
                    | _ -> None)
                |> Result.requireSome "delivery requires one exact started provider execution"

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

            return witness, profile.CanonicalRole
        }
