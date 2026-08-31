namespace Foreign

let verify (current: CurrentWitness) (_: CurrentWitness) = Ok current

let dispatchAfterWrongAdmission (effectPort: EffectPort)
                                (current: CurrentWitness)
                                (stale: CurrentWitness)
                                (other: CurrentWitness) =
    verify current (ignore stale; other)
    |> Result.map (fun _ -> effectPort.SendMessage stale)
