namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open Wanxiangshu.Requirement.Grounding

type RequirementGroundingProjectionState =
    { Pending: Map<string, GroundingSnapshot>
      OccurrencesRev: RequirementGroundingOccurrence list
      VisibleMaterials: Set<string>
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
          VisibleMaterials = Set.empty
          VisibleFromOrdinal = 1L }

    let private occurrenceKey (occurrence: RequirementGroundingOccurrence) =
        GroundingIdentity.key occurrence.Workspace occurrence.PackageName occurrence.Digest

    let private materialKey workspace packageName path digest =
        GroundingIdentity.materialKey workspace packageName path digest

    let isSnapshotGrounded snapshot state =
        snapshot.Materials
        |> List.forall (fun material ->
            Set.contains
                (GroundingIdentity.snapshotMaterialKey snapshot material)
                state.VisibleMaterials)

    let snapshotRequested snapshot state =
        Map.containsKey (GroundingIdentity.snapshotKey snapshot) state.Pending

    let pending state =
        state.Pending
        |> Map.toList
        |> List.map snd
        |> List.choose (fun snapshot ->
            let missing =
                snapshot.Materials
                |> List.filter (fun material ->
                    not (
                        Set.contains
                            (GroundingIdentity.snapshotMaterialKey snapshot material)
                            state.VisibleMaterials
                    ))

            if List.isEmpty missing then
                None
            else
                Some { snapshot with Materials = missing })
        |> List.sortBy (fun snapshot -> snapshot.PackageName, snapshot.Digest)

    let occurrences state = List.rev state.OccurrencesRev

    let visibleOccurrences state =
        occurrences state
        |> List.filter (fun occurrence -> occurrence.Ordinal >= state.VisibleFromOrdinal)

    let groundedKeys state =
        state.VisibleMaterials |> Set.toList |> List.sort

    let nextOrdinal state =
        match state.OccurrencesRev with
        | [] -> 1L
        | newest :: _ -> newest.Ordinal + 1L

    let applyReanchor state =
        { state with
            VisibleMaterials = Set.empty
            VisibleFromOrdinal = nextOrdinal state }

    let applyRequested snapshot state =
        let key = GroundingIdentity.snapshotKey snapshot

        if isSnapshotGrounded snapshot state || Map.containsKey key state.Pending then
            state
        else
            { state with
                Pending = Map.add key snapshot state.Pending }

    let applyMaterialObserved (observation: RequirementGroundingMaterialObserved) state =
        let key =
            materialKey observation.Workspace observation.PackageName observation.Path observation.Digest

        let visible = Set.add key state.VisibleMaterials

        let pending =
            state.Pending
            |> Map.filter (fun _ snapshot ->
                snapshot.Materials
                |> List.exists (fun material ->
                    not (
                        Set.contains
                            (GroundingIdentity.snapshotMaterialKey snapshot material)
                            visible
                    )))

        { state with
            Pending = pending
            VisibleMaterials = visible }

    let applyAnchored occurrence state =
        let key = occurrenceKey occurrence
        let expected = nextOrdinal state

        if occurrence.Ordinal <> expected then
            Error(RequirementGroundingFoldRejection.NonSequentialOrdinal(expected, occurrence.Ordinal))
        elif not (Map.containsKey key state.Pending) then
            Error(RequirementGroundingFoldRejection.MissingRequest key)
        else
            let visible =
                occurrence.Reads
                |> List.fold
                    (fun current read ->
                        Set.add
                            (materialKey occurrence.Workspace occurrence.PackageName read.Path read.MaterialDigest)
                            current)
                    state.VisibleMaterials

            Ok
                { Pending = Map.remove key state.Pending
                  OccurrencesRev = occurrence :: state.OccurrencesRev
                  VisibleMaterials = visible
                  VisibleFromOrdinal = state.VisibleFromOrdinal }
