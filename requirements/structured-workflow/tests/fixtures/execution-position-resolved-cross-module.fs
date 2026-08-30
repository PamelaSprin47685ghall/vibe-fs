module Reviewer

type Cursor =
    { Address: int }

let choose cursor =
    match cursor.Address with
    | 0 -> Foreign.runA
    | _ -> Foreign.runB

let send (port: Foreign.Port) cursor =
    match cursor.Address with
    | 0 -> port.Send "a"
    | _ -> port.Send "b"
