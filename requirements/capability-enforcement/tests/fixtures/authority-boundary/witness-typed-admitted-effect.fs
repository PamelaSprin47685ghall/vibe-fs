namespace Foreign

let dispatchAfterTypedAdmission subject version digest (witness: CurrentWitness) =
    CurrentAdmission.admit subject version digest witness
    |> Result.map (fun admitted -> Task.send admitted)
