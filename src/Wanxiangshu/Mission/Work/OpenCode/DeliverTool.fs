namespace Wanxiangshu.Mission.Work.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Host
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Mission.Work
open Wanxiangshu.OpenCode

/// Thin OpenCode adapter for the `deliver` tool.
module DeliverTool =

    let description () = DeliverPolicy.description ()

    let recordedFenceReplay
        (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork)
        (providerRunId: string)
        (toolCallId: string)
        : Result<string, string> option =
        DeliverPolicy.recordedFenceReplay work providerRunId toolCallId

    let private owningPhysical
        (messages: SessionMessage list)
        (providerRun: ProviderRunIdentity)
        (toolCallId: ToolCallId)
        =
        messages
        |> List.tryFind (fun message ->
            message.Role = "assistant"
            && message.Id = ProviderRunIdentity.value providerRun
            && (message.ToolParts
                |> Array.exists (fun part -> part.ToolCallId = toolCallId && part.ToolName = "deliver")))
        |> Option.bind (fun message -> message.ParentId)
        |> Option.filter (fun parent ->
            not (String.IsNullOrWhiteSpace parent)
            && (messages
                |> List.exists (fun message -> message.Id = parent && message.Role = "user")))
        |> Option.map PhysicalUserMessageId.create

    let private buildRuntime (scope: ToolRuntimeScope) (ctx: HostToolContext) : DeliveryRuntime =
        { Role = scope.RoleFor ctx
          ToolCallId = ctx.ToolCallId
          ProviderRun = ctx.ProviderRunId
          OwningPhysicalUserMessage =
            fun sessionId providerRun toolCallId ->
                task {
                    match scope.Snapshot with
                    | None -> return None
                    | Some snapshot ->
                        let! loaded = snapshot.GetMessages sessionId

                        return
                            loaded
                            |> Result.toOption
                            |> Option.bind (fun messages -> owningPhysical messages providerRun toolCallId)
                }
          WorkspaceDirectory = scope.WorkspaceDirectory
          Journal = scope.Journal
          Snapshot = scope.Snapshot
          HasActiveIncumbency =
            fun sessionId ->
                task {
                    let! facts = scope.ManagerCapabilityFactsFor(SessionId.value sessionId)
                    return facts.HasActiveIncumbency
                }
          CurrentPhysicalUserMessage = fun sessionId -> scope.CurrentPhysicalUserMessage(SessionId.value sessionId)
          FreezeRetirement = fun sessionId incumbent -> scope.TryFreezeRetirement(SessionId.value sessionId, incumbent)
          UnfreezeRetirement = fun sessionId -> scope.UnfreezeRetirement(SessionId.value sessionId)
          RetirementBlockers = fun sessionId -> scope.RetirementBlockersFor(SessionId.value sessionId)
          RegisterEstablishedDelivery =
            fun sessionId providerRun callId physical ->
                scope.RegisterEstablishedDelivery(sessionId, providerRun, callId, physical)
          ContinueManagerLoop = fun sessionId directory -> scope.ContinueManagerLoop(sessionId, directory) }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "deliver"
          Description = DeliverPolicy.description ()
          Arguments = []
          Execute = fun _args ctx -> DeliveryFlow.execute (buildRuntime scope ctx) (SessionId.create ctx.SessionId) }
