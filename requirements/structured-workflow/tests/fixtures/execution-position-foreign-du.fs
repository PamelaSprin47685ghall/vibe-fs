module ChildSurface

open ForeignProtocol

type Child = { OpaqueChoice: RemoteInstruction }

let resume child =
    match child.OpaqueChoice with
    | Validate -> validate ()
    | Dispatch -> dispatch ()
