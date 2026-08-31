namespace Foreign

let dispatchIgnoringIdentityArgs subject version digest (witness: CurrentWitness) =
    Task.send witness
