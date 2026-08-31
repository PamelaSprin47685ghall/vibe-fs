namespace Foreign

let dispatchFromWitness (witness: CurrentWitness) =
    match Some witness with
    | Some current -> Task.send current
    | None -> Task.send witness
