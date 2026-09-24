namespace Wanxiangshu.Participant.Cognition

open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

/// JS-native boundary for the cognitive commit algebra.
///
/// The fold is pure and needs no Host, so its whole contract — idempotence, identity
/// conflict, ordinal succession — is reachable here. That is the point: a semantic
/// test must prove the commit rules without a journal or a plugin.
[<RequireQualifiedAccess>]
module FoldSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private optionInt64 (value: obj) =
        if isNull value then
            None
        else
            Some(int64 (unbox<float> value))

    let private blobRef (value: obj) = BlobRef.create (text value)

    let private blobDigest (value: obj) = BlobDigest.create (text value)

    let private decodeCommit (fields: obj) : AssumePhaseCommitted =
        { OwnerKey = text fields?ownerKey
          SessionId = SessionId.create (text fields?sessionId)
          IncumbencyId = text fields?incumbencyId
          ToolCallId = ToolCallId.create (text fields?toolCallId)
          Ordinal = int64 (unbox<float> fields?ordinal)
          InputDigest = text fields?inputDigest
          PredecessorOrdinal = optionInt64 fields?predecessorOrdinal
          SnapshotRef = blobRef fields?snapshotRef
          SnapshotDigest = blobDigest fields?snapshotDigest
          RendererVersion = text fields?rendererVersion }

    /// The JS view of a folded projection.
    let private projectionToJs (projection: CognitiveProjection) : obj =
        box
            // A plain number, not a BigInt: JS consumers compare ordinals against
            // counts and jq outputs, and a BigInt there would make every equality
            // silently false.
            {| ordinal = float projection.Ordinal
               snapshotRef = projection.SnapshotRef |> Option.map BlobRef.value |> Option.toObj
               snapshotDigest = projection.SnapshotDigest |> Option.map BlobDigest.value |> Option.toObj
               lastToolCallId = projection.LastToolCallId |> Option.map ToolCallId.value |> Option.toObj
               lastInputDigest = projection.LastInputDigest |> Option.toObj
               todoCount = List.length projection.Todos |}

    /// Fold one committed phase for one owner.
    ///
    /// `current` is that owner's folded state, or None for the first commit. The
    /// result is `{ ok, value? , error? }` so a caller can distinguish a refusal from
    /// an empty fold without catching.
    let CognitiveFactFold_fold (current: obj) (fact: obj) : obj =
        let state =
            if isNull current then
                None
            else
                Some
                    { SnapshotRef =
                        if isNull current?snapshotRef then
                            None
                        else
                            Some(blobRef current?snapshotRef)
                      SnapshotDigest =
                        if isNull current?snapshotDigest then
                            None
                        else
                            Some(blobDigest current?snapshotDigest)
                      Todos = []
                      Ordinal = int64 (unbox<float> current?ordinal)
                      LastToolCallId =
                        if isNull current?lastToolCallId then
                            None
                        else
                            Some(ToolCallId.create (current?lastToolCallId))
                      LastInputDigest =
                        if isNull current?lastInputDigest then
                            None
                        else
                            Some(text current?lastInputDigest)
                      LastSnapshotRef =
                        if isNull current?snapshotRef then
                            None
                        else
                            Some(blobRef current?snapshotRef)
                      LastSnapshotDigest =
                        if isNull current?snapshotDigest then
                            None
                        else
                            Some(blobDigest current?snapshotDigest) }

        let typedFact =
            AssumeFactCases.T.AssumePhaseCommitted(decodeCommit (unbox<obj> fact?fields))

        match CognitiveFactFold.fold state typedFact with
        | Ok changes ->
            box
                {| ok = true
                   value =
                    changes
                    |> List.map (fun change ->
                        match change with
                        | CognitiveProjectionChange.CognitiveSet(ownerKey, projection) ->
                            box
                                {| ownerKey = ownerKey
                                   projection = projectionToJs projection |})
                    |> List.toArray |}
        | Error rejection ->
            box
                {| ok = false
                   error = CognitiveFoldRejection.message rejection |}
