namespace Wanxiangshu.OpenCode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host

/// HOST-013: the pair-programming thought marker.
///
/// Injected into the final provider-facing transcript at
/// `experimental.chat.messages.transform`: one marker after every anchor (a user
/// message or a completed tool-result message) and before ReviewSeal, so the
/// seal digests the exact bytes the provider receives. XTrace capture runs
/// earlier in the chain, so the marker never enters a work record.
///
/// PROJ-008 Step5：正文/锚点布局经 `InsertPairProgrammingThought` → plan →
/// renderMessagesWithIntents；Host 仅写回 id / source 侧信道。
module PairProgrammingThoughtTransform =

    open Wanxiangshu.Domain

    /// The frozen provider-visible thought text (HOST-013). Domain 单源。
    let text = ProjectionConstants.PairProgrammingThoughtText

    /// The marker's source identity (HOST-013). Filtering must use this, never
    /// the text: a real user may quote the sentence.
    let source = "pair-programming-thought"

    let private idPrefix = "pair-programming-thought-"

    /// HOST-013: the marker identity predicate, by `info.source` only.
    let isPairProgrammingThought (rawMsg: obj) : bool =
        if isNull rawMsg then
            false
        else
            match rawMsg?info with
            | null -> false
            | info -> unbox<string> info?source = source

    /// HOST-013: stable marker id = digest(sessionId + anchorMessageId +
    /// source). A missing session id participates as the empty string, so the
    /// id stays stable per anchor; re-transforming the same anchor yields the
    /// same id, keeping prompt bytes, prefix cache and review seal stable.
    let private stableId (sessionId: string option) (anchorMessageId: string option) : string =
        let digest =
            HostDigest.sha256Hex ((defaultArg sessionId "") + (defaultArg anchorMessageId "") + source)

        idPrefix + digest.Substring(0, 24)

    /// HOST-013: the synthetic assistant message, one reasoning part.
    let private buildMarker (id: string) (markerText: string) : obj =
        createObj
            [ "info",
              box (
                  createObj
                      [ "id", box id
                        "role", box "assistant"
                        "source", box source
                        "synthetic", box true ]
              )
              "parts", box [| createObj [ "type", box "reasoning"; "text", box markerText ] |] ]

    /// Wire 锚点：user 或含 completed tool-result 的消息。
    let private isWireAnchor (message: WireMessage) : bool =
        message.Role = "user"
        || message.Parts
           |> List.exists (function
               | WireToolResult _ -> true
               | _ -> false)

    let private isWirePairMarker (message: WireMessage) : bool =
        message.Role = "assistant"
        && message.Parts
           |> List.exists (function
               | WireReasoning t when t = text -> true
               | _ -> false)

    /// 将 algebra wire 视图与 base raw 对齐写回 Host obj：保留非 marker 的原始
    /// raw（含 id）；新 marker 用稳定 id；已有 pair marker raw 字节保持。
    let private mergeHostMarkers (sessionId: string option) (baseRaw: obj list) (wire: WireMessage list) : obj list =
        // 建立 base raw 的消费游标：按顺序匹配非 marker wire 项。
        // DSL-MUTABLE: merge 游标
        let mutable baseIdx = 0
        let acc = ResizeArray<obj>()

        for msg in wire do
            if isWirePairMarker msg then
                let anchorMessageId =
                    if acc.Count = 0 then
                        None
                    else
                        Projection.hostMessageId (acc.[acc.Count - 1])

                let markerText =
                    msg.Parts
                    |> List.tryPick (function
                        | WireReasoning t -> Some t
                        | _ -> None)
                    |> Option.defaultValue text

                // 幂等：若 base 在当前位置已是 pair marker，保留原 raw。
                let existing =
                    if baseIdx < baseRaw.Length then
                        let raw = baseRaw.[baseIdx]

                        if isPairProgrammingThought raw then Some raw else None
                    else
                        None

                match existing with
                | Some raw ->
                    acc.Add raw
                    baseIdx <- baseIdx + 1
                | None -> acc.Add(buildMarker (stableId sessionId anchorMessageId) markerText)
            else
                // 对齐 base 中下一个同角色消息（跳过 base 内已有 pair marker）。
                let rec takeBase () =
                    if baseIdx >= baseRaw.Length then
                        // base 耗尽：从 wire 构造最小 Host 对象（不应在生产路径出现）
                        let role = msg.Role

                        let partText =
                            msg.Parts
                            |> List.tryPick (function
                                | WireText t -> Some("text", t)
                                | WireReasoning t -> Some("reasoning", t)
                                | WireToolResult(_, t) -> Some("text", t)
                                | WireToolCall(_, _, t) -> Some("text", t)
                                | WireMedia(_, digest) -> Some("text", digest))
                            |> Option.defaultValue ("text", "")

                        let kind, body = partText

                        acc.Add(
                            createObj
                                [ "info", box (createObj [ "role", box role ])
                                  "parts", box [| createObj [ "type", box kind; "text", box body ] |] ]
                        )
                    else
                        let raw = baseRaw.[baseIdx]
                        baseIdx <- baseIdx + 1

                        if isPairProgrammingThought raw then
                            takeBase ()
                        else
                            acc.Add raw

                takeBase ()

        acc |> Seq.toList

    /// HOST-013: 声明 `InsertPairProgrammingThought`，经 plan+render 得到 marker 布局，
    /// 再 merge 回 Host obj。无锚点 → None（不写回）。
    let tryInject (sessionId: string option) (rawMessages: obj list) : obj list option =
        let baseWire = rawMessages |> List.choose Projection.decodeMessage

        let hasAnchor = baseWire |> List.exists isWireAnchor

        if not hasAnchor then
            None
        else
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

            let intents =
                [ ProjectionIntent.InsertPairProgrammingThought { SessionId = sessionId } ]

            match ProjectionPlanner.plan intents with
            | Error _ -> None
            | Ok ordered ->
                let wire = ProjectionRenderer.renderMessagesWithIntents snapshot baseWire ordered

                if wire = baseWire then
                    None
                else
                    Some(mergeHostMarkers sessionId rawMessages wire)
