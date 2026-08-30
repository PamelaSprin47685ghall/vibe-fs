namespace Foreign

let dispatch (current: CurrentWitness) (stale: CurrentWitness) =
    match verify current stale with
    | Ok permit -> RegisteredEffect.send stale
    | Error _ -> ()
