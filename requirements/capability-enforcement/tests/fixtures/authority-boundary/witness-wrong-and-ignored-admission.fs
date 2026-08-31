namespace Foreign

let dispatchOther currentSubject currentVersion currentDigest
                  (stale: CurrentWitness) (other: CurrentWitness) =
    let _ = CurrentAdmission.admit currentSubject currentVersion currentDigest other
    Task.send stale

let dispatchIgnoringFailure currentSubject currentVersion currentDigest (witness: CurrentWitness) =
    CurrentAdmission.admit currentSubject currentVersion currentDigest witness |> ignore
    Task.send witness
