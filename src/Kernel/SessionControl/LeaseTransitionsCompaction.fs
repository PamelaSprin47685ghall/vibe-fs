module Wanxiangshu.Kernel.SessionControl.LeaseTransitionsCompaction

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.SessionControl.State
open Wanxiangshu.Kernel.SessionControl.Event
open Wanxiangshu.Kernel.SessionControl.LeaseIdentity
open Wanxiangshu.Kernel.SessionControl.LeaseIdentityOps

let handleCompactionStarted (st: OwnerEpisodeState) (ev: CompactionStartEvent) : OwnerEpisodeState =
    let newOrdinal = defaultOrdinal st.CompactionOrdinal ev.Ordinal

    if newOrdinal <= st.CompactionOrdinal then
        st
    else
        let genVal = defaultGeneration st.SessionGeneration ev.GenerationAtStart

        { st with
            Owner = Some "Compaction"
            CompactionOrdinal = newOrdinal
            CompactionStage = Requested
            CompactionGeneration = genVal
            IsCompacted = false
            Compaction =
                Some
                    { CompactionID = ev.CompactionId
                      CompactionOrdinal = newOrdinal
                      SessionGeneration = genVal
                      HumanTurnID = deriveHumanTurnId st.HumanTurn ev.HumanTurnId
                      CancelGeneration = st.CancelGeneration
                      Status = "started" } }

let handleContextGenerationChanged (st: OwnerEpisodeState) (ev: CompactionStageEvent) : OwnerEpisodeState =
    let contextGeneration = defaultGeneration st.CompactionGeneration ev.Generation

    if guardCompactionMatch ev.CompactionId ev.Ordinal st.Compaction then
        { st with
            IsCompacted = true
            CompactionGeneration =
                if ev.CompactionId <> "" && ev.Ordinal.IsSome then
                    contextGeneration
                else
                    st.CompactionGeneration }
    else
        st

let handleCompactionSettled (st: OwnerEpisodeState) (ev: EpisodeStageEvent) : OwnerEpisodeState =
    let eventOrdinal = defaultOrdinal st.CompactionOrdinal ev.Ordinal

    if
        eventOrdinal = st.CompactionOrdinal
        && st.CompactionStage <> Terminal
        && st.Compaction |> Option.exists (fun c -> c.CompactionID = ev.Id)
    then
        { st with
            Owner = Some "None"
            CompactionStage = Terminal
            Compaction = None
            IsCompacted = false
            CompactionGeneration = 0 }
    else
        st
