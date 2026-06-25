module VibeFs.Shell.DynField

open VibeFs.Shell.Dyn

let hasField (a: obj) (k: string) : bool = Dyn.has a k

let strField (a: obj) (k: string) : string option =
    let v = Dyn.get a k
    if Dyn.isNullish v then None else Some(string v)

let optInt (a: obj) (k: string) : int option =
    let v = Dyn.get a k
    if Dyn.isNullish v then None else Some(unbox<int> v)

let optBool (a: obj) (k: string) : bool option =
    let v = Dyn.get a k
    if Dyn.isNullish v then None else Some(unbox<bool> v)

let requiredStrField (a: obj) (k: string) : string =
    match strField a k with
    | Some s -> s
    | None -> ""

let strListField (a: obj) (k: string) : string list option =
    let v = Dyn.get a k
    if Dyn.isNullish v || not (Dyn.isArray v) then None
    else
        unbox<string array> v
        |> Array.toList
        |> Some