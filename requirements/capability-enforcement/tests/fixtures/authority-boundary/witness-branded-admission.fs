namespace Foreign

let dispatchAfterBrandedAdmission
    (managerSessionId: string)
    (currentBarrierId: string)
    (currentGitTreeHash: string)
    (witness: CurrentWitness)
    =
    if managerSessionId <> "" && currentBarrierId <> "" && currentGitTreeHash <> "" then
        Task.send witness
