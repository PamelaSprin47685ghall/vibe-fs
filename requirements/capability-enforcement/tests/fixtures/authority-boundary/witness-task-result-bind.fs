namespace Foreign

let dispatch (current: CurrentWitness) (stale: CurrentWitness) =
    taskResult {
        let! permit = verifyAsync current stale
        RegisteredEffect.send stale
    }
