namespace Foreign

let dispatchAfterAdmission subject version digest (witness: CurrentWitness) =
    if subject <> "" && version > 0L && digest <> "" then Task.send witness
