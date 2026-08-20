namespace Wanxiangshu.Context.Companion.Blogger

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module BloggerMainContext =

    let private openingFloor (journal: AgentJournal option) (mainSessionId: SessionId) =
        journal
        |> Option.bind (fun durable ->
            ManagerOpeningFloor.floorSequence mainSessionId (AgentJournal.snapshot durable).AgentProjections)

    let private nextChunk
        (journal: AgentJournal option)
        (mainSessionId: SessionId)
        (blog: BlogProjectionState)
        (xTrace: XTraceProjectionState)
        (projection: ProviderProjection.ProviderSemanticProjection)
        =
        let effectiveIngested =
            openingFloor journal mainSessionId
            |> Option.map (max blog.Coverage.IngestedThroughSequence)
            |> Option.defaultValue blog.Coverage.IngestedThroughSequence

        BloggerDelta.nextChunk
            BloggerDelta.DeltaLimitBytes
            (XTraceProjection.semanticCursorFor effectiveIngested xTrace)
            blog.Coverage.CoverableTurnCutoffExclusive
            projection.Messages

    let hasMaterial
        (journal: AgentJournal option)
        (mainSessionId: SessionId)
        (blog: BlogProjectionState)
        (xTrace: XTraceProjectionState)
        (projection: ProviderProjection.ProviderSemanticProjection)
        =
        nextChunk journal mainSessionId blog xTrace projection |> Option.isSome

    let fromProjection
        (journal: AgentJournal option)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (observedEpoch: PrefixEpochId)
        (blog: BlogProjectionState)
        (xTrace: XTraceProjectionState)
        (projection: ProviderProjection.ProviderSemanticProjection)
        : BloggerRequestContext option =
        nextChunk journal mainSessionId blog xTrace projection
        |> Option.bind (EnforcerHost.mainContextFromChunk mainSessionId bloggerSessionId observedEpoch blog xTrace projection)

    let private semanticPart (part: XTracePartRef) (body: string) =
        match part.Kind with
        | "text" -> Some(ProviderProjection.SemanticText body)
        | "reasoning" -> Some(ProviderProjection.SemanticReasoning body)
        | "tool_call" -> part.ToolName |> Option.map (fun name -> ProviderProjection.SemanticToolCall(name, body))
        | "tool_result" -> Some(ProviderProjection.SemanticToolResult body)
        | "media_omitted" ->
            let mediaType = if String.IsNullOrWhiteSpace body then None else Some body
            Some(ProviderProjection.SemanticMedia(mediaType, ""))
        | _ -> None

    let private readTurn (journal: AgentJournal) (_turn, parts: XTracePartRef list) =
        task {
            let ordered = parts |> List.sortBy (fun part -> part.PartIndex)
            let role = ordered |> List.tryHead |> Option.map (fun part -> part.Role) |> Option.defaultValue "user"
            let semanticParts = ResizeArray<_>()

            for part in ordered do
                match! journal.Writer.BlobWriter.Read part.TextRef with
                | Ok body -> semanticPart part body |> Option.iter semanticParts.Add
                | Error _ -> ()

            return
                if semanticParts.Count = 0 then
                    None
                else
                    Some
                        { ProviderProjection.SemanticMessage.Role = role
                          ProviderProjection.SemanticMessage.Parts = semanticParts |> Seq.toList }
        }

    let projectionFromXTrace
        (journal: AgentJournal)
        (xTrace: XTraceProjectionState)
        : Task<ProviderProjection.ProviderSemanticProjection> =
        task {
            let turns =
                XTraceProjection.currentGenerationParts (XTraceProjection.parts xTrace)
                |> List.groupBy (fun part -> part.Turn)
                |> List.sortBy fst

            let messages = ResizeArray<_>()

            for turn in turns do
                match! readTurn journal turn with
                | Some message -> messages.Add message
                | None -> ()

            return
                { ProviderId = None
                  ModelId = None
                  Variant = None
                  Tools = []
                  System = []
                  Messages = messages |> Seq.toList }
        }

    let fromJournal
        (scope: IParkedTransformHost)
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        : Task<BloggerRequestContext option> =
        task {
            let key = SessionId.value bloggerSessionId

            if BloggerRuntimeHost.blocksNew (Some journal) mainSessionId scope key then
                return None
            else
                let session =
                    AgentProjection.tryFind mainSessionId (AgentJournal.snapshot journal).AgentProjections
                    |> Option.defaultValue AgentProjection.emptySession

                let blog = session.Blog |> Option.defaultValue BlogProjection.empty
                let xTrace = session.XTrace |> Option.defaultValue XTraceProjection.empty
                let epoch = session.PrefixEpoch |> Option.map (fun prefix -> prefix.EpochId) |> Option.defaultValue PrefixEpochId.initial
                let! projection = projectionFromXTrace journal xTrace
                return fromProjection (Some journal) mainSessionId bloggerSessionId epoch blog xTrace projection
        }
