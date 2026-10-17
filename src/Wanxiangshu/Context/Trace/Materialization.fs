namespace Wanxiangshu.Context.Trace

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection

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

    let tryHostToolResult
        (messageId: TranscriptMessageAddress)
        (partId: HostToolPartId option)
        (callId: ToolCallId)
        (xTrace: XTraceProjectionState)
        : Task<Result<string option, string>> =
        let candidates =
            XTraceProjection.partsForHostMessageIds (Set.singleton (TranscriptMessageAddress.value messageId)) xTrace
            |> List.filter (fun part ->
                part.Kind = "tool_result"
                && part.HostToolPartId = partId
                && part.ToolCallId = Some callId)

        taskResult {
            match candidates with
            | [] -> return None
            | [ part ] ->
                let! body = XTraceProjection.partText part.Provenance xTrace
                return Some body
            | _ -> return! Error "XTrace original Host tool result identity is ambiguous"
        }

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
        (xTrace: XTraceProjectionState)
        (part: XTracePartRef)
        : Task<Result<ProviderProjection.SemanticPart, string>> =
        XTraceProjection.partText part.Provenance xTrace
        |> Result.bind (semanticPart part)
        |> Task.FromResult

    let private readTurn
        (xTrace: XTraceProjectionState)
        (_turn, parts: XTracePartRef list)
        : Task<Result<ProviderProjection.SemanticMessage, string>> =
        taskResult {
            let role =
                parts
                |> List.tryHead
                |> Option.map (fun part -> part.Role)
                |> Option.defaultValue "user"

            let! semanticParts = parts |> TaskResultList.traverseM (readSemanticPart xTrace)

            return
                { ProviderProjection.SemanticMessage.Role = role
                  ProviderProjection.SemanticMessage.Parts = semanticParts }
        }

    let private projectionFromParts
        (xTrace: XTraceProjectionState)
        (parts: XTracePartRef list)
        : Task<Result<ProviderProjection.ProviderSemanticProjection, string>> =
        taskResult {
            let turns =
                parts |> XTraceProjection.canonicalParts |> List.groupBy (fun part -> part.Turn)

            let! messages = turns |> TaskResultList.traverseM (readTurn xTrace)
            return { empty with Messages = messages }
        }

    /// Rebuild the current reanchor generation from durable XTrace only.
    /// Missing material is a proof failure, not permission to omit history.
    let currentProjection
        (xTrace: XTraceProjectionState)
        : Task<Result<ProviderProjection.ProviderSemanticProjection, string>> =
        XTraceProjection.currentGenerationParts (XTraceProjection.parts xTrace)
        |> projectionFromParts xTrace

    /// Canonical current-generation semantic projection restricted to a
    /// trace-owned half-open interval.
    let currentProjectionBetween
        (range: XTraceRange)
        (xTrace: XTraceProjectionState)
        : Task<Result<ProviderProjection.ProviderSemanticProjection, string>> =
        XTraceProjection.traceIntervalOfRange range xTrace |> ignore

        XTraceProjection.currentGenerationParts (XTraceProjection.parts xTrace)
        |> List.filter (fun part -> XTraceRange.contains part.Cursor range)
        |> projectionFromParts xTrace

    let private readItem (xTrace: XTraceProjectionState) (part: XTracePartRef) : Task<Result<XTraceItem, string>> =
        taskResult {
            let! semantic = readSemanticPart xTrace part

            return
                { Cursor = part.Cursor
                  Provenance = part.Provenance
                  Role = part.Role
                  Part = semantic }
        }

    /// Resolve one range to the owner model used by deterministic rendering.
    /// Missing material fails closed rather than silently shortening proof.
    let materializeRange (range: XTraceRange) (xTrace: XTraceProjectionState) : Task<Result<XTraceItem list, string>> =
        task {
            XTraceProjection.traceIntervalOfRange range xTrace |> ignore

            let selected =
                XTraceProjection.parts xTrace
                |> List.filter (fun part -> XTraceRange.contains part.Cursor range)

            return! selected |> TaskResultList.traverseM (readItem xTrace)
        }

    let materializeWorkRecordRange
        (range: XTraceRange)
        (xTrace: XTraceProjectionState)
        : Task<Result<XTraceItem list, string>> =
        task {
            let! resolved = materializeRange range xTrace
            return resolved |> Result.map XTrace.forWorkRecord
        }

    let renderRange (range: XTraceRange) (xTrace: XTraceProjectionState) : Task<Result<string, string>> =
        task {
            let! resolved = materializeRange range xTrace
            return resolved |> Result.map XTrace.render
        }

    let renderWorkRecordRange (range: XTraceRange) (xTrace: XTraceProjectionState) : Task<Result<string, string>> =
        task {
            let! resolved = materializeWorkRecordRange range xTrace
            return resolved |> Result.map XTrace.render
        }
