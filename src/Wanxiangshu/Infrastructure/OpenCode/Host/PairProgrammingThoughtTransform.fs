namespace Wanxiangshu.OpenCode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Kernel.Identity

/// HOST-013：结对编程 guideline marker。
///
/// Injected into the final provider-facing transcript at
/// `experimental.chat.messages.transform`: one final marker before ReviewSeal, so the
/// seal digests the exact bytes the provider receives. XTrace capture runs
/// earlier in the chain, so the marker never enters a work record.
///
/// PROJ-008 Step5：正文/末尾布局经 `InsertPairProgrammingThought` → plan →
/// renderMessagesWithIntents；Host 仅写回 id / source 侧信道。
module PairProgrammingThoughtTransform =

    open Wanxiangshu.Domain

    /// HOST-013 guideline 正文。Domain 单源。
    let text = ProjectionConstants.PairProgrammingGuidelineText

    /// The marker's source identity (HOST-013). Filtering must use this, never
    /// the text: a real user may quote the sentence.
    let source = "pair-programming-guideline"

    let private legacySource = "pair-programming-thought"

    let private idPrefix = "pair-programming-guideline-"

    /// HOST-013：marker 身份仅按 `info.source`。
    let isPairProgrammingThought (rawMsg: obj) : bool =
        if isNull rawMsg then
            false
        else
            match rawMsg?info with
            | null -> false
            | info -> unbox<string> info?source = source

    let private isAnyPairMarker (rawMsg: obj) : bool =
        if isNull rawMsg then
            false
        else
            match rawMsg?info with
            | null -> false
            | info ->
                let markerSource = unbox<string> info?source
                markerSource = source || markerSource = legacySource

    /// HOST-013：稳定 marker id = digest(sessionId + source)。
    let private stableId (sessionId: string option) : string =
        let digest = HostDigest.sha256Hex ((defaultArg sessionId "") + source)

        idPrefix + digest.Substring(0, 24)

    /// HOST-013：合成 assistant guideline tool-result。
    let private buildMarker (markerId: string) (markerText: string) : obj =
        createObj
            [ "info",
              box (
                  createObj
                      [ "id", box markerId
                        "role", box "assistant"
                        "source", box source
                        "synthetic", box true ]
              )
              "parts",
              box
                  [| createObj
                         [ "type", box "tool"
                           "tool", box "guideline"
                           "callID", box markerId
                           "state",
                           box (
                               createObj
                                   [ "status", box "completed"
                                     "input", box (createObj [])
                                     "output", box markerText
                                     "time", box (createObj [ "start", box 0; "end", box 0 ]) ]
                           ) ] |] ]

    let private isWireAnchor (message: WireMessage) : bool =
        message.Role = "user"
        || (message.Parts
            |> List.exists (function
                | WireToolResult _ -> true
                | _ -> false))

    /// HOST-013：清理历史 marker 后，末尾写入单条 guideline tool-result。
    let tryInject (sessionId: string option) (markerText: string) (rawMessages: obj list) : obj list option =
        let retainedRaw = rawMessages |> List.filter (isAnyPairMarker >> not)
        let markerId = stableId sessionId
        let baseWire = retainedRaw |> List.choose Projection.decodeMessage

        let emptyCurrent: ProviderSemanticProjection =
            { ProviderId = None
              ModelId = None
              Variant = None
              Tools = []
              System = []
              Messages = [] }

        let snapshot: ProjectionSnapshot =
            { CurrentProjection = emptyCurrent
              CommittedPrefix = None
              BlogFrames = []
              TransportMessages = Set.empty
              HostReanchor = None }

        let intent =
            ProjectionIntent.InsertPairProgrammingThought
                { MarkerId = markerId
                  MarkerText = markerText }

        match ProjectionPlanner.plan [ intent ] with
        | Error _ -> None
        | Ok _ when baseWire |> List.exists isWireAnchor |> not -> None
        | Ok ordered ->
            let wire = ProjectionRenderer.renderMessagesWithIntents snapshot baseWire ordered

            let expected =
                baseWire
                @ [ { Role = "assistant"
                      Parts = [ WireToolResult(ToolCallId.create markerId, markerText) ] } ]

            if wire <> expected then
                None
            else
                Some(retainedRaw @ [ buildMarker markerId markerText ])
