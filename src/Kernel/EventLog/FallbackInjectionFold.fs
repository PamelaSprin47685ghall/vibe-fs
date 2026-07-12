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
      InjectedCount: int }

let emptyFallbackInjectionState: FallbackInjectionState =
    { InjectedModel = None
      InjectedAgent = None
      InjectedAt = None
      InjectedCount = 0 }

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
        st
    elif
        e.Kind = eventKindContinuationSettled
        || e.Kind = eventKindContinuationFailed
        || e.Kind = eventKindContinuationCancelled
    then
        st
    elif
        e.Kind <> eventKindFallbackContinueInjected
        && e.Kind <> eventKindContinuationDispatched
    then
        st
    elif currentContinuationStage = NoEpisode || currentContinuationStage = Terminal then
        st
    else
        let contOrdinal = continuationOrdinal e

        let shouldProcess =
            match contOrdinal with
            | Some eventOrd -> eventOrd = currentContinuationOrdinal
            | None -> true

        if not shouldProcess then
            st
        elif
            e.Kind = eventKindContinuationDispatched
            && currentContinuationStage <> DispatchStarted
        then
            st
        else
            let model = payloadField "model" e
            let agent = payloadField "agent" e
            let at = payloadField "at" e |> Option.bind parseAtMs

            { InjectedModel = if model.IsSome then model else st.InjectedModel
              InjectedAgent = if agent.IsSome then agent else st.InjectedAgent
              InjectedAt = if at.IsSome then at else st.InjectedAt
              InjectedCount = st.InjectedCount + 1 }

let private forSession (sessionId: string) (events: WanEvent list) : WanEvent list =
    events |> List.filter (fun e -> e.Session = sessionId)

let private foldEventStream
    (sessionId: string)
    (zero: 'State)
    (folder: 'State -> WanEvent -> 'State)
    (events: WanEvent list)
    : 'State =
    forSession sessionId events |> List.fold folder zero

let private continuationStartOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "continuationOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue (currentOrdinal + 1)

/// Fold fallback injection state for one session; file line order preserved.
let foldFallbackInjection (sessionId: string) (events: WanEvent list) : FallbackInjectionState =
    let updateStage (ordinal, stage) (e: WanEvent) =
        if e.Kind = eventKindContinuationRequested then
            continuationStartOrdinal ordinal e, Requested
        elif e.Kind = eventKindContinuationDispatchStarted then
            ordinal, DispatchStarted
        elif e.Kind = eventKindContinuationDispatched then
            ordinal, Dispatched
        elif
            e.Kind = eventKindContinuationFailed
            || e.Kind = eventKindContinuationCancelled
            || e.Kind = eventKindContinuationSettled
        then
            ordinal, Terminal
        elif e.Kind = eventKindHumanTurnStarted || e.Kind = eventKindUserAbortObserved then
            0, NoEpisode
        else
            ordinal, stage

    let folder ((ordinal, stage, st): int * EpisodeStage * FallbackInjectionState) (e: WanEvent) =
        let nextSt = fallbackInjectionFolder ordinal stage st e
        let nextOrdinal, nextStage = updateStage (ordinal, stage) e
        nextOrdinal, nextStage, nextSt

    let _, _, final =
        foldEventStream sessionId (0, Requested, emptyFallbackInjectionState) folder events

    final
