namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.OpenCode.ProviderWireDecode
open Wanxiangshu.OpenCode.ProviderWireCapture

/// CRASH-018 marker for the exact Host user material produced by `/continue`.
///
/// A SessionId is a reusable container and is therefore not a valid suppression
/// lifetime. The durable semantic marker rides on the visible text part itself.
/// The process-local registry below only remembers which exact physical material
/// carried that marker so later reconcile/idle passes can recognize the same turn;
/// a new unmarked PhysicalUserMessageId for the same SessionId clears it immediately.
/// No session-end/idle/abort signal is required for correctness.
module ExplicitResumeSuppression =

    type private PendingBriefing =
        { MaterialWitness: string
          Text: string }

    [<RequireQualifiedAccess>]
    type PhysicalMaterialObservation =
        | ExplicitResume
        | ReplacedExplicitResume
        | Ordinary

    [<RequireQualifiedAccess>]
    type BriefingMaterialization =
        | ExplicitResume
        | Ordinary

    [<Literal>]
    let MetadataKey = "wanxiangshu_explicit_resume"

    let markedTextPart (text: string) : obj =
        createObj
            [ "type" ==> "text"
              "text" ==> text
              "metadata" ==> createObj [ MetadataKey ==> true ] ]

    let private gate = obj ()
    // DSL-MUTABLE: resource — marked physical material registry per session
    let private markedPhysicalBySession = Dictionary<string, string>()
    // DSL-MUTABLE: resource — one-shot command→chat.message transport handoff.
    // command.execute.before has no PhysicalUserMessageId, so the dynamic briefing
    // waits here only until the Host presents the next physical material.
    let private pendingBriefingBySession = Dictionary<string, PendingBriefing>()

    [<Emit("""(() => {
      const parts = Array.isArray($0?.parts) ? $0.parts : [];
      return parts.some((part) => part?.metadata?.wanxiangshu_explicit_resume === true);
    })()""")>]
    let private outputHasMarker (_output: obj) : bool = jsNative

    [<Emit("""(() => {
      const parts = Array.isArray($0?.parts) ? $0.parts : [];
      return parts.some((part) => String(part?.text ?? '').includes($1));
    })()""")>]
    let private outputContainsWitness (_output: obj) (_witness: string) : bool = jsNative

    let private existingParts (output: obj) : obj array =
        if isNull output || isNull output?parts then
            [||]
        else
            unbox<obj array> output?parts

    let stageBriefing (sessionId: SessionId) (materialWitness: string) (text: string) : unit =
        lock gate (fun () ->
            pendingBriefingBySession.[SessionId.value sessionId] <-
                { MaterialWitness = materialWitness
                  Text = text })

    let private materializeStagedBriefing (output: obj) (pending: PendingBriefing) : BriefingMaterialization =
        if outputHasMarker output then
            BriefingMaterialization.ExplicitResume
        elif isNull output || not (outputContainsWitness output pending.MaterialWitness) then
            BriefingMaterialization.Ordinary
        else
            output?parts <- Array.append (existingParts output) [| markedTextPart pending.Text |]
            BriefingMaterialization.ExplicitResume

    let private classifyExistingMaterial (output: obj) : BriefingMaterialization =
        if outputHasMarker output then
            BriefingMaterialization.ExplicitResume
        else
            BriefingMaterialization.Ordinary

    /// Materialize the staged disclosure on the real chat.message material.
    /// Hosts that already forwarded command output carry the marker themselves;
    /// in that case the pending handoff is only consumed, never duplicated.
    let materializePendingBriefing (sessionId: SessionId) (output: obj) : BriefingMaterialization =
        lock gate (fun () ->
            let sessionKey = SessionId.value sessionId

            match pendingBriefingBySession.TryGetValue sessionKey with
            | false, _ -> classifyExistingMaterial output
            | true, pending ->
                pendingBriefingBySession.Remove sessionKey |> ignore
                materializeStagedBriefing output pending)

    /// chat.message is the physical-material boundary. Same marked material keeps
    /// its suppression across provider retries; a later ordinary user material on
    /// the same SessionId removes it immediately.
    let observePhysicalMaterial
        (sessionId: SessionId)
        (physicalId: PhysicalUserMessageId)
        (output: obj)
        : PhysicalMaterialObservation =
        lock gate (fun () ->
            let sessionKey = SessionId.value sessionId
            let marked = outputHasMarker output

            let existing =
                match markedPhysicalBySession.TryGetValue sessionKey with
                | true, physical -> Some physical
                | false, _ -> None

            if marked then
                markedPhysicalBySession.[sessionKey] <- PhysicalUserMessageId.value physicalId

            match marked, existing with
            | true, _ -> PhysicalMaterialObservation.ExplicitResume
            | false, Some current when current = PhysicalUserMessageId.value physicalId ->
                PhysicalMaterialObservation.ExplicitResume
            | false, Some _ ->
                markedPhysicalBySession.Remove sessionKey |> ignore
                PhysicalMaterialObservation.ReplacedExplicitResume
            | false, None -> PhysicalMaterialObservation.Ordinary)

    let isPhysicalMaterial (sessionId: SessionId) (physicalId: PhysicalUserMessageId) : bool =
        lock gate (fun () ->
            match markedPhysicalBySession.TryGetValue(SessionId.value sessionId) with
            | true, current -> current = PhysicalUserMessageId.value physicalId
            | false, _ -> false)

    let hasMarkedPhysicalMaterial (sessionId: SessionId) : bool =
        lock gate (fun () -> markedPhysicalBySession.ContainsKey(SessionId.value sessionId))

    /// CRASH-018 chat.message classification. Materialization and exact-physical
    /// replay knowledge are one owner decision; Host wiring must not reconstruct
    /// the precedence between them.
    let classifyChatMessage (decoded: PromptIngressCodec.DecodedMessage) (output: obj) : bool =
        let materialization =
            match decoded.SessionId with
            | Some sessionId -> materializePendingBriefing sessionId output
            | None -> BriefingMaterialization.Ordinary

        let knownExplicitResume =
            match decoded.SessionId, decoded.PhysicalUserMessageId with
            | Some sessionId, Some physicalId -> isPhysicalMaterial sessionId physicalId
            | _ -> false

        match materialization with
        | BriefingMaterialization.ExplicitResume -> true
        | BriefingMaterialization.Ordinary -> knownExplicitResume

    /// The exact physical material registry decides whether reconciliation must
    /// bind this user material. Both a marked resume and the first ordinary
    /// replacement change the binding boundary.
    let requiresPhysicalBinding sessionId physicalId output =
        match observePhysicalMaterial sessionId physicalId output with
        | PhysicalMaterialObservation.ExplicitResume
        | PhysicalMaterialObservation.ReplacedExplicitResume -> true
        | PhysicalMaterialObservation.Ordinary -> false

    /// CRASH-018: Check if the trailing user message in the transform output
    /// is an explicit resume binding for the given session.
    /// Domain decision: determines whether material is /continue disclosure.
    let isExplicitResumeBinding (projectionSessionIdOpt: string option) (outObj: obj) : bool =
        projectionSessionIdOpt
        |> Option.exists (fun sessionText ->
            let rawMessages =
                ProviderWireDecode.rawArray (ProviderWireDecode.readField outObj "messages")

            ProviderWireCapture.lastUserMessageId rawMessages
            |> Option.exists (isPhysicalMaterial (SessionId.create sessionText)))

    /// Cleanup only. Exact-new-material replacement is the correctness boundary.
    let dropSession (sessionId: SessionId) : unit =
        lock gate (fun () ->
            let sessionKey = SessionId.value sessionId
            markedPhysicalBySession.Remove sessionKey |> ignore
            pendingBriefingBySession.Remove sessionKey |> ignore)

    /// The provider-facing transform always receives the current user request as
    /// the trailing real message. Historical `/continue` messages may remain in
    /// the transcript, so only that trailing user material is authoritative.
    [<Emit("""(() => {
      const messages = Array.isArray($0?.messages) ? $0.messages : [];
      const last = messages.length === 0 ? null : messages[messages.length - 1];
      const info = last?.info ?? last;
      if (String(info?.role ?? '').toLowerCase() !== 'user') return false;
      const parts = Array.isArray(last?.parts) ? last.parts : [];
      return parts.some((part) => part?.metadata?.wanxiangshu_explicit_resume === true);
    })()""")>]
    let private isCurrentMaterialPhysical (_output: obj) : bool = jsNative

    let isCurrentMaterial output = isCurrentMaterialPhysical output
