namespace Wanxiangshu.Execution.Fission

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
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

open System

[<RequireQualifiedAccess>]
type FissionRejectReason =
    | AlreadyFissioned
    | TooFewLanes
    | EmptyLanePrompt of int
    | CapacityExceeded
    | InvalidOrigin
    | RuntimeUnavailable of string

[<CLIMutable>]
type FissionLanePrompt = { Index: int; Prompt: string }

[<CLIMutable>]
type ParsedFissionPrompts =
    { Count: int
      Lanes: FissionLanePrompt list }

module FissionPrompt =

    let private normalizeNewlines (value: string) =
        let normalized =
            if isNull value then
                ""
            else
                value.Replace("\r\n", "\n").Replace("\r", "\n")

        if normalized.EndsWith("\n", StringComparison.Ordinal) then
            normalized.Substring(0, normalized.Length - 1)
        else
            normalized

    /// INTRA-PARTICIPANT-PARALLELISM-002: a line is a lane. Preserve every byte
    /// except newline normalization and one ergonomic trailing LF.
    let private validateLanePrompts lines =
        match lines |> List.tryFindIndex String.IsNullOrWhiteSpace with
        | Some index -> Error(FissionRejectReason.EmptyLanePrompt index)
        | None -> Ok()

    let parse (value: string) : Result<ParsedFissionPrompts, FissionRejectReason> =
        let normalized = normalizeNewlines value
        let lines = normalized.Split('\n') |> Array.toList

        if List.length lines < 2 then
            Error FissionRejectReason.TooFewLanes
        else
            validateLanePrompts lines
            |> Result.map (fun () ->
                { Count = List.length lines
                  Lanes = lines |> List.mapi (fun index prompt -> { Index = index; Prompt = prompt }) })

[<RequireQualifiedAccess>]
type FissionCompletionAffinity =
    | PreFissionBroadcast
    | Lane of int

module FissionCompletionAffinity =
    let preFission = FissionCompletionAffinity.PreFissionBroadcast
    let lane index = FissionCompletionAffinity.Lane index

module FissionExternalId =
    let agent handleId = "agent:" + handleId
    let pty ptyId = "pty:" + ptyId

module FissionCompletionRouting =

    let targets laneCount affinity =
        match affinity with
        | FissionCompletionAffinity.PreFissionBroadcast -> [ 0 .. laneCount - 1 ]
        | FissionCompletionAffinity.Lane index when index >= 0 && index < laneCount -> [ index ]
        | FissionCompletionAffinity.Lane _ -> []

[<RequireQualifiedAccess>]
type FissionDeliveryError = InvalidLane of int

[<CLIMutable>]
type FissionDelivery =
    { LaneCount: int
      Delivered: Map<string, Set<int>> }

module FissionDelivery =

    let empty laneCount =
        { LaneCount = max 0 laneCount
          Delivered = Map.empty }

    let mark completionId laneIndex delivery =
        if laneIndex < 0 || laneIndex >= delivery.LaneCount then
            Error(FissionDeliveryError.InvalidLane laneIndex)
        else
            let current =
                delivery.Delivered |> Map.tryFind completionId |> Option.defaultValue Set.empty

            Ok
                { delivery with
                    Delivered = Map.add completionId (Set.add laneIndex current) delivery.Delivered }

    let pendingTargets completionId delivery =
        let delivered =
            delivery.Delivered |> Map.tryFind completionId |> Option.defaultValue Set.empty

        [ 0 .. delivery.LaneCount - 1 ]
        |> List.filter (fun lane -> not (Set.contains lane delivered))

[<RequireQualifiedAccess>]
type FissionBundleError = ConflictingLaneRecord of laneIndex: int * existingRef: string * proposedRef: string

[<Struct>]
type FissionWorkBundle = private FissionWorkBundle of Map<int, string>

module FissionWorkBundle =

    let empty = FissionWorkBundle Map.empty

    let private value (FissionWorkBundle records) = records

    let add laneIndex workRecordRef bundle =
        let records = value bundle

        match Map.tryFind laneIndex records with
        | None -> Ok(FissionWorkBundle(Map.add laneIndex workRecordRef records))
        | Some existing when existing = workRecordRef -> Ok bundle
        | Some existing -> Error(FissionBundleError.ConflictingLaneRecord(laneIndex, existing, workRecordRef))

    let merge left right =
        let folder state (laneIndex, workRecordRef) =
            state |> Result.bind (add laneIndex workRecordRef)

        value right |> Map.toList |> List.fold folder (Ok left)

    let keys bundle =
        value bundle |> Map.toList |> List.map fst

    let entries bundle = value bundle |> Map.toList

    let count bundle = value bundle |> Map.count

module FissionConvergence =

    let ready laneCount preFissionCompletionIds bundle delivery =
        let completeLaneSet = FissionWorkBundle.keys bundle = [ 0 .. laneCount - 1 ]

        let allBroadcastsDelivered =
            preFissionCompletionIds
            |> List.forall (fun completionId -> FissionDelivery.pendingTargets completionId delivery |> List.isEmpty)

        laneCount >= 2 && completeLaneSet && allBroadcastsDelivered
