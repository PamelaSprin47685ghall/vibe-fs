module Generic

type IWorkflowFilter =
    abstract Invoke: (Request -> Task<Response>) -> Request -> Task<Response>

let handlers = ResizeArray<Request -> Task<Response>>()
let addStage stage = handlers.Add stage
