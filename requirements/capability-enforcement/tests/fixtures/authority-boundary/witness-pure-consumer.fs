namespace Foreign

let keepWitness (witness: CurrentWitness) =
    match Some witness with
    | Some current -> current
    | None -> witness

let foldWitnesses (witnesses: CurrentWitness list) =
    witnesses |> List.fold (fun count _ -> count + 1) 0
