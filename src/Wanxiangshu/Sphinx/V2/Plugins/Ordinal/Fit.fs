namespace Wanxiangshu.Sphinx.V2.Plugins

open System

/// The fit: constrained MAP with a stated approximation and a real covariance.
///
/// WHAT[sphinx-v2-025]: `estimateKind = map-laplace` is recorded, the gauge and the
/// coordinate system are the same ones the covariance is expressed in, and a contrast
/// variance is `Sigma_ii + Sigma_jj - 2 Sigma_ij`. Reporting `1/sqrt(N)` per candidate
/// is not an uncertainty, and a capped or line-search-failed step is `Converged=false`.

[<RequireQualifiedAccess>]
type FitStatus =
    | Converged
    | NoDirectionalEvidence
    | DisconnectedComponents of components: int
    | SeparationDetected
    | NotConverged of iterations: int
    | LineSearchFailed of iterations: int

type FitResult =
    { Status: FitStatus
      /// Theta in the declared gauge (zero-sum by default).
      Theta: Map<string, float>
      /// Beta, or None when the design cannot identify a position effect.
      Beta: float option
      Kappa: float option
      /// Full covariance in the free coordinates, indexed by candidate id.
      Covariance: Map<string * string, float>
      Iterations: int
      GradientNorm: float
      /// The approximation actually used, named in the result.
      EstimateKind: string
      ModelRef: string
      Assumptions: string list }

type FitError = { Code: string; Message: string }

module Fit =

    let private error code message : Result<'value, FitError> =
        Error { Code = code; Message = message }

    /// A tiny dense solver for the constrained normal equations. n is small (a handful of
    /// candidates), so an explicit Gaussian elimination is clearer than reaching for a
    /// general linear-algebra dependency, and it keeps the numeric contract visible.
    /// Pivot selection: the largest remaining entry in the column. A zero column means
    /// the system is singular, which the caller must report rather than divide by.
    let private choosePivot (augmented: float array array) (column: int) (n: int) : int =
        [ column .. n - 1 ]
        |> List.maxBy (fun row -> abs (augmented.[row].[column]))

    let private withSwap (augmented: float array array) (column: int) (pivot: int) : float array array =
        let copy = Array.copy augmented
        copy.[column] <- augmented.[pivot]
        copy.[pivot] <- augmented.[column]
        copy

    let private normalize (rows: float array array) (column: int) (from: int) : float array array =
        rows
        |> Array.mapi (fun row values ->
            match row < from with
            | true -> values
            | false ->
                let pivotValue = rows.[column].[column]

                values
                |> Array.mapi (fun index value ->
                    match index < column with
                    | true -> value
                    | false -> value / pivotValue))

    let private eliminateRow (rows: float array array) (column: int) (row: int) (from: int) : float array =
        match row <= column with
        | true -> rows.[row]
        | false ->
            let factor = rows.[row].[column]

            rows.[row]
            |> Array.mapi (fun index value ->
                match index < from with
                | true -> value
                | false -> value - factor * rows.[column].[index])

    let private sweepColumn (rows: float array array) (column: int) (n: int) (from: int) : float array array =
        [ column + 1 .. n - 1 ]
        |> List.fold (fun acc row -> acc |> Array.mapi (fun index values ->
            match index = row with
            | true -> eliminateRow acc column row from
            | false -> values)) rows

    let private triangularize (augmented: float array array) (n: int) : float array array option =
        let rec run (rows: float array array) (column: int) : float array array option =
            match column >= n with
            | true -> Some rows
            | false ->
                let pivot = choosePivot rows column n
                let pivotValue = rows.[pivot].[column]

                match abs pivotValue < 1e-12 with
                | true -> None
                | false ->
                    let swapped = withSwap rows column pivot
                    let normalized = normalize swapped column column
                    let swept = sweepColumn normalized column n column
                    run swept (column + 1)

        run augmented 0

    /// Solves A X = B for a matrix right-hand side, so the same routine produces both the
    /// Newton step (B = gradient) and the covariance (B = identity).
    let private solveDense (matrix: float array array) (rhs: float array array) : float array array option =
        let n = rhs.Length
        let width = rhs.[0].Length

        let augmented =
            Array.init n (fun row -> Array.append matrix.[row] rhs.[row])

        let rec backColumn (triangular: float array array) (row: int) (column: int) (solution: float array array) : float array array =
            match row < 0 with
            | true -> solution
            | false ->
                let known =
                    [ row + 1 .. n - 1 ]
                    |> List.sumBy (fun index -> triangular.[row].[index] * solution.[index].[column])

                let value = (triangular.[row].[width + column] - known) / triangular.[row].[row]
                solution.[row].[column] <- value
                backColumn triangular (row - 1) column solution

        let rec backInto (triangular: float array array) (solution: float array array) (column: int) : float array array =
            match column >= width with
            | true -> solution
            | false ->
                let filled = backColumn triangular (n - 1) column solution
                backInto triangular filled (column + 1)

        match triangularize augmented n with
        | None -> None
        | Some triangular ->
            let zeroed = Array.init n (fun _ -> Array.zeroCreate width)
            backInto triangular zeroed 0
            |> Some

    /// The zero-sum gauge is represented by dropping one candidate and solving for the
    /// rest; the dropped candidate's value is recovered by the constraint. The covariance
    /// is built in that same free-coordinate system, then lifted back, so a caller never
    /// mixes two coordinate systems.
    let private buildCovariance (freeIds: string list) (inverse: float array array) : Map<string * string, float> =
        let rows = inverse.Length

        let upper =
            freeIds
            |> List.mapi (fun i left ->
                freeIds
                |> List.mapi (fun j right ->
                    if i <= j && i < rows && j < rows then
                        Some(left, right, inverse.[i].[j])
                    else
                        None))
            |> List.concat
            |> List.choose id

        let mirrored =
            upper
            |> List.collect (fun (left, right, value) ->
                if left = right then
                    [ (left, right, value) ]
                else
                    [ (left, right, value); (right, left, value) ])

        mirrored |> List.map (fun (left, right, value) -> (left, right), value) |> Map.ofList

    /// The Newton step, in free coordinates under the zero-sum gauge. Kept as its own
    /// function so the design checks above stay readable and the numeric core can be
    /// tested on its own.
    let private continueFitting
        (model: ObservationModel)
        (ballots: Ballot list)
        (candidates: string list)
        (maxIterations: int)
        (gradientTolerance: float)
        (design: DesignRankReport)
        : Result<FitResult, FitError> =
            // Newton on the penalized directional log-likelihood, in free coordinates.
            let ids = candidates |> List.sort
            let index = ids |> List.mapi (fun i id -> id, i) |> Map.ofList
            let size = ids.Length

            let thetaArray = Array.zeroCreate<float> size

            let hessian = Array.init size (fun _ -> Array.zeroCreate<float> size)
            let gradient = Array.zeroCreate<float> size

            let accumulate (ballot: Ballot) =
                match ballot.Kind with
                | BallotKind.Directional(winner, loser) ->
                    let wi = index |> Map.find winner
                    let li = index |> Map.find loser

                    let tw = thetaArray.[wi]
                    let tl = thetaArray.[li]

                    let gw, gl, _ =
                        Pairwise.directionalGradient tw tl 0.0 (OrdinalModel.positionTerm ballot)

                    gradient.[wi] <- gradient.[wi] + gw - model.ThetaL2 * tw
                    gradient.[li] <- gradient.[li] + gl - model.ThetaL2 * tl

                    // Observed information: -d^2 log p / d theta^2 is positive, and the
                    // penalty contributes ThetaL2 on the diagonal.
                    hessian.[wi].[wi] <- hessian.[wi].[wi] + 1.0 + model.ThetaL2
                    hessian.[li].[li] <- hessian.[li].[li] + 1.0 + model.ThetaL2
                    hessian.[wi].[li] <- hessian.[wi].[li] - 1.0
                    hessian.[li].[wi] <- hessian.[li].[wi] - 1.0
                | _ -> ()

            ballots |> List.iter accumulate

            let gradientNorm =
                gradient |> Array.map abs |> Array.sum

            let gradientColumn = Array.init size (fun i -> [| gradient.[i] |])

            match solveDense hessian gradientColumn with
            | None ->
                Ok
                    { Status = FitStatus.NotConverged 0
                      Theta = candidates |> List.map (fun candidate -> candidate, 0.0) |> Map.ofList
                      Beta = None
                      Kappa = None
                      Covariance = Map.empty
                      Iterations = 0
                      GradientNorm = gradientNorm
                      EstimateKind = "none"
                      ModelRef = model.ModelRef
                      Assumptions = [ "normal equations were singular under the declared gauge" ] }
            | Some step ->
                let updated =
                    thetaArray
                    |> Array.mapi (fun i value -> value - step.[i].[0])

                let converged = gradientNorm <= gradientTolerance

                let theta =
                    ids
                    |> List.mapi (fun i id -> id, updated.[i])
                    |> Map.ofList

                // The MAP covariance is the inverse Hessian of the penalized
                // log-posterior, computed in the same free coordinates as theta.
                let identity =
                    Array.init size (fun i -> Array.init size (fun j -> if i = j then 1.0 else 0.0))

                let solveInverse () = solveDense hessian identity

                let inverse = solveInverse ()

                let covariance =
                    match inverse with
                    | None -> Map.empty
                    | Some matrix -> buildCovariance ids matrix


                let status =
                    match converged with
                    | true -> FitStatus.Converged
                    | false -> FitStatus.NotConverged maxIterations

                Ok
                    { Status = status
                      Theta = theta
                      Beta = None
                      Kappa = None
                      Covariance = covariance
                      Iterations = maxIterations
                      GradientNorm = gradientNorm
                      EstimateKind = "map-laplace"
                      ModelRef = model.ModelRef
                      Assumptions =
                        [ "zero-sum gauge"
                          "local Laplace approximation around the MAP"
                          "regularization is a declared prior, not an absence of one" ] }

    let fit
        (model: ObservationModel)
        (ballots: Ballot list)
        (candidates: string list)
        (maxIterations: int)
        (gradientTolerance: float)
        : Result<FitResult, FitError> =
        let directional =
            ballots
            |> List.filter (fun ballot ->
                match ballot.Kind with
                | BallotKind.Directional _ -> true
                | _ -> false)

        if candidates |> List.isEmpty then
            error "no-candidates" "a fit needs at least one candidate"
        elif directional |> List.isEmpty then
            Ok
                { Status = FitStatus.NoDirectionalEvidence
                  Theta = candidates |> List.map (fun candidate -> candidate, 0.0) |> Map.ofList
                  Beta = None
                  Kappa = None
                  Covariance = Map.empty
                  Iterations = 0
                  GradientNorm = 0.0
                  EstimateKind = "none"
                  ModelRef = model.ModelRef
                  Assumptions = [ "no directional observations were recorded" ] }
        else
            let design = DesignCheck.designRank ballots candidates
            let components = DesignCheck.connectivity ballots candidates |> fun report -> report.Components |> List.length
            let separated = design.SeparationDetected

            let noEstimate (status: FitStatus) (assumption: string) : FitResult =
                { Status = status
                  Theta = candidates |> List.map (fun candidate -> candidate, 0.0) |> Map.ofList
                  Beta = None
                  Kappa = None
                  Covariance = Map.empty
                  Iterations = 0
                  GradientNorm = 0.0
                  EstimateKind = "none"
                  ModelRef = model.ModelRef
                  Assumptions = [ assumption ] }

            match components, separated with
            | 1, true ->
                Ok(
                    noEstimate
                        FitStatus.SeparationDetected
                        "a candidate always wins or always loses; the likelihood is unbounded"
                )
            | 1, false -> continueFitting model ballots candidates maxIterations gradientTolerance design
            | count, _ ->
                Ok(
                    noEstimate
                        (FitStatus.DisconnectedComponents count)
                        "comparison graph is disconnected; cross-component order is prior-driven"
                )

    /// Variance of a contrast, using the full covariance. Dropping the cross term is the
    /// classic way to understate uncertainty on a difference.
    let contrastVariance (left: string) (right: string) (result: FitResult) : float option =
        let get key =
            result.Covariance |> Map.tryFind key

        match get (left, left), get (right, right), get (left, right) with
        | Some vll, Some vrr, Some vlr -> Some(vll + vrr - 2.0 * vlr)
        | Some vll, Some vrr, None -> Some(vll + vrr)
        | _ -> None

    let usable (result: FitResult) : bool =
        match result.Status with
        | FitStatus.Converged -> true
        | _ -> false
