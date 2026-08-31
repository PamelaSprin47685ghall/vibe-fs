module DynamicHandlerCollection

open System.Threading.Tasks

type Request = Request of string
type Response = Response of string

let handlers = ResizeArray<Request -> Task<Response>>()

let addStage stage =
    handlers.Add stage
