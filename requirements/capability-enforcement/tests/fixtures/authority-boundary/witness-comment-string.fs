namespace Foreign

/// CurrentWitness must never call Task.send directly.
(* A diagnostic note mentioning CurrentWitness and dispatch is not code. *)
let diagnostic = "CurrentWitness Task.send append execute"
