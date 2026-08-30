module ReviewerUnitCallback

type Input = Input of string

let twice (handler: Input -> unit) input =
    handler input
    handler input
