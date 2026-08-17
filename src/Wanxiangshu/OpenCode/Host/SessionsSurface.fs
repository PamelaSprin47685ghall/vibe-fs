namespace Wanxiangshu.OpenCode

open Fable.Core.JsInterop

/// JS-native observation surface for HOST-015 family-root flattening.
/// Parent lineage is a durable lookup; the physical parent for every child is
/// the resolved family root, never the immediate logical parent.
module SessionsSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private parentsOf (value: obj) : obj array =
        if isNull value then [||] else unbox<obj array> value

    let familyRoot (parents: obj) (session: string) : string =
        let rec resolve current visited =
            if Set.contains current visited then
                current
            else
                parentsOf parents
                |> Array.tryPick (fun pair ->
                    if text pair?child = current then
                        Some(text pair?parent)
                    else
                        None)
                |> Option.map (fun parent -> resolve parent (Set.add current visited))
                |> Option.defaultValue current

        resolve session Set.empty

    let physicalParents (parents: obj) (children: obj) : string array =
        if isNull children then
            [||]
        else
            (unbox<obj array> children)
            |> Array.map (fun child -> familyRoot parents (text child))
