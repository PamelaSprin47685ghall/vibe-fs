module DistinctCallbackCalls

let runDistinct runner commandA commandB =
    task {
        do! runner commandA
        do! runner commandB
    }

let runTypedDistinct (invoke: Command -> Task<Result>) commandA commandB =
    task {
        do! invoke commandA
        do! invoke commandB
    }

let forwardCallback register callback scope =
    register callback scope
