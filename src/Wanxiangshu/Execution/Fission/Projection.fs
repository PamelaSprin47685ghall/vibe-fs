namespace Wanxiangshu.Execution.Fission
open Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type FissionGroupTerminal =
    | Open
    | Converged of
        terminalLane: SessionId *
        providerRun: ProviderRunIdentity *
        aggregateRef: BlobRef *
        aggregateDigest: BlobDigest
    | Failed of reason: string

[<CLIMutable>]
type FissionGroupProjection =
    { GroupId: string
      OwnerSessionId: SessionId
      ParentSessionId: SessionId option
      OriginToolCallId: ToolCallId
      LaneCount: int
      LaneSessions: Map<int, SessionId>
      LanePrompts: Map<int, string>
      OwnerWorkRecordRef: BlobRef
      OwnerWorkRecordDigest: BlobDigest
      PreFissionCompletionIds: Set<string>
      LaneWork: Map<int, BlobRef * BlobDigest>
      LaneProviderRuns: Map<int, ProviderRunIdentity>
      CapturedCompletions: Map<string, BlobRef * BlobDigest>
      CompletionDeliveries: Map<string, Set<int>>
      ExternalAffinities: Map<string, int>
      Terminal: FissionGroupTerminal }

[<CLIMutable>]
type FissionProjectionState =
    { Groups: Map<string, FissionGroupProjection>
      ActiveByOwner: Map<SessionId, string>
      LatestByOwner: Map<SessionId, string>
      LaneOwner: Map<SessionId, SessionId>
      LaneMembership: Map<SessionId, string * int> }

[<RequireQualifiedAccess>]
type FissionProjectionRejection =
    | DuplicateOwnerActive of owner: SessionId
    | ConflictingGroup of groupId: string
    | UnknownGroup of groupId: string
    | InvalidLane of laneIndex: int
    | LaneSessionMismatch of laneIndex: int
    | ConflictingLaneWork of laneIndex: int
    | ConvergenceIncomplete of groupId: string
    | TerminalAlreadySet of groupId: string

module FissionProjection =

    let empty =
        { Groups = Map.empty
          ActiveByOwner = Map.empty
          LatestByOwner = Map.empty
          LaneOwner = Map.empty
          LaneMembership = Map.empty }

    let tryGroup groupId state = Map.tryFind groupId state.Groups

    let tryActiveForOwner owner state =
        state.ActiveByOwner
        |> Map.tryFind owner
        |> Option.bind (fun groupId -> Map.tryFind groupId state.Groups)

    let tryLatestForOwner owner state =
        state.LatestByOwner
        |> Map.tryFind owner
        |> Option.bind (fun groupId -> Map.tryFind groupId state.Groups)

    let tryOwnerOfLane lane state = Map.tryFind lane state.LaneOwner

    let tryMembershipOfLane lane state =
        Map.tryFind lane state.LaneMembership
        |> Option.bind (fun (groupId, laneIndex) ->
            Map.tryFind groupId state.Groups
            |> Option.map (fun group -> group, laneIndex))

    let private allBroadcastsDelivered group =
        group.PreFissionCompletionIds
        |> Set.forall (fun completionId ->
            let delivered =
                group.CompletionDeliveries
                |> Map.tryFind completionId
                |> Option.defaultValue Set.empty

            delivered = Set.ofList [ 0 .. group.LaneCount - 1 ])

    let private convergenceReady group =
        group.LaneWork.Count = group.LaneCount
        && (group.LaneWork |> Map.forall (fun laneIndex _ -> laneIndex >= 0 && laneIndex < group.LaneCount))
        && allBroadcastsDelivered group

    let fold (state: FissionProjectionState) (fact: FissionFactCases) =
        match fact with
        | FissionFactCases.FissionAdmitted payload ->
            match Map.tryFind payload.GroupId state.Groups with
            | Some existing
                when existing.OwnerSessionId = payload.OwnerSessionId
                     && existing.ParentSessionId = payload.ParentSessionId
                     && existing.OriginToolCallId = payload.OriginToolCallId
                     && existing.LaneCount = payload.LaneCount
                     && (existing.LaneSessions |> Map.toList |> List.map snd) = payload.LaneSessions
                     && (existing.LanePrompts |> Map.toList |> List.map snd) = payload.LanePrompts
                     && existing.OwnerWorkRecordRef = payload.OwnerWorkRecordRef
                     && existing.OwnerWorkRecordDigest = payload.OwnerWorkRecordDigest
                     && existing.PreFissionCompletionIds = Set.ofList payload.PreFissionCompletionIds ->
                Ok state
            | Some _ -> Error(FissionProjectionRejection.ConflictingGroup payload.GroupId)
            | None when
                payload.LaneCount < 2
                || List.length payload.LaneSessions <> payload.LaneCount
                || List.length payload.LanePrompts <> payload.LaneCount ->
                Error(FissionProjectionRejection.ConflictingGroup payload.GroupId)
            | None ->
                match Map.tryFind payload.OwnerSessionId state.ActiveByOwner with
                | Some _ -> Error(FissionProjectionRejection.DuplicateOwnerActive payload.OwnerSessionId)
                | None ->
                    let laneSessions =
                        payload.LaneSessions
                        |> List.mapi (fun index lane -> index, lane)
                        |> Map.ofList

                    let group =
                        { GroupId = payload.GroupId
                          OwnerSessionId = payload.OwnerSessionId
                          ParentSessionId = payload.ParentSessionId
                          OriginToolCallId = payload.OriginToolCallId
                          LaneCount = payload.LaneCount
                          LaneSessions = laneSessions
                          LanePrompts = payload.LanePrompts |> List.mapi (fun index prompt -> index, prompt) |> Map.ofList
                          OwnerWorkRecordRef = payload.OwnerWorkRecordRef
                          OwnerWorkRecordDigest = payload.OwnerWorkRecordDigest
                          PreFissionCompletionIds = Set.ofList payload.PreFissionCompletionIds
                          LaneWork = Map.empty
                          LaneProviderRuns = Map.empty
                          CapturedCompletions = Map.empty
                          CompletionDeliveries = Map.empty
                          ExternalAffinities = Map.empty
                          Terminal = FissionGroupTerminal.Open }

                    Ok
                        { state with
                            Groups = Map.add payload.GroupId group state.Groups
                            ActiveByOwner = Map.add payload.OwnerSessionId payload.GroupId state.ActiveByOwner
                            LatestByOwner = Map.add payload.OwnerSessionId payload.GroupId state.LatestByOwner
                            LaneOwner =
                                laneSessions
                                |> Map.fold (fun acc _ lane -> Map.add lane payload.OwnerSessionId acc) state.LaneOwner
                            LaneMembership =
                                laneSessions
                                |> Map.fold
                                    (fun acc laneIndex lane -> Map.add lane (payload.GroupId, laneIndex) acc)
                                    state.LaneMembership }

        | FissionFactCases.FissionLaneMaterialized payload ->
            match Map.tryFind payload.GroupId state.Groups with
            | None -> Error(FissionProjectionRejection.UnknownGroup payload.GroupId)
            | Some group when payload.LaneIndex < 0 || payload.LaneIndex >= group.LaneCount ->
                Error(FissionProjectionRejection.InvalidLane payload.LaneIndex)
            | Some group ->
                match Map.tryFind payload.LaneIndex group.LaneSessions with
                | Some expected when expected <> payload.LaneSessionId ->
                    Error(FissionProjectionRejection.LaneSessionMismatch payload.LaneIndex)
                | None -> Error(FissionProjectionRejection.LaneSessionMismatch payload.LaneIndex)
                | Some _ ->
                    match Map.tryFind payload.LaneIndex group.LaneWork with
                    | Some existing
                        when existing = (payload.WorkRecordRef, payload.WorkRecordDigest)
                             && Map.tryFind payload.LaneIndex group.LaneProviderRuns = Some payload.ProviderRun ->
                        Ok state
                    | Some _ -> Error(FissionProjectionRejection.ConflictingLaneWork payload.LaneIndex)
                    | None ->
                        let next =
                            { group with
                                LaneWork =
                                    Map.add payload.LaneIndex (payload.WorkRecordRef, payload.WorkRecordDigest) group.LaneWork
                                LaneProviderRuns = Map.add payload.LaneIndex payload.ProviderRun group.LaneProviderRuns }

                        Ok { state with Groups = Map.add payload.GroupId next state.Groups }

        | FissionFactCases.FissionCompletionCaptured payload ->
            match Map.tryFind payload.GroupId state.Groups with
            | None -> Error(FissionProjectionRejection.UnknownGroup payload.GroupId)
            | Some group when not (Set.contains payload.CompletionId group.PreFissionCompletionIds) ->
                Error(FissionProjectionRejection.ConflictingGroup payload.GroupId)
            | Some group ->
                match Map.tryFind payload.CompletionId group.CapturedCompletions with
                | Some existing when existing = (payload.PayloadRef, payload.PayloadDigest) -> Ok state
                | Some _ -> Error(FissionProjectionRejection.ConflictingGroup payload.GroupId)
                | None ->
                    let next =
                        { group with
                            CapturedCompletions =
                                Map.add payload.CompletionId (payload.PayloadRef, payload.PayloadDigest) group.CapturedCompletions }

                    Ok { state with Groups = Map.add payload.GroupId next state.Groups }

        | FissionFactCases.FissionCompletionDelivered payload ->
            match Map.tryFind payload.GroupId state.Groups with
            | None -> Error(FissionProjectionRejection.UnknownGroup payload.GroupId)
            | Some group when payload.LaneIndex < 0 || payload.LaneIndex >= group.LaneCount ->
                Error(FissionProjectionRejection.InvalidLane payload.LaneIndex)
            | Some group when not (Map.containsKey payload.CompletionId group.CapturedCompletions) ->
                Error(FissionProjectionRejection.ConflictingGroup payload.GroupId)
            | Some group ->
                let current =
                    group.CompletionDeliveries
                    |> Map.tryFind payload.CompletionId
                    |> Option.defaultValue Set.empty

                let next =
                    { group with
                        CompletionDeliveries =
                            Map.add payload.CompletionId (Set.add payload.LaneIndex current) group.CompletionDeliveries }

                Ok { state with Groups = Map.add payload.GroupId next state.Groups }

        | FissionFactCases.FissionExternalAffinityBound payload ->
            match Map.tryFind payload.GroupId state.Groups with
            | None -> Error(FissionProjectionRejection.UnknownGroup payload.GroupId)
            | Some group when payload.LaneIndex < 0 || payload.LaneIndex >= group.LaneCount ->
                Error(FissionProjectionRejection.InvalidLane payload.LaneIndex)
            | Some group ->
                match Map.tryFind payload.ExternalId group.ExternalAffinities with
                | Some existing when existing = payload.LaneIndex -> Ok state
                | Some _ -> Error(FissionProjectionRejection.ConflictingGroup payload.GroupId)
                | None ->
                    let next =
                        { group with
                            ExternalAffinities =
                                Map.add payload.ExternalId payload.LaneIndex group.ExternalAffinities }

                    Ok { state with Groups = Map.add payload.GroupId next state.Groups }

        | FissionFactCases.FissionConverged payload ->
            match Map.tryFind payload.GroupId state.Groups with
            | None -> Error(FissionProjectionRejection.UnknownGroup payload.GroupId)
            | Some group ->
                match group.Terminal with
                | FissionGroupTerminal.Converged(existingLane, existingRun, existingRef, existingDigest)
                    when existingLane = payload.TerminalLaneSessionId
                         && existingRun = payload.TerminalProviderRun
                         && existingRef = payload.AggregateWorkRecordRef
                         && existingDigest = payload.AggregateWorkRecordDigest ->
                    Ok state
                | FissionGroupTerminal.Converged _
                | FissionGroupTerminal.Failed _ -> Error(FissionProjectionRejection.TerminalAlreadySet payload.GroupId)
                | FissionGroupTerminal.Open when not (convergenceReady group) ->
                    Error(FissionProjectionRejection.ConvergenceIncomplete payload.GroupId)
                | FissionGroupTerminal.Open ->
                    let next =
                        { group with
                            Terminal =
                                FissionGroupTerminal.Converged(
                                    payload.TerminalLaneSessionId,
                                    payload.TerminalProviderRun,
                                    payload.AggregateWorkRecordRef,
                                    payload.AggregateWorkRecordDigest
                                ) }

                    Ok
                        { state with
                            Groups = Map.add payload.GroupId next state.Groups
                            ActiveByOwner = Map.remove group.OwnerSessionId state.ActiveByOwner }

        | FissionFactCases.FissionFailed payload ->
            match Map.tryFind payload.GroupId state.Groups with
            | None -> Error(FissionProjectionRejection.UnknownGroup payload.GroupId)
            | Some group ->
                match group.Terminal with
                | FissionGroupTerminal.Failed existing when existing = payload.Reason -> Ok state
                | FissionGroupTerminal.Open ->
                    let next = { group with Terminal = FissionGroupTerminal.Failed payload.Reason }

                    Ok
                        { state with
                            Groups = Map.add payload.GroupId next state.Groups
                            ActiveByOwner = Map.remove group.OwnerSessionId state.ActiveByOwner }
                | _ -> Error(FissionProjectionRejection.TerminalAlreadySet payload.GroupId)
