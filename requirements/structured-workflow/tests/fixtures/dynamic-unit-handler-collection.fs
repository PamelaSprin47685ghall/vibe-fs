module MutableUnitHandlers

type Input = Input of string

let handlers = ResizeArray<Input -> unit>()

let register handler =
    handlers.Add handler
