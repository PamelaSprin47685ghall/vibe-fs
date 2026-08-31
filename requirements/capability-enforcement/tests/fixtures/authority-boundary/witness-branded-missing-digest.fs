namespace Foreign

let dispatchWithoutDigest
    (managerSessionId: string)
    (currentBarrierId: string)
    (witness: CurrentWitness)
    =
    if managerSessionId <> "" && currentBarrierId <> "" then
        Task.send witness
