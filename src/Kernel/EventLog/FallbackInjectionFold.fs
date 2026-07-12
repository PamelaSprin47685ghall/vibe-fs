module Wanxiangshu.Kernel.EventLog.FallbackInjectionFold

open Wanxiangshu.Kernel.EventLog.Types

/// Per-session integral of fallback `SendContinue` injections.
/// The injected model is the model the fallback state machine selected and
/// dispatched; the consumer side (resolveNudgeModel / tryGetLatestUserModel)
/// must read this integral instead of sniffing the message text for "continue".
/// SSOT rule: any time the fallback runtime injects a user prompt, the same
/// fact must appear in NDJSON and be folded here.
type FallbackInjectionState =
    { InjectedModel: string option
      InjectedAgent: string option
      InjectedAt: int64 option
      InjectedCount: int
      ActiveContinuationOrdinal: int option }

let emptyFallbackInjectionState: FallbackInjectionState =
    { InjectedModel = None
      InjectedAgent = None
      InjectedAt = None
      InjectedCount = 0
      ActiveContinuationOrdinal = None }

let private payloadField (key: string) (e: WanEvent) : string option = e.Payload |> Map.tryFind key

let private parseAtMs (raw: string) : int64 option =
    if raw = "" then
        None
    else
        match System.Int64.TryParse raw with
        | true, n -> Some n
        | _ -> None

let private parseIntOpt (raw: string) : int option =
    if raw = "" then
        None
    else
        try
            Some(int raw)
        with _ ->
            None

let private continuationOrdinal (e: WanEvent) : int option =
    e.Payload |> Map.tryFind "continuationOrdinal" |> Option.bind parseIntOpt

let fallbackInjectionFolder
    (currentContinuationOrdinal: int)
    (currentContinuationStage: EpisodeStage)
    (st: FallbackInjectionState)
    (e: WanEvent)
    : FallbackInjectionState =
    if e.Kind = eventKindContinuationRequested then
        let contOrdinal =
            continuationOrdinal e |> Option.defaultValue (currentContinuationOrdinal + 1)

        if contOrdinal <= currentContinuationOrdinal then
            st
        else
            { st with
                ActiveContinuationOrdinal = Some contOrdinal }
    elif
        e.Kind = eventKindContinuationSettled
        || e.Kind = eventKindContinuationFailed
        || e.Kind = eventKindContinuationCancelled
    then
        if currentContinuationStage = Terminal then
            { st with
                ActiveContinuationOrdinal = None }
        else
            st
    elif
        e.Kind <> eventKindFallbackContinueInjected
        && e.Kind <> eventKindContinuationDispatched
    then
        st
    elif currentContinuationStage = NoEpisode || currentContinuationStage = Terminal then
        st
    else
        let model = payloadField "model" e
        let agent = payloadField "agent" e
        let at = payloadField "at" e |> Option.bind parseAtMs
        let contOrdinal = continuationOrdinal e

        let shouldProcess =
            match st.ActiveContinuationOrdinal, contOrdinal with
            | Some active, Some eventOrd -> eventOrd = active || eventOrd > active
            | Some _, None -> true
            | None, _ -> true

        if not shouldProcess then
            st
        else
            { InjectedModel = if model.IsSome then model else st.InjectedModel
              InjectedAgent = if agent.IsSome then agent else st.InjectedAgent
              InjectedAt = if at.IsSome then at else st.InjectedAt
              InjectedCount = st.InjectedCount + 1
              ActiveContinuationOrdinal = st.ActiveContinuationOrdinal }

let private forSession (sessionId: string) (events: WanEvent list) : WanEvent list =
    events |> List.filter (fun e -> e.Session = sessionId)

let private foldEventStream
    (sessionId: string)
    (zero: 'State)
    (folder: 'State -> WanEvent -> 'State)
    (events: WanEvent list)
    : 'State =
    forSession sessionId events |> List.fold folder zero

/// Fold fallback injection state for one session; file line order preserved.
let foldFallbackInjection (sessionId: string) (events: WanEvent list) : FallbackInjectionState =
    foldEventStream sessionId emptyFallbackInjectionState (fallbackInjectionFolder 0 Requested) events
