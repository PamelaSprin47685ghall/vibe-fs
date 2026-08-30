module ChildSurface

type Cursor =
    { Address: int }

let resume cursor =
    match cursor.Address with
    | 0 -> validate ()
    | _ -> dispatch ()
