namespace Wanxiangshu.Sphinx.V2.Plugins

/// Design diagnostics that decide whether a fit is even meaningful.
///
/// WHAT[sphinx-v2-025]: a comparison graph with disconnected components yields a
/// numerically finite answer under regularization, but the cross-component ordering
/// comes from the prior, not the data. That must be reported, not smoothed over.

type ConnectivityReport =
    { Components: string list list
      Connected: bool
      /// Candidates that never appear in any ballot.
      Isolated: string list }

type DesignRankReport =
    { /// Number of identifiable contrasts in the free coordinates.
      Rank: int
      ExpectedRank: int
      Sufficient: bool
      /// True when the design can identify a position effect at all.
      PositionIdentifiable: bool
      /// True when one candidate always wins or always loses.
      SeparationDetected: bool }

module DesignCheck =

    /// Connected components over the undirected comparison graph.
    let connectivity (ballots: Ballot list) (candidates: string list) : ConnectivityReport =
        let adjacency =
            ballots
            |> List.collect (fun ballot ->
                match ballot.Kind with
                | BallotKind.Directional(winner, loser) -> [ (winner, loser); (loser, winner) ]
                | BallotKind.Tie(left, right) -> [ (left, right); (right, left) ]
                | _ -> [])
            |> List.groupBy fst
            |> List.map (fun (node, edges) -> node, edges |> List.map snd)
            |> Map.ofList

        let rec walk (visited: Set<string>) (node: string) : Set<string> =
            if visited |> Set.contains node then
                visited
            else
                let seen = visited |> Set.add node
                let neighbours = adjacency |> Map.tryFind node |> Option.defaultValue []

                neighbours |> List.fold (fun acc neighbour -> walk acc neighbour) seen

        let components =
            candidates
            |> List.fold
                (fun (found: string list list) candidate ->
                    if found |> List.exists (List.contains candidate) then
                        found
                    else
                        let reached = walk Set.empty candidate |> Set.toList
                        (reached :: found))
                []
            |> List.map List.sort
            |> List.sort

        let compared =
            ballots
            |> List.collect (fun ballot ->
                match ballot.Kind with
                | BallotKind.Directional(winner, loser) -> [ winner; loser ]
                | BallotKind.Tie(left, right) -> [ left; right ]
                | _ -> [])
            |> Set.ofList

        let isolated = candidates |> List.filter (fun candidate -> not (Set.contains candidate compared))

        { Components = components
          Connected = components |> List.length <= 1
          Isolated = [] }

    /// Design rank: with n candidates and the zero-sum gauge there are n-1 identifiable
    /// contrasts. Fewer means the comparison graph does not tie the candidates together
    /// and the cross-component ordering is a prior artefact.
    let designRank (ballots: Ballot list) (candidates: string list) : DesignRankReport =
        let report = connectivity ballots candidates
        let candidateCount = candidates |> List.length

        let edges =
            ballots
            |> List.collect (fun ballot ->
                match ballot.Kind with
                | BallotKind.Directional(winner, loser) -> [ (winner, loser) ]
                | BallotKind.Tie(left, right) -> [ (left, right) ]
                | _ -> [])

        // rank of the signed incidence matrix: a spanning forest gives n - components.
        let spanningRank = max 0 (candidateCount - (report.Components |> List.length))

        let alwaysWins =
            candidates
            |> List.filter (fun candidate ->
                let wins =
                    ballots
                    |> List.choose (fun ballot ->
                        match ballot.Kind with
                        | BallotKind.Directional(winner, _) when winner = candidate -> Some()
                        | _ -> None)
                    |> List.length

                let losses =
                    ballots
                    |> List.choose (fun ballot ->
                        match ballot.Kind with
                        | BallotKind.Directional(_, loser) when loser = candidate -> Some()
                        | _ -> None)
                    |> List.length

                wins > 0 && losses = 0)

        let alwaysLoses =
            candidates
            |> List.filter (fun candidate ->
                let wins =
                    ballots
                    |> List.choose (fun ballot ->
                        match ballot.Kind with
                        | BallotKind.Directional(winner, _) when winner = candidate -> Some()
                        | _ -> None)
                    |> List.length

                let losses =
                    ballots
                    |> List.choose (fun ballot ->
                        match ballot.Kind with
                        | BallotKind.Directional(_, loser) when loser = candidate -> Some()
                        | _ -> None)
                    |> List.length

                losses > 0 && wins = 0)

        let anyComparisons =
            ballots
            |> List.exists (fun ballot ->
                match ballot.Kind with
                | BallotKind.Directional _
                | BallotKind.Tie _ -> true
                | _ -> false)

        { Rank = spanningRank
          ExpectedRank = max 0 (candidateCount - 1)
          Sufficient = anyComparisons && report.Connected
          PositionIdentifiable =
            ballots
            |> List.exists (fun ballot -> ballot.PositionIdentifiable)
          SeparationDetected = (not (List.isEmpty alwaysWins)) || (not (List.isEmpty alwaysLoses)) }
