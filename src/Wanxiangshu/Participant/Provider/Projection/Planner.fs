namespace Wanxiangshu.Participant.Provider.Projection

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module ProjectionPlanner =

    /// Canonical rank（how/projection.md）：
    /// keep/activate/mirror → blog → repair → strengthFrames → suppress → reanchor
    let private rank (intent: ProjectionIntent) : int =
        match intent with
        | ProjectionIntent.KeepPhysicalPrefix
        | ProjectionIntent.ActivatePrefixEpoch _
        | ProjectionIntent.UseStrengthMirror _ -> 0
        | ProjectionIntent.InsertBlogFrames _ -> 1
        | ProjectionIntent.InsertRepair _ -> 2
        | ProjectionIntent.InsertStrengthFrames _ -> 3
        | ProjectionIntent.SuppressTransportOnly -> 4
        | ProjectionIntent.ReanchorAfterCompaction -> 5

    let private kindKey (intent: ProjectionIntent) : int = rank intent

    let private requireIdentical
        (conflict: ProjectionConflict)
        (same: bool)
        (first: ProjectionIntent)
        : Result<ProjectionIntent option, ProjectionConflict> =
        if same then Ok(Some first) else Error conflict

    /// Evidence → Decision: identical UseStrengthMirror payloads collapse; else conflict.
    let private decideMirrorConsensus
        (first: ProjectionIntent)
        (second: ProjectionIntent)
        (xs: ProjectionIntent list)
        : Result<ProjectionIntent option, ProjectionConflict> =
        match
            xs
            |> List.choose (function
                | ProjectionIntent.UseStrengthMirror m -> Some m
                | _ -> None)
        with
        | [] -> Ok(Some first)
        | headMirror :: rest when rest |> List.forall ((=) headMirror) ->
            Ok(Some(ProjectionIntent.UseStrengthMirror headMirror))
        | _ -> Error(ProjectionConflict.ConflictingPrefixSelection(first, second))

    /// Evidence → Decision: ActivatePrefixEpoch payloads must agree when selected.
    let private decideActivateConsensus
        (first: ProjectionIntent)
        (second: ProjectionIntent)
        (xs: ProjectionIntent list)
        : Result<ProjectionIntent option, ProjectionConflict> =
        match first with
        | ProjectionIntent.ActivatePrefixEpoch activation when
            xs
            |> List.forall (function
                | ProjectionIntent.ActivatePrefixEpoch other -> other = activation
                | _ -> false)
            ->
            Ok(Some first)
        | ProjectionIntent.ActivatePrefixEpoch _ -> Error(ProjectionConflict.ConflictingPrefixSelection(first, second))
        | _ -> Ok(Some first)

    /// Evidence → Decision: Work-base prefix selection among keep / activate / mirror.
    let private decidePrefixReduction
        (first: ProjectionIntent)
        (second: ProjectionIntent)
        (xs: ProjectionIntent list)
        : Result<ProjectionIntent option, ProjectionConflict> =
        let hasKeep =
            xs
            |> List.exists (function
                | ProjectionIntent.KeepPhysicalPrefix -> true
                | _ -> false)

        let hasActivate =
            xs
            |> List.exists (function
                | ProjectionIntent.ActivatePrefixEpoch _ -> true
                | _ -> false)

        let hasMirror =
            xs
            |> List.exists (function
                | ProjectionIntent.UseStrengthMirror _ -> true
                | _ -> false)

        // Mirror is mutually exclusive with normal Work base selection.
        if hasMirror && (hasKeep || hasActivate) then
            Error(ProjectionConflict.ConflictingPrefixSelection(first, second))
        elif hasKeep && hasActivate then
            Error(ProjectionConflict.ConflictingPrefixSelection(first, second))
        elif hasKeep then
            Ok(Some ProjectionIntent.KeepPhysicalPrefix)
        elif hasMirror then
            decideMirrorConsensus first second xs
        else
            decideActivateConsensus first second xs

    let private reducePrefix (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | [ single ] -> Ok(Some single)
        | first :: second :: _ as xs -> decidePrefixReduction first second xs

    let private reduceBlogFrames (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | [ single ] -> Ok(Some single)
        | (ProjectionIntent.InsertBlogFrames head as first) :: rest ->
            let same =
                rest
                |> List.forall (function
                    | ProjectionIntent.InsertBlogFrames other -> other = head
                    | _ -> false)

            requireIdentical ProjectionConflict.ConflictingBlogFrames same first
        | first :: _ -> Ok(Some first)

    let private reduceRepair (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | [ single ] -> Ok(Some single)
        | (ProjectionIntent.InsertRepair head as first) :: rest ->
            let same =
                rest
                |> List.forall (function
                    | ProjectionIntent.InsertRepair other -> other = head
                    | _ -> false)

            requireIdentical ProjectionConflict.ConflictingRepair same first
        | first :: _ -> Ok(Some first)

    /// 幂等并 1：Suppress / Reanchor（以及任何单例重放型）。
    let private reduceIdempotent (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | first :: _ -> Ok(Some first)

    /// Evidence → Decision: Visibility × Anchor legality for one Strength frame.
    let private decideStrengthVisibilityAnchor
        (insertion: StrengthFrameInsertion)
        : Result<StrengthFrameInsertion, ProjectionConflict> =
        match insertion.Visibility, insertion.Anchor with
        | StrengthFrameVisibility.Candidate(target, current), StrengthFrameAnchor.Append when target <> current ->
            Error(ProjectionConflict.StrengthCandidateWrongTarget insertion.DecisionId)
        | StrengthFrameVisibility.Candidate _, StrengthFrameAnchor.Append -> Ok insertion
        | StrengthFrameVisibility.Candidate _, StrengthFrameAnchor.BeforeMessageIndex _ ->
            Error(ProjectionConflict.InvalidStrengthAnchor insertion.DecisionId)
        | StrengthFrameVisibility.Promoted(_, true), StrengthFrameAnchor.BeforeMessageIndex _ ->
            Error(ProjectionConflict.StrengthPromotedReplicaReflection insertion.DecisionId)
        | StrengthFrameVisibility.Promoted(_, false), StrengthFrameAnchor.BeforeMessageIndex _ -> Ok insertion
        | StrengthFrameVisibility.Promoted _, StrengthFrameAnchor.Append ->
            Error(ProjectionConflict.InvalidStrengthAnchor insertion.DecisionId)
        | StrengthFrameVisibility.ReplicaLocal, StrengthFrameAnchor.Append -> Ok insertion
        | StrengthFrameVisibility.ReplicaLocal, StrengthFrameAnchor.BeforeMessageIndex _ ->
            Error(ProjectionConflict.InvalidStrengthAnchor insertion.DecisionId)

    let private validateStrengthInsertion
        (insertion: StrengthFrameInsertion)
        : Result<StrengthFrameInsertion, ProjectionConflict> =
        if insertion.FrameDigest <> insertion.Bundle.Digest then
            Error(ProjectionConflict.StrengthFrameDigestMismatch insertion.DecisionId)
        else
            decideStrengthVisibilityAnchor insertion

    let private strengthItemsOf (intent: ProjectionIntent) : StrengthFrameInsertion list =
        match intent with
        | ProjectionIntent.InsertStrengthFrames payload -> payload.Items
        | _ -> []

    /// Evidence → Decision: kept list after one DecisionId merge step.
    let private mergeStrengthStep
        (kept: StrengthFrameInsertion list)
        (head: StrengthFrameInsertion)
        : Result<StrengthFrameInsertion list, ProjectionConflict> =
        match kept |> List.tryFind (fun existing -> existing.DecisionId = head.DecisionId) with
        | None -> Ok(head :: kept)
        | Some existing when existing = head -> Ok kept
        | Some _ -> Error(ProjectionConflict.ConflictingStrengthFrames head.DecisionId)

    let private mergeStrengthInsertions
        (insertions: StrengthFrameInsertion list)
        : Result<StrengthFrameInsertion list, ProjectionConflict> =
        let rec merge remaining kept =
            match remaining with
            | [] -> Ok(List.rev kept)
            | head :: tail -> mergeStrengthStep kept head |> Result.bind (fun nextKept -> merge tail nextKept)

        merge insertions []

    let private strengthSortKey (insertion: StrengthFrameInsertion) =
        match insertion.Anchor with
        | StrengthFrameAnchor.BeforeMessageIndex index -> 0, index, StrengthDecisionId.value insertion.DecisionId
        | StrengthFrameAnchor.Append -> 1, 0, StrengthDecisionId.value insertion.DecisionId

    /// Merge InsertStrengthFrames: same DecisionId+digest is idempotent;
    /// same DecisionId different digest/anchor → ConflictingStrengthFrames.
    /// Visibility rules applied per insertion before merge.
    let private reduceStrengthFrames
        (items: ProjectionIntent list)
        : Result<ProjectionIntent option, ProjectionConflict> =
        result {
            let collected = items |> List.collect strengthItemsOf
            let! validated = collected |> List.traverseResultM validateStrengthInsertion
            let! merged = mergeStrengthInsertions validated

            match merged with
            | [] -> return None
            | _ ->
                let canonical = merged |> List.sortBy strengthSortKey
                return Some(ProjectionIntent.InsertStrengthFrames { Items = canonical })
        }

    /// Evidence → Decision: which reducer owns this kind-group head.
    let private reduceByKind
        (head: ProjectionIntent)
        (items: ProjectionIntent list)
        : Result<ProjectionIntent option, ProjectionConflict> =
        match head with
        | ProjectionIntent.KeepPhysicalPrefix
        | ProjectionIntent.ActivatePrefixEpoch _
        | ProjectionIntent.UseStrengthMirror _ -> reducePrefix items
        | ProjectionIntent.InsertBlogFrames _ -> reduceBlogFrames items
        | ProjectionIntent.InsertRepair _ -> reduceRepair items
        | ProjectionIntent.InsertStrengthFrames _ -> reduceStrengthFrames items
        | ProjectionIntent.SuppressTransportOnly
        | ProjectionIntent.ReanchorAfterCompaction -> reduceIdempotent items

    let private reduceGroup (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | head :: _ -> reduceByKind head items

    let private appendReduced
        (intentOpt: ProjectionIntent option)
        (acc: ProjectionIntent list)
        : ProjectionIntent list =
        match intentOpt with
        | None -> acc
        | Some intent -> intent :: acc

    /// Evidence → Decision: ActivatePrefixEpoch ⊥ ReanchorAfterCompaction.
    let private ensurePrefixLifecycleCompatible (reduced: ProjectionIntent list) : Result<unit, ProjectionConflict> =
        let hasActivate =
            reduced
            |> List.exists (function
                | ProjectionIntent.ActivatePrefixEpoch _ -> true
                | _ -> false)

        let hasReanchor =
            reduced
            |> List.exists (function
                | ProjectionIntent.ReanchorAfterCompaction -> true
                | _ -> false)

        if hasActivate && hasReanchor then
            Error ProjectionConflict.ConflictingPrefixLifecycle
        else
            Ok()

    /// PROJ-006：汇总各功能意图 → groupBy kind → reduce → sortBy rank。
    ///
    /// 排列无关：同一多重集任意顺序得到同一有序结果或同一冲突。
    let plan (intents: ProjectionIntent list) : Result<ProjectionIntent list, ProjectionConflict> =
        let groups = intents |> List.groupBy kindKey |> List.sortBy fst

        let rec reduceAll remaining acc =
            match remaining with
            | [] -> Ok(List.rev acc)
            | (_, group) :: tail ->
                reduceGroup group
                |> Result.bind (fun intentOpt -> reduceAll tail (appendReduced intentOpt acc))

        result {
            let! reduced = reduceAll groups []
            do! ensurePrefixLifecycleCompatible reduced
            return reduced |> List.sortBy rank
        }
