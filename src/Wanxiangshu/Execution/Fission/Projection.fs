namespace Wanxiangshu.Execution.Fission

open Fable.Core.JsInterop
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
type FissionTakeoverProjection =
    { LaneIndex: int
      LaneSessionId: SessionId
      PromptKey: PromptKey option
      PhysicalUserMessageId: PhysicalUserMessageId option
      AggregateViewRef: BlobRef
      AggregateViewDigest: BlobDigest }

[<CLIMutable>]
type FissionGroupProjection =
    { GroupId: string
      OwnerSessionId: SessionId
      ParentSessionId: SessionId option
      OriginToolCallId: ToolCallId
      LaneCount: int
      LaneSessions: Map<int, SessionId>
      LanePrompts: Map<int, string>
      OwnerViewRef: BlobRef
      OwnerViewDigest: BlobDigest
      PreFissionCompletionIds: Set<string>
      LaneWork: Map<int, BlobRef * BlobDigest>
      LaneProviderRuns: Map<int, ProviderRunIdentity>
      CapturedCompletions: Map<string, BlobRef * BlobDigest>
      CompletionDeliveries: Map<string, Set<int>>
      ExternalAffinities: Map<string, int>
      Takeover: FissionTakeoverProjection option
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
    | ConflictingTakeover of groupId: string
    | ConvergenceIncomplete of groupId: string
    | TerminalAlreadySet of groupId: string

module FissionProjection =

    type private AdmittedPayload =
        {| GroupId: string
           OwnerSessionId: SessionId
           ParentSessionId: SessionId option
           OriginToolCallId: ToolCallId
           LaneCount: int
           LaneSessions: SessionId list
           LanePrompts: string list
           OwnerViewRef: BlobRef
           OwnerViewDigest: BlobDigest
           PreFissionCompletionIds: string list |}

    type private LaneMaterializedPayload =
        {| GroupId: string
           OwnerSessionId: SessionId
           LaneIndex: int
           LaneSessionId: SessionId
           ProviderRun: ProviderRunIdentity
           ViewRef: BlobRef
           ViewDigest: BlobDigest |}

    type private CompletionCapturedPayload =
        {| GroupId: string
           OwnerSessionId: SessionId
           CompletionId: string
           PayloadRef: BlobRef
           PayloadDigest: BlobDigest |}

    type private CompletionDeliveredPayload =
        {| GroupId: string
           OwnerSessionId: SessionId
           CompletionId: string
           LaneIndex: int |}

    type private ExternalAffinityPayload =
        {| GroupId: string
           OwnerSessionId: SessionId
           ExternalId: string
           LaneIndex: int |}

    type private TakeoverStartedPayload =
        {| GroupId: string
           OwnerSessionId: SessionId
           LaneIndex: int
           LaneSessionId: SessionId
           PhysicalUserMessageId: PhysicalUserMessageId
           AggregateViewRef: BlobRef
           AggregateViewDigest: BlobDigest |}

    type private TakeoverClaimedPayload =
        {| GroupId: string
           OwnerSessionId: SessionId
           LaneIndex: int
           LaneSessionId: SessionId
           PromptKey: PromptKey
           AggregateViewRef: BlobRef
           AggregateViewDigest: BlobDigest |}

    type private ConvergedPayload =
        {| GroupId: string
           OwnerSessionId: SessionId
           TerminalLaneSessionId: SessionId
           TerminalProviderRun: ProviderRunIdentity
           AggregateViewRef: BlobRef
           AggregateViewDigest: BlobDigest |}

    type private FailedPayload =
        {| GroupId: string
           OwnerSessionId: SessionId
           Reason: string |}

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
            Map.tryFind groupId state.Groups |> Option.map (fun group -> group, laneIndex))

    /// CUT-28: which Fission facts a group accepts, which are idempotent replays
    /// and which are refused (and in which order) is wanxiangli/fission-group's
    /// law, as is the convergence precondition. This module keeps the group
    /// maps, compares the values it holds (sessions, refs, digests, runs) and
    /// writes what the law decides.
    [<Fable.Core.Import("FissionGroupLaw", "wanxiangli/fission-group")>]
    let private lawClass: obj = Fable.Core.Util.jsNative

    [<Fable.Core.Emit("new $0()")>]
    let private construct (_cls: obj) : obj = Fable.Core.Util.jsNative

    let private law: obj = construct lawClass

    let private heldOf (value: 'a option) (same: 'a -> bool) : obj =
        match value with
        | None -> null
        | Some v when same v -> box "same"
        | Some _ -> box "different"

    let private progressOf (group: FissionGroupProjection) =
        createObj
            [ "workedLanes" ==> (group.LaneWork |> Map.toList |> List.map fst |> List.toArray)
              "deliveries"
              ==> (group.PreFissionCompletionIds
                   |> Set.toArray
                   |> Array.map (fun completionId ->
                       group.CompletionDeliveries
                       |> Map.tryFind completionId
                       |> Option.defaultValue Set.empty
                       |> Set.toArray)) ]

    /// The one convergence precondition (wanxiangli/fission-group
    /// `convergenceReady`) over a folded group: every lane delivered its work
    /// and every pre-Fission completion reached every lane. Hosts ask this
    /// instead of re-deriving it from the group maps.
    let convergenceReady (group: FissionGroupProjection) =
        unbox<bool> (law?convergenceReady (createObj [ "laneCount" ==> group.LaneCount; "progress" ==> progressOf group ]))

    let private laneSessionOf (group: FissionGroupProjection) laneIndex (laneSessionId: SessionId) =
        match Map.tryFind laneIndex group.LaneSessions with
        | Some expected when expected = laneSessionId -> "match"
        | Some _ -> "mismatch"
        | None -> "absent"

    let private laneCountOf (group: FissionGroupProjection option) : obj =
        match group with
        | Some group -> box group.LaneCount
        | None -> null

    let private terminalKind (terminal: FissionGroupTerminal) =
        match terminal with
        | FissionGroupTerminal.Open -> "Open"
        | FissionGroupTerminal.Converged _ -> "Converged"
        | FissionGroupTerminal.Failed _ -> "Failed"

    [<RequireQualifiedAccess>]
    type private Verdict =
        | Set
        | Keep
        | Reject of string

    let private judge (input: (string * obj) list) =
        let verdict: obj = law?judge (createObj input)

        match unbox<string> verdict?verdict with
        | "set" -> Verdict.Set
        | "keep" -> Verdict.Keep
        | "reject" -> Verdict.Reject(unbox<string> verdict?rejection)
        | other -> invalidOp ("wanxiangli/fission-group returned an unknown verdict: " + other)

    let private rejection groupId owner laneIndex (name: string) =
        match name with
        | "UnknownGroup" -> FissionProjectionRejection.UnknownGroup groupId
        | "ConflictingGroup" -> FissionProjectionRejection.ConflictingGroup groupId
        | "DuplicateOwnerActive" -> FissionProjectionRejection.DuplicateOwnerActive owner
        | "InvalidLane" -> FissionProjectionRejection.InvalidLane laneIndex
        | "LaneSessionMismatch" -> FissionProjectionRejection.LaneSessionMismatch laneIndex
        | "ConflictingLaneWork" -> FissionProjectionRejection.ConflictingLaneWork laneIndex
        | "ConflictingTakeover" -> FissionProjectionRejection.ConflictingTakeover groupId
        | "ConvergenceIncomplete" -> FissionProjectionRejection.ConvergenceIncomplete groupId
        | "TerminalAlreadySet" -> FissionProjectionRejection.TerminalAlreadySet groupId
        | other -> invalidOp ("wanxiangli/fission-group returned an unknown rejection: " + other)

    /// Apply a verdict: `write` runs only on Set and receives the (known) group.
    let private apply
        (state: FissionProjectionState)
        groupId
        owner
        laneIndex
        (group: FissionGroupProjection option)
        verdict
        (write: FissionGroupProjection -> FissionProjectionState)
        =
        match verdict, group with
        | Verdict.Keep, _ -> Ok state
        | Verdict.Reject name, _ -> Error(rejection groupId owner laneIndex name)
        | Verdict.Set, Some group -> Ok(write group)
        | Verdict.Set, None -> invalidOp "wanxiangli/fission-group set a group it was told is unknown"

    let private replaceGroup (state: FissionProjectionState) groupId (group: FissionGroupProjection) =
        { state with
            Groups = Map.add groupId group state.Groups }

    let private admissionMatches (existing: FissionGroupProjection) (payload: AdmittedPayload) =
        existing.OwnerSessionId = payload.OwnerSessionId
        && existing.ParentSessionId = payload.ParentSessionId
        && existing.OriginToolCallId = payload.OriginToolCallId
        && existing.LaneCount = payload.LaneCount
        && (existing.LaneSessions |> Map.toList |> List.map snd) = payload.LaneSessions
        && (existing.LanePrompts |> Map.toList |> List.map snd) = payload.LanePrompts
        && existing.OwnerViewRef = payload.OwnerViewRef
        && existing.OwnerViewDigest = payload.OwnerViewDigest
        && existing.PreFissionCompletionIds = Set.ofList payload.PreFissionCompletionIds

    let private admitFresh (state: FissionProjectionState) (payload: AdmittedPayload) =
        let laneSessions =
            payload.LaneSessions |> List.mapi (fun index lane -> index, lane) |> Map.ofList

        let group =
            { GroupId = payload.GroupId
              OwnerSessionId = payload.OwnerSessionId
              ParentSessionId = payload.ParentSessionId
              OriginToolCallId = payload.OriginToolCallId
              LaneCount = payload.LaneCount
              LaneSessions = laneSessions
              LanePrompts =
                payload.LanePrompts
                |> List.mapi (fun index prompt -> index, prompt)
                |> Map.ofList
              OwnerViewRef = payload.OwnerViewRef
              OwnerViewDigest = payload.OwnerViewDigest
              PreFissionCompletionIds = Set.ofList payload.PreFissionCompletionIds
              LaneWork = Map.empty
              LaneProviderRuns = Map.empty
              CapturedCompletions = Map.empty
              CompletionDeliveries = Map.empty
              ExternalAffinities = Map.empty
              Takeover = None
              Terminal = FissionGroupTerminal.Open }

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

    let private foldAdmitted (state: FissionProjectionState) (payload: AdmittedPayload) =
        let existing = Map.tryFind payload.GroupId state.Groups

        let verdict =
            judge
                [ "event" ==> "admitted"
                  "existing" ==> heldOf existing (fun group -> admissionMatches group payload)
                  "laneCount" ==> payload.LaneCount
                  "sessionCount" ==> List.length payload.LaneSessions
                  "promptCount" ==> List.length payload.LanePrompts
                  "ownerActive" ==> Map.containsKey payload.OwnerSessionId state.ActiveByOwner ]

        match verdict with
        | Verdict.Keep -> Ok state
        | Verdict.Reject name -> Error(rejection payload.GroupId payload.OwnerSessionId 0 name)
        | Verdict.Set -> Ok(admitFresh state payload)

    let private foldLaneMaterialized (state: FissionProjectionState) (payload: LaneMaterializedPayload) =
        let group = Map.tryFind payload.GroupId state.Groups

        let verdict =
            judge
                [ "event" ==> "laneMaterialized"
                  "laneCount" ==> laneCountOf group
                  "laneIndex" ==> payload.LaneIndex
                  "laneSession"
                  ==> (group
                       |> Option.map (fun g -> laneSessionOf g payload.LaneIndex payload.LaneSessionId)
                       |> Option.defaultValue "absent")
                  "work"
                  ==> (group
                       |> Option.bind (fun g -> Map.tryFind payload.LaneIndex g.LaneWork |> Option.map (fun w -> g, w))
                       |> fun held ->
                           heldOf held (fun (g, work) ->
                               work = (payload.ViewRef, payload.ViewDigest)
                               && Map.tryFind payload.LaneIndex g.LaneProviderRuns = Some payload.ProviderRun)) ]

        apply state payload.GroupId payload.OwnerSessionId payload.LaneIndex group verdict (fun group ->
            replaceGroup
                state
                payload.GroupId
                { group with
                    LaneWork = Map.add payload.LaneIndex (payload.ViewRef, payload.ViewDigest) group.LaneWork
                    LaneProviderRuns = Map.add payload.LaneIndex payload.ProviderRun group.LaneProviderRuns })

    let private foldCompletionCaptured (state: FissionProjectionState) (payload: CompletionCapturedPayload) =
        let group = Map.tryFind payload.GroupId state.Groups

        let verdict =
            judge
                [ "event" ==> "completionCaptured"
                  "laneCount" ==> laneCountOf group
                  "preFission"
                  ==> (group
                       |> Option.exists (fun g -> Set.contains payload.CompletionId g.PreFissionCompletionIds))
                  "captured"
                  ==> heldOf
                          (group |> Option.bind (fun g -> Map.tryFind payload.CompletionId g.CapturedCompletions))
                          (fun existing -> existing = (payload.PayloadRef, payload.PayloadDigest)) ]

        apply state payload.GroupId payload.OwnerSessionId 0 group verdict (fun group ->
            replaceGroup
                state
                payload.GroupId
                { group with
                    CapturedCompletions =
                        Map.add payload.CompletionId (payload.PayloadRef, payload.PayloadDigest) group.CapturedCompletions })

    let private foldCompletionDelivered (state: FissionProjectionState) (payload: CompletionDeliveredPayload) =
        let group = Map.tryFind payload.GroupId state.Groups

        let verdict =
            judge
                [ "event" ==> "completionDelivered"
                  "laneCount" ==> laneCountOf group
                  "laneIndex" ==> payload.LaneIndex
                  "captured"
                  ==> (group
                       |> Option.exists (fun g -> Map.containsKey payload.CompletionId g.CapturedCompletions)) ]

        apply state payload.GroupId payload.OwnerSessionId payload.LaneIndex group verdict (fun group ->
            let current =
                group.CompletionDeliveries
                |> Map.tryFind payload.CompletionId
                |> Option.defaultValue Set.empty

            replaceGroup
                state
                payload.GroupId
                { group with
                    CompletionDeliveries =
                        Map.add payload.CompletionId (Set.add payload.LaneIndex current) group.CompletionDeliveries })

    let private foldExternalAffinityBound (state: FissionProjectionState) (payload: ExternalAffinityPayload) =
        let group = Map.tryFind payload.GroupId state.Groups

        let verdict =
            judge
                [ "event" ==> "externalAffinityBound"
                  "laneCount" ==> laneCountOf group
                  "laneIndex" ==> payload.LaneIndex
                  "bound"
                  ==> heldOf
                          (group |> Option.bind (fun g -> Map.tryFind payload.ExternalId g.ExternalAffinities))
                          (fun existing -> existing = payload.LaneIndex) ]

        apply state payload.GroupId payload.OwnerSessionId payload.LaneIndex group verdict (fun group ->
            replaceGroup
                state
                payload.GroupId
                { group with
                    ExternalAffinities = Map.add payload.ExternalId payload.LaneIndex group.ExternalAffinities })

    let private takeoverMatches
        (existing: FissionTakeoverProjection)
        laneIndex
        laneSessionId
        promptKey
        physicalUserMessageId
        aggregateViewRef
        aggregateViewDigest
        =
        existing.LaneIndex = laneIndex
        && existing.LaneSessionId = laneSessionId
        && existing.PromptKey = promptKey
        && existing.PhysicalUserMessageId = physicalUserMessageId
        && existing.AggregateViewRef = aggregateViewRef
        && existing.AggregateViewDigest = aggregateViewDigest

    let private foldTakeover
        (state: FissionProjectionState)
        groupId
        owner
        laneIndex
        laneSessionId
        promptKey
        physicalUserMessageId
        aggregateViewRef
        aggregateViewDigest
        =
        let takeover: FissionTakeoverProjection =
            { LaneIndex = laneIndex
              LaneSessionId = laneSessionId
              PromptKey = promptKey
              PhysicalUserMessageId = physicalUserMessageId
              AggregateViewRef = aggregateViewRef
              AggregateViewDigest = aggregateViewDigest }

        let group = Map.tryFind groupId state.Groups

        let verdict =
            judge
                [ "event" ==> "takeover"
                  "laneCount" ==> laneCountOf group
                  "laneIndex" ==> laneIndex
                  "laneSession"
                  ==> (group
                       |> Option.map (fun g -> laneSessionOf g laneIndex laneSessionId)
                       |> Option.defaultValue "absent")
                  "progress"
                  ==> (group
                       |> Option.map progressOf
                       |> Option.defaultValue (createObj [ "workedLanes" ==> [||]; "deliveries" ==> [||] ]))
                  "takeover"
                  ==> heldOf (group |> Option.bind (fun g -> g.Takeover)) (fun existing ->
                      takeoverMatches
                          existing
                          laneIndex
                          laneSessionId
                          promptKey
                          physicalUserMessageId
                          aggregateViewRef
                          aggregateViewDigest) ]

        apply state groupId owner laneIndex group verdict (fun group ->
            replaceGroup state groupId { group with Takeover = Some takeover })

    let private foldTakeoverClaimed (state: FissionProjectionState) (payload: TakeoverClaimedPayload) =
        foldTakeover
            state
            payload.GroupId
            payload.OwnerSessionId
            payload.LaneIndex
            payload.LaneSessionId
            (Some payload.PromptKey)
            None
            payload.AggregateViewRef
            payload.AggregateViewDigest

    let private foldTakeoverStarted (state: FissionProjectionState) (payload: TakeoverStartedPayload) =
        foldTakeover
            state
            payload.GroupId
            payload.OwnerSessionId
            payload.LaneIndex
            payload.LaneSessionId
            None
            (Some payload.PhysicalUserMessageId)
            payload.AggregateViewRef
            payload.AggregateViewDigest

    let private close (state: FissionProjectionState) groupId (group: FissionGroupProjection) terminal =
        { state with
            Groups = Map.add groupId { group with Terminal = terminal } state.Groups
            ActiveByOwner = Map.remove group.OwnerSessionId state.ActiveByOwner }

    let private foldConverged (state: FissionProjectionState) (payload: ConvergedPayload) =
        let group = Map.tryFind payload.GroupId state.Groups

        let verdict =
            match group with
            | None -> judge [ "event" ==> "converged"; "laneCount" ==> null ]
            | Some g ->
                judge
                    [ "event" ==> "converged"
                      "laneCount" ==> g.LaneCount
                      "terminal" ==> terminalKind g.Terminal
                      "terminalSame"
                      ==> (match g.Terminal with
                           | FissionGroupTerminal.Converged(lane, run, ref, digest) ->
                               lane = payload.TerminalLaneSessionId
                               && run = payload.TerminalProviderRun
                               && ref = payload.AggregateViewRef
                               && digest = payload.AggregateViewDigest
                           | _ -> false)
                      "progress" ==> progressOf g
                      "takeoverLane"
                      ==> (match g.Takeover with
                           | Some takeover when takeover.LaneSessionId = payload.TerminalLaneSessionId -> box "match"
                           | Some _ -> box "mismatch"
                           | None -> null) ]

        apply state payload.GroupId payload.OwnerSessionId 0 group verdict (fun group ->
            close
                state
                payload.GroupId
                group
                (FissionGroupTerminal.Converged(
                    payload.TerminalLaneSessionId,
                    payload.TerminalProviderRun,
                    payload.AggregateViewRef,
                    payload.AggregateViewDigest
                )))

    let private foldFailed (state: FissionProjectionState) (payload: FailedPayload) =
        let group = Map.tryFind payload.GroupId state.Groups

        let verdict =
            match group with
            | None -> judge [ "event" ==> "failed"; "laneCount" ==> null ]
            | Some g ->
                judge
                    [ "event" ==> "failed"
                      "laneCount" ==> g.LaneCount
                      "terminal" ==> terminalKind g.Terminal
                      "terminalSame"
                      ==> (match g.Terminal with
                           | FissionGroupTerminal.Failed existing -> existing = payload.Reason
                           | _ -> false) ]

        apply state payload.GroupId payload.OwnerSessionId 0 group verdict (fun group ->
            close state payload.GroupId group (FissionGroupTerminal.Failed payload.Reason))

    let fold (state: FissionProjectionState) (fact: FissionFactCases) =
        match fact with
        | FissionFactCases.FissionAdmitted payload -> foldAdmitted state payload
        | FissionFactCases.FissionLaneMaterialized payload -> foldLaneMaterialized state payload
        | FissionFactCases.FissionCompletionCaptured payload -> foldCompletionCaptured state payload
        | FissionFactCases.FissionCompletionDelivered payload -> foldCompletionDelivered state payload
        | FissionFactCases.FissionExternalAffinityBound payload -> foldExternalAffinityBound state payload
        | FissionFactCases.FissionTakeoverClaimed payload -> foldTakeoverClaimed state payload
        | FissionFactCases.FissionTakeoverStarted payload -> foldTakeoverStarted state payload
        | FissionFactCases.FissionConverged payload -> foldConverged state payload
        | FissionFactCases.FissionFailed payload -> foldFailed state payload
