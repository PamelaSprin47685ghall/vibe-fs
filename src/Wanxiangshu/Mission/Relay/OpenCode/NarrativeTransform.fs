namespace Wanxiangshu.Mission.Relay.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Relay
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Persistence.Journal

module RelayNarrativeTransform =
    let private relayRoad (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.Relay)
        |> Option.bind (fun relay -> Fold.view relay (RoadId.create (SessionId.value sessionId)))

    let private rootAuthorityId (journal: AgentJournal) (sessionId: SessionId) =
        let projection = AgentJournal.snapshot journal

        PromptAuthorityLedger.activeProfile sessionId projection.AgentProjections
        |> Option.orElseWith (fun () -> PromptAuthorityLedger.lastAuthorityProfile sessionId projection.AgentProjections)
        |> Option.map (fun profile -> AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId)

    let private messageId message = ProviderWireDecode.hostMessageId message

    let private messageContainsToolCall callId message =
        ProviderWireDecode.rawPartsOf message
        |> List.choose ProviderWireDecode.decodePart
        |> List.exists (function
            | WireToolCall(toolCallId, _, _)
            | WireToolResult(toolCallId, _) -> ToolCallId.value toolCallId = callId
            | _ -> false)

    let private cutMessages rootId (cut: ProjectionCut) messages =
        match messages |> List.tryFindIndex (fun message -> messageId message = Some cut.ThroughProviderRunId) with
        | None -> Error("relay projection cut provider run is absent: " + cut.ThroughProviderRunId)
        | Some cutIndex ->
            let stale = cut.StaleProviderRunIds |> Set.ofList

            messages
            |> List.mapi (fun index message -> index, message)
            |> List.choose (fun (index, message) ->
                let id = messageId message
                let keepRoot = id = rootId
                let oldEpoch = index <= cutIndex && not keepRoot
                let staleRun = id |> Option.exists (fun value -> Set.contains value stale)
                let retirementTool = messageContainsToolCall cut.ThroughToolCallId message
                if oldEpoch || staleRun || retirementTool then None else Some message)
            |> Ok

    let private phaseName =
        function
        | IncumbencyPhase.AuditPending -> "AuditPending"
        | IncumbencyPhase.WorkOwned -> "WorkOwned"
        | IncumbencyPhase.PerfectAwaitingRetirement -> "PerfectAwaitingRetirement"
        | IncumbencyPhase.RetirementCleanupBlocked -> "RetirementCleanupBlocked"

    let private batonText (road: RoadView) =
        let latest = road.LatestRetirement |> Option.map (fun retirement -> retirement.Baton)

        let values =
            [ "authority_revision=" + AuthorityRevision.value road.AuthorityRevision
              "incumbency_id="
              + (road.ActiveIncumbency |> Option.map IncumbencyId.value |> Option.defaultValue "none")
              "phase=" + (road.ActivePhase |> Option.map phaseName |> Option.defaultValue "Retired")
              "open_quality_obligations="
              + (latest
                 |> Option.map (fun baton -> String.concat "," baton.OpenObligations)
                 |> Option.defaultValue "")
              "evidence_refs="
              + (latest
                 |> Option.map (fun baton -> String.concat "," baton.EvidenceRefs)
                 |> Option.defaultValue "") ]

        String.concat "\n" ([ "[RelayContext]" ] @ values @ [ "[/RelayContext]" ])

    let private syntheticContext (sessionId: SessionId) road =
        let identity =
            road.ActiveIncumbency |> Option.map IncumbencyId.value |> Option.defaultValue "retired"

        createObj
            [ "info",
              box (
                  createObj
                      [ "id", box ("relay-context:" + identity)
                        "sessionID", box (SessionId.value sessionId)
                        "role", box "user" ]
              )
              "role", box "user"
              "parts", box [| createObj [ "type", box "text"; "text", box (batonText road) ] |] ]

    let private project journal sessionId road outObj =
        let messages = ProviderWireDecode.messagesFromTransformOutput outObj
        let rootId = rootAuthorityId journal sessionId

        let projected =
            match road.ActiveSource, road.LatestRetirement with
            | Some(BatonSource.Retirement predecessor), Some retirement when retirement.Id = predecessor ->
                cutMessages rootId retirement.ProjectionCut messages
            | _ -> Ok messages

        match projected with
        | Error error -> raise (InvalidOperationException error)
        | Ok current ->
            HostMessageProjection.replaceMessagesInPlace outObj (syntheticContext sessionId road :: current)

    let apply (journal: AgentJournal option) (sessionIdValue: string option) (outObj: obj) : Task<unit> =
        task {
            match journal, sessionIdValue with
            | Some durable, Some value when not (String.IsNullOrWhiteSpace value) ->
                let sessionId = SessionId.create value

                match relayRoad durable sessionId with
                | Some road -> project durable sessionId road outObj
                | None -> ()
            | _ -> ()
        }
