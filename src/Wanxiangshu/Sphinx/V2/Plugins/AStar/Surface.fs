namespace Wanxiangshu.Sphinx.V2.Plugins

open System

/// The JS-native conformance surface for the A* refiner.
module Surface =

    let listOfItems (items: 'a list) : 'a list = items |> List.ofSeq

    let stringFloatMapOf (entries: (string * float) list) : Map<string, float> = Map.ofSeq entries

    let isOk (result: Result<'value, 'error>) : bool =
        match result with
        | Ok _ -> true
        | Error _ -> false

    let isError (result: Result<'value, 'error>) : bool =
        match result with
        | Ok _ -> false
        | Error _ -> true

    let okValue (result: Result<'value, 'error>) : 'value =
        match result with
        | Ok value -> value
        | Error _ -> failwith "expected an ok result"

    /// Runs the search to completion on a problem the caller supplies. The reopen rule
    /// (M-07) is exercised through this one entry point.
    let solve (problem: AStarProblem) : Result<SearchSnapshot, AStarFault> =
        let stepLimit = 10000

        AStar.initialize problem
        |> Result.bind (fun start ->
            let rec run (snapshot: SearchSnapshot) (steps: int) : Result<SearchSnapshot, AStarFault> =
                match AStar.isComplete snapshot, AStar.isUnreachable snapshot, steps >= stepLimit with
                | true, _, _
                | _, true, _ -> Ok snapshot
                | false, false, false -> AStar.step problem snapshot |> Result.bind (fun next -> run next (steps + 1))
                | false, false, true -> Ok snapshot

            run start 0)

    let costOf (snapshot: SearchSnapshot) : float =
        snapshot.Incumbent |> Option.defaultValue Double.NaN

    let pathOf (problem: AStarProblem) (snapshot: SearchSnapshot) : string list option = AStar.pathOf problem snapshot

    let boundOf (snapshot: SearchSnapshot) : float option = AStar.globalBound snapshot
