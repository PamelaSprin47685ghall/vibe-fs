module PluginBoot

open ForeignPolicy

let applyForeign x =
    task {
        do! ForeignPolicy.apply x
        return! ForeignPolicy.finish x
        do! applyPolicy x
    }

let ordinary x = ForeignPolicy.inspect x
let piped x = x |> ForeignPolicy.normalize
let dormant = applyPolicy
// applyPolicy x is commentary, not an application.
