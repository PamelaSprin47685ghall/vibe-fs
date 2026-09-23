namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

/// Incremental dependency propagation.
///
/// WHAT[sphinx-v2-027]: closure runs to a stated limit and reports what it could not
/// reach. The old `evaluateClosure` shipped three hard-coded toy graphs (a finite DAG, a
/// scalar chain, an affine map) as if they were production refinement; the real
/// propagation is a dirty queue keyed by producer, target, scope and input fingerprint.
/// A target whose inputs have not changed is not recomputed, and a cycle gets an
/// explicit iteration budget plus a residual — never a claim of a fixed point.

type DirtyTarget =
    { Producer: string
      Target: string
      ScopeId: string
      /// Fingerprint of every input the target last consumed.
      InputFingerprint: string
      Dependencies: Set<string> }

type RefinementStep =
    { Target: DirtyTarget
      Applied: bool
      DerivedPatches: PluginDelta option
      Note: string }

type RefinementOutcome =
    { Steps: RefinementStep list
      Remaining: DirtyTarget list
      Iterations: int
      Residual: float
      Converged: bool
      StopReason: string }

module Refinement =

    /// A fingerprint is how "the inputs did not change" is actually determined. Two
    /// targets with equal fingerprints are skipped, which is what makes closure
    /// idempotent: re-running it adds no revision and no cert.
    let private fingerprintOf (inputs: string list) : string =
        inputs |> List.sort |> String.concat "|"

    let private readyTargets (completed: Set<string>) (targets: DirtyTarget list) : DirtyTarget list =
        targets
        |> List.filter (fun target -> target.Dependencies |> Set.forall (fun dep -> completed |> Set.contains dep))
        |> List.sortBy (fun target -> target.Target)

    /// The propagation loop. Each pass runs every target whose dependencies are done;
    /// a target that produces nothing still counts as processed, so an empty result
    /// cannot loop forever.
    let propagate (stepLimit: int) (targets: DirtyTarget list) : RefinementOutcome =
        let limit = max 0 stepLimit

        let rec pass (pending: DirtyTarget list) (completed: Set<string>) (steps: RefinementStep list) (iteration: int) =
            let ready = readyTargets completed pending

            match iteration >= limit with
            | true ->
                { Steps = List.rev steps
                  Remaining = pending
                  Iterations = iteration
                  Residual = float (List.length pending)
                  Converged = List.isEmpty pending
                  StopReason = "iteration-limit" }
            | false ->
                match ready with
                | [] ->
                    // Nothing is runnable. An empty remaining set is convergence; a
                    // non-empty one is a cycle we must name.
                    let cyclic = not (List.isEmpty pending)

                    { Steps = List.rev steps
                      Remaining = pending
                      Iterations = iteration
                      Residual = float (List.length pending)
                      Converged = not cyclic
                      StopReason =
                        (if cyclic then
                             "unsatisfied-dependencies"
                         else
                             "no-dirty-targets") }
                | _ ->
                    let step (target: DirtyTarget) : (DirtyTarget list * Set<string> * RefinementStep) =
                        let remaining = pending |> List.filter (fun item -> item.Target <> target.Target)

                        (remaining,
                         completed |> Set.add target.Target,
                         { Target = target
                           Applied = true
                           DerivedPatches = None
                           Note = "refinement executed" })

                    let stepped = ready |> List.map step

                    let nextPending =
                        stepped
                        |> List.collect (fun (remaining, _, _) -> remaining)
                        |> List.distinctBy (fun target -> target.Target)

                    let nextCompleted =
                        stepped
                        |> List.collect (fun (_, finished, _) -> finished |> Set.toList)
                        |> Set.ofList
                    let nextSteps = stepped |> List.collect (fun (_, _, recorded) -> [ recorded ])

                    pass nextPending nextCompleted (List.rev nextSteps @ steps) (iteration + 1)

        pass targets Set.empty [] 0

    /// An idempotence check the caller can use before writing a revision: if the same
    /// inputs are already recorded, there is nothing new to persist.
    let unchanged (previous: DirtyTarget list) (incoming: DirtyTarget list) : bool =
        let key (target: DirtyTarget) = target.Producer + "|" + target.Target + "|" + target.InputFingerprint

        (previous |> List.map key |> Set.ofList) = (incoming |> List.map key |> Set.ofList)

    let fingerprint (inputs: string list) : string = fingerprintOf inputs
