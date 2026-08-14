namespace Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
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

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module ProjectionPlanner =

    /// Canonical rank（how/projection.md）：
    /// keep/activate/mirror → blog → repair → strengthFrames → suppress → challenge → reanchor
    let private rank (intent: ProjectionIntent) : int =
        match intent with
        | ProjectionIntent.KeepPhysicalPrefix
        | ProjectionIntent.ActivatePrefixEpoch _
        | ProjectionIntent.UseStrengthMirror _ -> 0
        | ProjectionIntent.InsertBlogFrames _ -> 1
        | ProjectionIntent.InsertRepair _ -> 2
        | ProjectionIntent.InsertStrengthFrames _ -> 3
        | ProjectionIntent.SuppressTransportOnly -> 4
        | ProjectionIntent.AppendReviewChallenge _ -> 5
        | ProjectionIntent.ReanchorAfterCompaction -> 6

    let private kindKey (intent: ProjectionIntent) : int = rank intent

    let private reducePrefix (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | [ single ] -> Ok(Some single)
        | first :: second :: _ as xs ->
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
                match
                    xs
                    |> List.choose (function
                        | ProjectionIntent.UseStrengthMirror m -> Some m
                        | _ -> None)
                with
                | [] -> Ok(Some first)
                | headMirror :: restMirrors ->
                    if restMirrors |> List.forall ((=) headMirror) then
                        Ok(Some(ProjectionIntent.UseStrengthMirror headMirror))
                    else
                        Error(ProjectionConflict.ConflictingPrefixSelection(first, second))
            else
                match first with
                | ProjectionIntent.ActivatePrefixEpoch activation ->
                    let samePayload =
                        xs
                        |> List.forall (function
                            | ProjectionIntent.ActivatePrefixEpoch other -> other = activation
                            | _ -> false)

                    if samePayload then
                        Ok(Some first)
                    else
                        Error(ProjectionConflict.ConflictingPrefixSelection(first, second))
                | _ -> Ok(Some first)

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

            if same then
                Ok(Some first)
            else
                Error ProjectionConflict.ConflictingBlogFrames
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

            if same then
                Ok(Some first)
            else
                Error ProjectionConflict.ConflictingRepair
        | first :: _ -> Ok(Some first)

    let private reduceChallenge (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | [ single ] -> Ok(Some single)
        | (ProjectionIntent.AppendReviewChallenge head as first) :: rest ->
            let same =
                rest
                |> List.forall (function
                    | ProjectionIntent.AppendReviewChallenge other -> other = head
                    | _ -> false)

            if same then
                Ok(Some first)
            else
                Error ProjectionConflict.ConflictingReviewChallenge
        | first :: _ -> Ok(Some first)

    /// 幂等并 1：Suppress / Reanchor（以及任何单例重放型）。
    let private reduceIdempotent (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | first :: _ -> Ok(Some first)

    let private validateStrengthInsertion
        (insertion: StrengthFrameInsertion)
        : Result<StrengthFrameInsertion, ProjectionConflict> =
        if insertion.FrameDigest <> insertion.Bundle.Digest then
            Error(ProjectionConflict.StrengthFrameDigestMismatch insertion.DecisionId)
        else
            match insertion.Visibility, insertion.Anchor with
            | StrengthFrameVisibility.Candidate(target, current), StrengthFrameAnchor.Append ->
                if target <> current then
                    Error(ProjectionConflict.StrengthCandidateWrongTarget insertion.DecisionId)
                else
                    Ok insertion
            | StrengthFrameVisibility.Candidate _, StrengthFrameAnchor.BeforeMessageIndex _ ->
                Error(ProjectionConflict.InvalidStrengthAnchor insertion.DecisionId)
            | StrengthFrameVisibility.Promoted(_, isReplicaRequest), StrengthFrameAnchor.BeforeMessageIndex _ ->
                if isReplicaRequest then
                    Error(ProjectionConflict.StrengthPromotedReplicaReflection insertion.DecisionId)
                else
                    Ok insertion
            | StrengthFrameVisibility.Promoted _, StrengthFrameAnchor.Append ->
                Error(ProjectionConflict.InvalidStrengthAnchor insertion.DecisionId)
            | StrengthFrameVisibility.ReplicaLocal, StrengthFrameAnchor.Append -> Ok insertion
            | StrengthFrameVisibility.ReplicaLocal, StrengthFrameAnchor.BeforeMessageIndex _ ->
                Error(ProjectionConflict.InvalidStrengthAnchor insertion.DecisionId)

    /// Merge InsertStrengthFrames: same DecisionId+digest is idempotent;
    /// same DecisionId different digest/anchor → ConflictingStrengthFrames.
    /// Visibility rules applied per insertion before merge.
    let private reduceStrengthFrames
        (items: ProjectionIntent list)
        : Result<ProjectionIntent option, ProjectionConflict> =
        let rec collect remaining acc =
            match remaining with
            | [] -> Ok(List.rev acc)
            | ProjectionIntent.InsertStrengthFrames payload :: tail ->
                let rec validateAll insertions validated =
                    match insertions with
                    | [] -> Ok(List.rev validated)
                    | head :: rest ->
                        match validateStrengthInsertion head with
                        | Error conflict -> Error conflict
                        | Ok ok -> validateAll rest (ok :: validated)

                match validateAll payload.Items [] with
                | Error conflict -> Error conflict
                | Ok validated -> collect tail (validated @ acc)
            | _ :: tail -> collect tail acc

        match collect items [] with
        | Error conflict -> Error conflict
        | Ok [] -> Ok None
        | Ok insertions ->
            // Stable merge by DecisionId: identical material collapses; conflicts fail closed.
            let rec merge remaining kept =
                match remaining with
                | [] -> Ok(List.rev kept)
                | head :: tail ->
                    match kept |> List.tryFind (fun existing -> existing.DecisionId = head.DecisionId) with
                    | None -> merge tail (head :: kept)
                    | Some existing when existing = head -> merge tail kept
                    | Some _ -> Error(ProjectionConflict.ConflictingStrengthFrames head.DecisionId)

            match merge insertions [] with
            | Error conflict -> Error conflict
            | Ok merged ->
                let canonical =
                    merged
                    |> List.sortBy (fun insertion ->
                        match insertion.Anchor with
                        | StrengthFrameAnchor.BeforeMessageIndex index ->
                            0, index, StrengthDecisionId.value insertion.DecisionId
                        | StrengthFrameAnchor.Append -> 1, 0, StrengthDecisionId.value insertion.DecisionId)

                Ok(Some(ProjectionIntent.InsertStrengthFrames { Items = canonical }))

    let private reduceGroup (items: ProjectionIntent list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | head :: _ ->
            match head with
            | ProjectionIntent.KeepPhysicalPrefix
            | ProjectionIntent.ActivatePrefixEpoch _
            | ProjectionIntent.UseStrengthMirror _ -> reducePrefix items
            | ProjectionIntent.InsertBlogFrames _ -> reduceBlogFrames items
            | ProjectionIntent.InsertRepair _ -> reduceRepair items
            | ProjectionIntent.InsertStrengthFrames _ -> reduceStrengthFrames items
            | ProjectionIntent.AppendReviewChallenge _ -> reduceChallenge items
            | ProjectionIntent.SuppressTransportOnly
            | ProjectionIntent.ReanchorAfterCompaction -> reduceIdempotent items

    /// PROJ-006：汇总各功能意图 → groupBy kind → reduce → sortBy rank。
    ///
    /// 排列无关：同一多重集任意顺序得到同一有序结果或同一冲突。
    let plan (intents: ProjectionIntent list) : Result<ProjectionIntent list, ProjectionConflict> =
        let groups = intents |> List.groupBy kindKey |> List.sortBy fst

        let rec reduceAll remaining acc =
            match remaining with
            | [] -> Ok(List.rev acc)
            | (_, group) :: tail ->
                match reduceGroup group with
                | Error conflict -> Error conflict
                | Ok None -> reduceAll tail acc
                | Ok(Some intent) -> reduceAll tail (intent :: acc)

        match reduceAll groups [] with
        | Error conflict -> Error conflict
        | Ok reduced ->
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
                Ok(reduced |> List.sortBy rank)
