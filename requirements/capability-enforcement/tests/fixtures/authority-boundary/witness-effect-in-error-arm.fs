namespace Foreign

let dispatch (current: CurrentWitness) (stale: CurrentWitness) =
    match verify current stale with
    | Ok permit -> ()
    | Error _ -> RegisteredEffect.send stale
