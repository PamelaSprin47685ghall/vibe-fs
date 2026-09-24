namespace Wanxiangshu.Participant.Cognition

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type CognitiveProjectionChange = CognitiveSet of ownerKey: string * projection: CognitiveProjection

[<RequireQualifiedAccess>]
type CognitiveFoldRejection =
    /// The ordinal is not the successor of the folded one: a replayed line from a
    /// different generation, or a fabricated jump.
    | NonSequentialOrdinal of committed: int64 * proposed: int64
    /// Two different inputs claimed the same tool call identity.
    | ToolCallIdentityConflict of toolCallId: ToolCallId

[<RequireQualifiedAccess>]
module CognitiveFoldRejection =
    let fact (rejection: CognitiveFoldRejection) : string =
        match rejection with
        | CognitiveFoldRejection.NonSequentialOrdinal _
        | CognitiveFoldRejection.ToolCallIdentityConflict _ -> "AssumePhaseCommitted"

    let message (rejection: CognitiveFoldRejection) : string =
        match rejection with
        | CognitiveFoldRejection.NonSequentialOrdinal(committed, proposed) ->
            sprintf
                "cognitive phase ordinal %d is not the successor of the committed %d (cognitive-workspace-006)"
                proposed
                committed
        | CognitiveFoldRejection.ToolCallIdentityConflict toolCallId ->
            sprintf
                "same tool call %s committed two different inputs (cognitive-workspace-004)"
                (ToolCallId.value toolCallId)

module CognitiveFactFold =

    /// The ordinal this commit claims to follow. The first phase has no predecessor
    /// and must be 1; every later phase must be its predecessor plus one.
    let private expectedOrdinal (commit: AssumePhaseCommitted) : int64 =
        match commit.PredecessorOrdinal with
        | Some previous -> previous + 1L
        | None -> 1L

    /// The ordinal already in force, reported when a line is refused. A first phase
    /// reports 0, which is what "nothing committed yet" means.
    let private committedOrdinal (commit: AssumePhaseCommitted) : int64 =
        match commit.PredecessorOrdinal with
        | Some previous -> previous
        | None -> 0L

    let private conflictsWithFolded (state: CognitiveProjection) (commit: AssumePhaseCommitted) : bool =
        state.LastToolCallId = Some commit.ToolCallId
        && state.LastInputDigest <> Some commit.InputDigest

    /// Fold one committed phase for one owner.
    ///
    /// The owner's state is supplied by the caller because the projection index is
    /// owned by Composition: this module stays a pure transition over one owner.
    /// The refusal for one committed phase, or None when it is admissible.
    let private refusalOf (state: CognitiveProjection) (commit: AssumePhaseCommitted) : CognitiveFoldRejection option =
        if conflictsWithFolded state commit then
            Some(CognitiveFoldRejection.ToolCallIdentityConflict commit.ToolCallId)
        elif commit.Ordinal <> expectedOrdinal commit then
            Some(CognitiveFoldRejection.NonSequentialOrdinal(committedOrdinal commit, commit.Ordinal))
        else
            None

    let fold
        (current: CognitiveProjection option)
        (fact: AssumeFactCases.T)
        : Result<CognitiveProjectionChange list, CognitiveFoldRejection> =
        let commitOf (fact: AssumeFactCases.T) =
            match fact with
            | AssumeFactCases.T.AssumePhaseCommitted commit -> commit

        let stateOf (current: CognitiveProjection option) =
            current |> Option.defaultValue CognitiveProjection.empty

        let commit = commitOf fact
        let state = stateOf current

        match refusalOf state commit with
        | Some rejection -> Error rejection
        | None -> Ok [ CognitiveProjectionChange.CognitiveSet(commit.OwnerKey, CognitiveProjection.apply commit state) ]
