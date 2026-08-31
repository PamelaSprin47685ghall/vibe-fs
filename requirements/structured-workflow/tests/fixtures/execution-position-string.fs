module ChildSurface

type Child = { OpaqueChoice: string }

let resume child =
    match child.OpaqueChoice with
    | "validate" -> validate ()
    | _ -> dispatch ()
