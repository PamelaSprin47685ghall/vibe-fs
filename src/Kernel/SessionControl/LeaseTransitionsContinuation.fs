module Wanxiangshu.Kernel.SessionControl.LeaseTransitionsContinuation

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.SessionControl.State
open Wanxiangshu.Kernel.SessionControl.Event
open Wanxiangshu.Kernel.SessionControl.LeaseIdentity
open Wanxiangshu.Kernel.SessionControl.LeaseIdentityOps

let handleContinuationRequested (st: OwnerEpisodeState) (ev: ContinuationRequestEvent) : OwnerEpisodeState =
    let currentOrdinal = st.ContinuationOrdinal
    let newOrdinal = defaultOrdinal currentOrdinal ev.Ordinal

    if newOrdinal <= currentOrdinal then
        st
    else
        let nextLease =
            { ContinuationID = ev.ContinuationId
              ContinuationOrdinal = newOrdinal
              SessionGeneration = defaultGeneration st.SessionGeneration ev.Generation
              HumanTurnID = deriveHumanTurnId st.HumanTurn ev.HumanTurnId
              CancelGeneration = defaultCancelGeneration st.CancelGeneration ev.CancelGeneration
              Owner = ev.Owner
              Model = ev.Model
              PromptText = None
              Status = "requested" }

        { st with
            Owner = Some ev.Owner
            ContinuationOrdinal = newOrdinal
            ContinuationStage = Requested
            ContinuationLease = Some nextLease }

let handleContinuationTerminal (st: OwnerEpisodeState) (ev: EpisodeStageEvent) : OwnerEpisodeState =
    let eventOrdinal = defaultOrdinal st.ContinuationOrdinal ev.Ordinal

    if eventOrdinal <> st.ContinuationOrdinal || st.ContinuationStage = Terminal then
        st
    elif st.ContinuationLease |> Option.exists (fun l -> l.ContinuationID = ev.Id) then
        releaseOwnerIf
            "Fallback"
            { st with
                ContinuationLease = None
                ContinuationStage = Terminal }
    else
        st
