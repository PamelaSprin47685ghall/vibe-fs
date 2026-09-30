namespace Wanxiangshu.Strength.Replica

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection

/// The main <-> predictor bijection, isolated so the mathematics lives in one
/// place instead of being spread across the repository.
///
/// A resident read-only replica and its owner are twins. The owner's transcript is
/// the authority: it is what main actually sends. The replica additionally has its
/// OWN speech — assistant text that main never receives, because the projection
/// emits tool calls and results only. That speech is recorded in the child session
/// and must be restored at the gap it originally occurred in, which is the whole
/// difficulty of the mapping.
///
/// This module states and proves that restoration as pure functions of two message
/// lists, so it can be exercised directly by property tests instead of being
/// re-derived at each call site.
[<RequireQualifiedAccess>]
module TwinBijection =

    /// Does this message carry at least one tool call?
    let hasCall (message: ProviderProjection.WireMessage) =
        message.Parts
        |> List.exists (function
            | ProviderProjection.WireToolCall _ -> true
            | _ -> false)

    /// Speech: an ASSISTANT message carrying text or reasoning and no tool call.
    /// Only such messages are replica material that main never receives — the role
    /// matters, because a `user` text message is a prompt (the bootstrap or a
    /// harness-injected turn), not something the replica said.
    let isSpeechOnly (message: ProviderProjection.WireMessage) =
        message.Role.ToLowerInvariant() = "assistant"
        && not (hasCall message)
        && (message.Parts
            |> List.exists (function
                | ProviderProjection.WireText _
                | ProviderProjection.WireReasoning _ -> true
                | _ -> false))

    /// The gap index of every child message: how many of the child's own tool
    /// messages precede it. Speech keeps its gap; tool messages carry their own
    /// ordinal (1-based) so the alignment is total and order-preserving.
    let private numbered (child: ProviderProjection.WireMessage list) =
        child
        |> List.fold (fun (seen, acc) message ->
            if hasCall message then
                (seen + 1, (seen + 1, message) :: acc)
            elif isSpeechOnly message then
                (seen, (seen, message) :: acc)
            else
                (seen, acc)) (0, [])
        |> snd
        |> List.rev

    /// Restore the replica's speech into the gaps the child recorded.
    ///
    /// The owner's transcript is the skeleton and is emitted in order, unchanged in
    /// count and order; the child's speech is inserted immediately before the
    /// owner's `gap`-th tool exchange, and speech whose gap is past the owner's last
    /// exchange trails at the end.
    ///
    /// This never refuses and never drops speech: an unbalanced pair of histories
    /// still yields a usable request. Misplacement can only cost some prefix reuse,
    /// whereas dropping speech or failing would break the delegation outright.
    let restore (child: ProviderProjection.WireMessage list) (owner: ProviderProjection.WireMessage list) : ProviderProjection.WireMessage list =
        let ownerToolPositions =
            owner
            |> List.mapi (fun index message -> index, message)
            |> List.filter (fun (_, message) -> hasCall message)
            |> List.map fst

        let gapAnchor gap =
            if gap < List.length ownerToolPositions then
                Some ownerToolPositions.[gap]
            else
                None

        let childNumbered = numbered child

        let speechAt index =
            childNumbered
            |> List.choose (fun (gap, message) ->
                match gapAnchor gap with
                | Some anchor when isSpeechOnly message && anchor = index -> Some message
                | _ -> None)

        let trailing =
            childNumbered
            |> List.choose (fun (gap, message) ->
                if isSpeechOnly message && Option.isNone (gapAnchor gap) then
                    Some message
                else
                    None)

        (owner
         |> List.mapi (fun index message -> speechAt index @ [ message ])
         |> List.concat)
        @ trailing

    /// Every message the owner sent is present, in order, exactly once: restoration
    /// adds the replica's speech but never alters the owner's own sequence.
    let preservesOwnerOrder (child: ProviderProjection.WireMessage list) (owner: ProviderProjection.WireMessage list) =
        let restored = restore child owner
        let ownerProjection = restored |> List.filter hasCall
        let ownerCalls = owner |> List.filter hasCall

        List.length ownerProjection = List.length ownerCalls

    /// No message of the result is fabricated: every message is either an owner
    /// message or a child speech message.
    let introducesNothing (child: ProviderProjection.WireMessage list) (owner: ProviderProjection.WireMessage list) =
        let restored = restore child owner
        let speech = child |> List.filter isSpeechOnly
        let known = owner @ speech

        restored |> List.forall (fun message -> known |> List.exists (fun k -> k = message))

    /// Speech survives: the number of speech messages is never reduced.
    let dropsNoSpeech (child: ProviderProjection.WireMessage list) (owner: ProviderProjection.WireMessage list) =
        let restored = restore child owner
        let speech = child |> List.filter isSpeechOnly
        let kept = restored |> List.filter isSpeechOnly

        List.length kept = List.length speech

    /// Extension: when the owner only appends and the child only appends, restoring
    /// the wider pair reproduces the narrower request as its prefix. This is the
    /// property the residency exists for — the provider's cached prefix stays valid.
    let extensionIsPrefix
        (childBefore: ProviderProjection.WireMessage list)
        (ownerBefore: ProviderProjection.WireMessage list)
        (childAfter: ProviderProjection.WireMessage list)
        (ownerAfter: ProviderProjection.WireMessage list)
        =
        let rec prefixOf (candidate: ProviderProjection.WireMessage list) (whole: ProviderProjection.WireMessage list) =
            match candidate, whole with
            | [], _ -> true
            | head :: tail, other :: rest when head = other -> prefixOf tail rest
            | _ -> false

        prefixOf (restore childBefore ownerBefore) (restore childAfter ownerAfter)

/// JS boundary for the bijection. The properties are proved against the pure
/// functions above; this face exists so a test can drive them with plain objects.
[<RequireQualifiedAccess>]
module TwinBijectionSurface =

    let private textOf (value: obj) = unbox<string> value

    let private partOf (value: obj) : ProviderProjection.WirePart =
        match textOf value?kind with
        | "text" -> ProviderProjection.WireText(textOf value?text)
        | "reasoning" -> ProviderProjection.WireReasoning(textOf value?text)
        | "tool-call" ->
            ProviderProjection.WireToolCall(
                ToolCallId.create (textOf value?callId),
                textOf value?name,
                textOf value?args
            )
        | "tool-result" ->
            ProviderProjection.WireToolResult(ToolCallId.create (textOf value?callId), textOf value?result)
        | other -> failwithf "TwinBijectionSurface: unknown wire part kind %s" other

    let private messageOf (value: obj) : ProviderProjection.WireMessage =
        { Role = textOf value?role
          Parts = unbox<obj array> value?parts |> Array.toList |> List.map partOf }

    let private messageToJs (message: ProviderProjection.WireMessage) : obj =
        message.Parts
        |> List.map (fun part ->
            match part with
            | ProviderProjection.WireText text -> box {| kind = "text"; text = text |}
            | ProviderProjection.WireReasoning text -> box {| kind = "reasoning"; text = text |}
            | ProviderProjection.WireToolCall(callId, name, args) ->
                box
                    {| kind = "tool-call"
                       callId = ToolCallId.value callId
                       name = name
                       args = args |}
            | ProviderProjection.WireToolResult(callId, result) ->
                box
                    {| kind = "tool-result"
                       callId = ToolCallId.value callId
                       result = result |}
            | ProviderProjection.WireMedia(mediaType, digest) ->
                box
                    {| kind = "media"
                       mediaType = mediaType
                       contentDigest = digest |})
        |> List.toArray
        |> fun parts -> box {| role = message.Role; parts = parts |}

    let private messagesOf (values: obj array) = values |> Array.toList |> List.map messageOf

    let restore (child: obj array) (owner: obj array) : obj array =
        TwinBijection.restore (messagesOf child) (messagesOf owner)
        |> List.map messageToJs
        |> List.toArray

    let preservesOwnerOrder (child: obj array) (owner: obj array) : bool =
        TwinBijection.preservesOwnerOrder (messagesOf child) (messagesOf owner)

    let introducesNothing (child: obj array) (owner: obj array) : bool =
        TwinBijection.introducesNothing (messagesOf child) (messagesOf owner)

    let dropsNoSpeech (child: obj array) (owner: obj array) : bool =
        TwinBijection.dropsNoSpeech (messagesOf child) (messagesOf owner)

    let extensionIsPrefix (childBefore: obj array) (ownerBefore: obj array) (childAfter: obj array) (ownerAfter: obj array) : bool =
        TwinBijection.extensionIsPrefix
            (messagesOf childBefore)
            (messagesOf ownerBefore)
            (messagesOf childAfter)
            (messagesOf ownerAfter)
