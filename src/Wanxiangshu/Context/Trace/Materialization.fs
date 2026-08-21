namespace Wanxiangshu.Context.Trace

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.Journal

/// Durable XTrace -> canonical X semantic projection.
///
/// Request-local provider presentation is intentionally absent from this owner.
/// Blogger coverage and prefix proof both consume this projection, so a later
/// transform may rewrite/inject presentation without changing what historical X
/// the durable trace proves actually happened.
[<RequireQualifiedAccess>]
module XTraceMaterialization =

    let empty: ProviderProjection.ProviderSemanticProjection =
        { ProviderId = None
          ModelId = None
          Variant = None
          Tools = []
          System = []
          Messages = [] }

    let private semanticPart (part: XTracePartRef) (body: string) =
        match part.Kind with
        | "text" -> Ok(ProviderProjection.SemanticText body)
        | "reasoning" -> Ok(ProviderProjection.SemanticReasoning body)
        | "tool_call" ->
            part.ToolName
            |> Option.map (fun name -> ProviderProjection.SemanticToolCall(name, body))
            |> Result.requireSome "XTrace tool_call is missing its tool name"
        | "tool_result" -> Ok(ProviderProjection.SemanticToolResult body)
        | "media_omitted" ->
            let mediaType = if String.IsNullOrWhiteSpace body then None else Some body
            Ok(ProviderProjection.SemanticMedia(mediaType, ""))
        | kind -> Error(sprintf "XTrace contains an unknown semantic part kind: %s" kind)

    let private readSemanticPart
        (journal: AgentJournal)
        (part: XTracePartRef)
        : Task<Result<ProviderProjection.SemanticPart, string>> =
        task {
            let! body = journal.Writer.BlobWriter.Read part.TextRef

            return
                body
                |> Result.mapError (fun error ->
                    sprintf "XTrace materialization could not read %s: %s" (BlobRef.value part.TextRef) error)
                |> Result.bind (semanticPart part)
        }

    let private readTurn
        (journal: AgentJournal)
        (_turn, parts: XTracePartRef list)
        : Task<Result<ProviderProjection.SemanticMessage, string>> =
        taskResult {
            let ordered = parts |> List.sortBy (fun part -> part.PartIndex)

            let role =
                ordered
                |> List.tryHead
                |> Option.map (fun part -> part.Role)
                |> Option.defaultValue "user"

            let! semanticParts = ordered |> TaskResultList.traverseM (readSemanticPart journal)

            return
                { ProviderProjection.SemanticMessage.Role = role
                  ProviderProjection.SemanticMessage.Parts = semanticParts }
        }

    /// Rebuild the current reanchor generation from durable XTrace only.
    /// Blob failure is a proof failure, not permission to silently omit history.
    let currentProjection
        (journal: AgentJournal)
        (xTrace: XTraceProjectionState)
        : Task<Result<ProviderProjection.ProviderSemanticProjection, string>> =
        taskResult {
            let turns =
                XTraceProjection.currentGenerationParts (XTraceProjection.parts xTrace)
                |> List.groupBy (fun part -> part.Turn)
                |> List.sortBy fst

            let! messages = turns |> TaskResultList.traverseM (readTurn journal)

            return { empty with Messages = messages }
        }
