module SynchronousCallbackCalls

type Request = Request of string
type Response = Response of string

let invokeTwice (operation: Request -> Response) request =
    let first = operation request
    operation request

let forwardOnce (operation: Request -> Response) request =
    operation request

let passForwardOnce register (operation: Request -> Response) =
    register operation
