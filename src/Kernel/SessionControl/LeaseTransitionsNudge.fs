module Wanxiangshu.Kernel.SessionControl.LeaseTransitionsNudge

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.SessionControl.State
open Wanxiangshu.Kernel.SessionControl.Event
open Wanxiangshu.Kernel.SessionControl.LeaseIdentity
open Wanxiangshu.Kernel.SessionControl.LeaseIdentityOps

let handleNudgeRequested (st: OwnerEpisodeState) (ev: NudgeRequestEvent) : OwnerEpisodeState =
    let currentOrdinal = st.NudgeOrdinal
    let newOrdinal = defaultOrdinal currentOrdinal ev.Ordinal

    if newOrdinal <= currentOrdinal then
        st
    else
        let nextLease =
            { NudgeID = ev.NudgeId
              NudgeOrdinal = newOrdinal
              Nonce = ev.Nonce
              Anchor = ev.Anchor
              HumanTurnID = deriveHumanTurnId st.HumanTurn ev.HumanTurnId
              SessionGeneration = defaultGeneration st.SessionGeneration ev.Generation
              CancelGeneration = defaultCancelGeneration st.CancelGeneration ev.CancelGeneration
              Status = "requested" }

        { st with
            Owner = Some "Nudge"
            NudgeOrdinal = newOrdinal
            NudgeStage = Requested
            NudgeLease = Some nextLease }

let handleNudgeTerminal (st: OwnerEpisodeState) (ev: EpisodeStageEvent) : OwnerEpisodeState =
    let eventOrdinal = defaultOrdinal st.NudgeOrdinal ev.Ordinal

    if eventOrdinal <> st.NudgeOrdinal || st.NudgeStage = Terminal then
        st
    elif st.NudgeLease |> Option.exists (fun nl -> nl.NudgeID = ev.Id) then
        releaseOwnerIf
            "Nudge"
            { st with
                NudgeLease = None
                NudgeStage = Terminal }
    else
        st

let handleAssistantCompleted (st: OwnerEpisodeState) : OwnerEpisodeState =
    if st.Owner = Some "Nudge" then
        { st with
            Owner = Some "None"
            NudgeLease = None
            NudgeStage = NoEpisode }
    else
        st

let handleNudgeDedupClearedOrWip (st: OwnerEpisodeState) : OwnerEpisodeState =
    { st with
        Owner = Some "None"
        NudgeLease = None
        NudgeStage = NoEpisode }
