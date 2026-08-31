module Reviewer

type Cursor =
    { Address: int }

type Port =
    abstract Send: string -> unit

let select (port: Port) cursor =
    match cursor.Address with
    | 0 -> port.Send "a"
    | _ -> port.Send "b"
