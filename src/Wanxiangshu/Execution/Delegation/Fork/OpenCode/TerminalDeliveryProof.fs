namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Repository.Programming.Js

/// The verification Host drives the real delivery workflow for one accepted child input.
module TerminalDeliveryProof =
    let materialize (journal: AgentJournal) directory sessionId physical provider text : Task =
        task {
            let projection = (AgentJournal.snapshot journal).AgentProjections

            let profile =
                PromptAuthorityProjectionQueries.activeProfile sessionId projection
                |> Option.defaultWith (fun () -> invalidOp "terminal delivery requires active child authority")

            let binding =
                Map.tryFind sessionId projection.HandleByChildSession
                |> Option.defaultWith (fun () -> invalidOp "terminal delivery requires a durable child binding")

            let evidence =
                ManagedChatAcceptance.evidenceFromIntent
                    profile
                    physical
                    (PromptOrigin.AuthorityRoot profile.AuthorityKind)

            let key: ChatExecutionKey =
                { SessionId = sessionId
                  PhysicalUserMessageId = physical }

            let! accepted = Wanxiangshu.Composition.Durable.ManagedChatAcceptance.accept journal key evidence
            accepted |> Result.defaultWith (fun error -> failwithf "%A" error) |> ignore

            let providerName = ProviderRunIdentity.value provider
            let toolCall = ToolCallId.create (providerName + "-delivery")

            let! started =
                ManagedChatProviderLifecycle.providerStarted journal key evidence provider ProviderRequestKind.WorkMain

            started |> Result.defaultWith (fun error -> failwithf "%A" error) |> ignore

            let settlement =
                DeliverySettlement.Attributed(
                    { LogicalRunId = profile.LogicalRunId
                      AuthorityRoot = profile.AuthorityRootUserMessageId
                      PhysicalUserMessageId = physical
                      Task = DeliveryTaskReference.ChildWork(HandleId.describe binding.Handle)
                      RetirementId = None
                      FormalText = text },
                    DeliveryWitnessWriter.create journal ignore
                )

            let surface =
                JsGeneratorSurface.typedRole (Roles.roleLabel profile.CanonicalRole) "en"
                |> Option.defaultWith (fun () -> invalidOp "terminal delivery requires a generated child surface")

            let material: string = emitJsExpr text "JSON.stringify($0)"

            let source =
                "class Js extends JsProgram { async run() { await this.deliver({ 0: "
                + material
                + " }); } }"

            let! delivered =
                JsToolWorkflow.executeWithReviewCheckAndDelivery
                    None
                    (Some settlement)
                    (SessionId.value sessionId)
                    (Some(provider, toolCall))
                    surface.Capabilities
                    directory
                    surface.BaseClassSource
                    (createObj [ "program" ==> source ])
                    5000
                    (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 5000L)
                    (1024 * 1024)
                    None
                    None
                    None
                    None

            match delivered with
            | JsToolWorkflow.JsToolOutcome.Succeeded _ -> ()
            | _ -> invalidOp (JsToolsResult.render delivered)
        }
        :> Task
