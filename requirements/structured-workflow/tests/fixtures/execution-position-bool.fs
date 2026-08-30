module ChildSurface

type Child = { OpaqueChoice: bool }

let resume child =
    if child.OpaqueChoice then validate ()
    else dispatch ()
