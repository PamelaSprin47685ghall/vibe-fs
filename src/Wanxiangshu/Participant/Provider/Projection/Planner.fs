namespace Wanxiangshu.Participant.Provider.Projection

[<RequireQualifiedAccess>]
module ProjectionPlanner =

    let private insertionSortKey (insertion: ProjectionMessageInsertion) =
        match insertion.Anchor with
        | ProjectionMessageAnchor.BeforeMessageIndex index -> 0, index, insertion.Key
        | ProjectionMessageAnchor.Append -> 1, 0, insertion.Key

    let private reduceBase (items: ProjectionMessageBase list) : Result<ProjectionIntent option, ProjectionConflict> =
        match items with
        | [] -> Ok None
        | head :: tail when tail |> List.forall ((=) head) -> Ok(Some(ProjectionIntent.ReplaceMessageBase head))
        | _ -> Error ProjectionConflict.ConflictingMessageBase

    let private reduceInsertions
        (items: ProjectionMessageInsertion list)
        : Result<ProjectionIntent list, ProjectionConflict> =
        let groups =
            items |> List.groupBy (fun insertion -> insertion.Key) |> List.sortBy fst

        let rec reduce remaining acc =
            match remaining with
            | [] ->
                acc
                |> List.sortBy insertionSortKey
                |> List.map ProjectionIntent.InsertMessageRows
                |> Ok
            | (_, head :: tail) :: rest when tail |> List.forall ((=) head) -> reduce rest (head :: acc)
            | (key, _) :: _ -> Error(ProjectionConflict.ConflictingMessageRows key)

        reduce groups []

    /// Dedupe owner keys, reject conflicts, and return base then canonically sorted insertions.
    /// The result is invariant under registration order for every conflict-free multiset.
    let plan (intents: ProjectionIntent list) : Result<ProjectionIntent list, ProjectionConflict> =
        let bases =
            intents
            |> List.choose (function
                | ProjectionIntent.ReplaceMessageBase replacement -> Some replacement
                | _ -> None)

        let insertions =
            intents
            |> List.choose (function
                | ProjectionIntent.InsertMessageRows insertion -> Some insertion
                | _ -> None)

        reduceBase bases
        |> Result.bind (fun replacement ->
            reduceInsertions insertions
            |> Result.map (fun orderedInsertions ->
                match replacement with
                | Some baseIntent -> baseIntent :: orderedInsertions
                | None -> orderedInsertions))
