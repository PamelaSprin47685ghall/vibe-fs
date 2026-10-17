namespace Wanxiangshu.Mission.WorkRecord

open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity

type private WorkRecordFrameNode =
    { Frame: BlogFrame
      CapturedBy: EventId option
      HasCausalReplacement: bool
      Replaced: WorkRecordFrameNode list }

type private CompletedWorkRecordRepresentation =
    { Frontier: XTraceCursor
      ProviderRun: ProviderRunIdentity
      CapturedBy: EventId option
      FramesNewestFirst: Result<BlogFrame list, unit> }

type CompletedWorkRecordProjectionState =
    private
        { FramesNewestFirst: WorkRecordFrameNode list
          CurrentFramesNewestFirst: BlogFrame list
          FrameWitnesses: EventId list
          FrameShortcut: EventId option
          InvalidFrameCount: int
          ByProviderRun: Map<ProviderRunIdentity, CompletedWorkRecordRepresentation list>
          FirstAtFrontier: Map<int64, CompletedWorkRecordRepresentation list>
          UnprovenRuns: Set<ProviderRunIdentity>
          UnprovenThrough: XTraceCursor option }

module CompletedWorkRecordProjection =
    let empty =
        { FramesNewestFirst = []
          CurrentFramesNewestFirst = []
          FrameWitnesses = []
          FrameShortcut = None
          InvalidFrameCount = 0
          ByProviderRun = Map.empty
          FirstAtFrontier = Map.empty
          UnprovenRuns = Set.empty
          UnprovenThrough = None }

    let private isKnown contains capturedBy =
        capturedBy |> Option.map contains |> Option.defaultValue true

    let private allFramesVisible contains state =
        state.InvalidFrameCount = 0
        && ((state.FrameShortcut |> Option.exists contains)
            || (state.FrameWitnesses |> List.forall contains))

    let private nextFrameWitnesses causality contains state =
        match causality with
        | None -> state.FrameWitnesses
        | Some(eventId, _) when allFramesVisible contains state -> [ eventId ]
        | Some(eventId, _) -> eventId :: (state.FrameWitnesses |> List.filter (contains >> not))

    let recordFrame causality replacedCount frame currentFramesNewestFirst state =
        let oldestFirst =
            if replacedCount = 0 then
                []
            else
                List.rev state.FramesNewestFirst

        let replaced = List.truncate replacedCount oldestFirst
        let contains = causality |> Option.map snd |> Option.defaultValue (fun _ -> true)

        let node =
            { Frame = frame
              CapturedBy = causality |> Option.map fst
              HasCausalReplacement =
                allFramesVisible contains state
                || (replaced
                    |> List.forall (fun item -> item.HasCausalReplacement && isKnown contains item.CapturedBy))
              Replaced = List.rev replaced }

        { state with
            CurrentFramesNewestFirst = currentFramesNewestFirst
            FrameWitnesses = nextFrameWitnesses causality contains state
            FrameShortcut = None
            InvalidFrameCount =
                state.InvalidFrameCount
                - (replaced |> List.sumBy (fun item -> if item.HasCausalReplacement then 0 else 1))
                + (if node.HasCausalReplacement then 0 else 1)
            FramesNewestFirst =
                if replacedCount = 0 then
                    node :: state.FramesNewestFirst
                else
                    List.rev (node :: List.skip replacedCount oldestFirst) }

    let markUnproven providerRun observedHead state =
        let through =
            state.UnprovenThrough
            |> Option.filter (fun previous -> XTraceCursor.isAfter previous observedHead)
            |> Option.defaultValue observedHead

        { state with
            UnprovenRuns = Set.add providerRun state.UnprovenRuns
            UnprovenThrough = Some through }

    let private addCausalFirst contains completed candidates =
        if candidates |> List.exists (fun prior -> isKnown contains prior.CapturedBy) then
            candidates
        else
            completed :: candidates

    let private selectFrames contains roots =
        let rec collect (pending: WorkRecordFrameNode list list) selected =
            match pending with
            | [] -> Ok selected
            | [] :: rest -> collect rest selected
            | (node :: tail) :: rest when not (isKnown contains node.CapturedBy) ->
                collect (node.Replaced :: tail :: rest) selected
            | (node :: tail) :: rest when node.HasCausalReplacement -> collect (tail :: rest) (node.Frame :: selected)
            | _ -> Error()

        collect [ roots ] [] |> Result.map List.rev

    let private completionFrames contains allVisible state =
        if allVisible then
            Ok state.CurrentFramesNewestFirst
        else
            selectFrames contains state.FramesNewestFirst

    let capture causality providerRun frontier state =
        let contains = causality |> Option.map snd |> Option.defaultValue (fun _ -> true)
        let allVisible = allFramesVisible contains state

        let completed =
            { Frontier = frontier
              ProviderRun = providerRun
              CapturedBy = causality |> Option.map fst
              FramesNewestFirst = completionFrames contains allVisible state }

        let prior = Map.tryFind providerRun state.ByProviderRun |> Option.defaultValue []
        let sequence = XTraceCursor.sequence frontier
        let first = Map.tryFind sequence state.FirstAtFrontier |> Option.defaultValue []

        { state with
            FrameShortcut =
                if allVisible then
                    causality |> Option.map fst
                else
                    state.FrameShortcut
            ByProviderRun = Map.add providerRun (addCausalFirst contains completed prior) state.ByProviderRun
            FirstAtFrontier = Map.add sequence (addCausalFirst contains completed first) state.FirstAtFrontier }

    let private sharedFrames (candidates: CompletedWorkRecordRepresentation list) =
        candidates
        |> List.fold
            (fun result candidate ->
                result
                |> Result.bind (fun previous ->
                    candidate.FramesNewestFirst
                    |> Result.bind (fun current ->
                        match previous with
                        | Some selected when selected <> current -> Error()
                        | _ -> Ok(Some current))))
            (Ok None)
        |> Result.map (Option.map List.rev)

    let private framesForProviderRun run sequence state =
        let candidates = Map.tryFind run state.ByProviderRun |> Option.defaultValue []

        match candidates with
        | first :: _ when
            candidates
            |> List.exists (fun completed -> completed.Frontier <> first.Frontier)
            ->
            Error()
        | first :: _ when XTraceCursor.sequence first.Frontier <> sequence -> Ok None
        | _ -> sharedFrames candidates

    let private framesAtFrontier endExclusive sequence state =
        let candidates =
            Map.tryFind sequence state.FirstAtFrontier |> Option.defaultValue []

        let unprovenBoundary =
            state.UnprovenThrough
            |> Option.exists (fun through -> not (XTraceCursor.isAfter endExclusive through))

        if
            (List.isEmpty candidates && unprovenBoundary)
            || (candidates
                |> List.exists (fun completed -> Set.contains completed.ProviderRun state.UnprovenRuns))
        then
            Error()
        else
            sharedFrames candidates

    let framesForRange providerRun endExclusive state =
        let sequence = XTraceCursor.sequence endExclusive

        match providerRun with
        | Some run when Set.contains run state.UnprovenRuns -> Error()
        | Some run -> framesForProviderRun run sequence state
        | None -> framesAtFrontier endExclusive sequence state
