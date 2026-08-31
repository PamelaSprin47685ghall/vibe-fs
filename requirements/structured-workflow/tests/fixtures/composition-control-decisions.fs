module PluginBoot

let selectRecovery x =
    if x then recoverA () else recoverB ()

let create x =
    let choose value = fun () -> value
    if x = 0 then choose x
    elif x = 1 then choose x
    else
        match x with
        | value -> try wire value with _ -> fallback value
