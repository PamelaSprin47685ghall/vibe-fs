module Foreign

open Fixture

let describe value = string value

type Runtime() =

    member _.Commit(payload: string) = Task.send payload

    member _.Inspect(witness: CurrentWitness) = witness
