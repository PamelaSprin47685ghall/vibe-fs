namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open Wanxiangshu.Requirement.Grounding

type RequirementGroundingProjectionState =
    { Pending: Map<string, GroundingSnapshot>
      OccurrencesRev: RequirementGroundingOccurrence list
      Grounded: Set<string>
      VisibleFromOrdinal: int64 }

[<RequireQualifiedAccess>]
type RequirementGroundingFoldRejection =
    | NonSequentialOrdinal of expected: int64 * actual: int64
    | DuplicateIdentity of identity: string
    | MissingRequest of identity: string

module RequirementGroundingProjection =

    let empty =
        { Pending = Map.empty
          OccurrencesRev = []
          Grounded = Set.empty
          VisibleFromOrdinal = 1L }

    let private occurrenceKey (occurrence: RequirementGroundingOccurrence) =
        GroundingIdentity.key occurrence.Workspace occurrence.PackageName occurrence.Digest

    let isGrounded workspace packageName digest state =
        Set.contains (GroundingIdentity.key workspace packageName digest) state.Grounded

    let isSnapshotGrounded snapshot state =
        Set.contains (GroundingIdentity.snapshotKey snapshot) state.Grounded

    let snapshotRequested snapshot state =
        Map.containsKey (GroundingIdentity.snapshotKey snapshot) state.Pending

    let pending state =
        state.Pending
        |> Map.toList
        |> List.map snd
        |> List.sortBy (fun snapshot -> snapshot.PackageName, snapshot.Digest)

    let occurrences state = List.rev state.OccurrencesRev

    let visibleOccurrences state =
        occurrences state
        |> List.filter (fun occurrence -> occurrence.Ordinal >= state.VisibleFromOrdinal)

    let groundedKeys state =
        state.Grounded |> Set.toList |> List.sort

    let nextOrdinal state =
        match state.OccurrencesRev with
        | [] -> 1L
        | newest :: _ -> newest.Ordinal + 1L

    let applyReanchor state =
        { state with
            Grounded = Set.empty
            VisibleFromOrdinal = nextOrdinal state }

    let applyRequested snapshot state =
        let key = GroundingIdentity.snapshotKey snapshot

        if Set.contains key state.Grounded || Map.containsKey key state.Pending then
            state
        else
            { state with
                Pending = Map.add key snapshot state.Pending }

    let applyAnchored occurrence state =
        let key = occurrenceKey occurrence
        let expected = nextOrdinal state

        if occurrence.Ordinal <> expected then
            Error(RequirementGroundingFoldRejection.NonSequentialOrdinal(expected, occurrence.Ordinal))
        elif Set.contains key state.Grounded then
            Error(RequirementGroundingFoldRejection.DuplicateIdentity key)
        elif not (Map.containsKey key state.Pending) then
            Error(RequirementGroundingFoldRejection.MissingRequest key)
        else
            Ok
                { Pending = Map.remove key state.Pending
                  OccurrencesRev = occurrence :: state.OccurrencesRev
                  Grounded = Set.add key state.Grounded
                  VisibleFromOrdinal = state.VisibleFromOrdinal }
