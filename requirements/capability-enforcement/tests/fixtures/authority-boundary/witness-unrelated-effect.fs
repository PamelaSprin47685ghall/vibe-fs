namespace Foreign

let keepWitness (witness: CurrentWitness) = witness

let sendHeartbeat () =
    Task.send "heartbeat"
