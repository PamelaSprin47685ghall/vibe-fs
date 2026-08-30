module Reviewer

type Cursor =
    { Address: int }

let private runA () = "a"
let private runB () = "b"

let select cursor =
    match cursor.Address with
    | 0 -> runA
    | _ -> runB
