module VibeFs.Mux.TodoWriteNudge

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Prompts

/// Append the reverie nudge to a todo_write result, whether it's a plain string
/// or a `{ success, nudge }` object.  Pure over the dynamic result shape.
let appendReverieNudge (result: obj) : obj =
    if Dyn.isNullish result then result
    elif Dyn.typeIs result "string" then
        let s = string result
        if s.Contains(reverieNudge) then result else box $"{s}\n\n{reverieNudge}"
    elif Dyn.typeIs result "object" then
        let success = Dyn.get result "success"
        if not (Dyn.isNullish success) && unbox<bool> success then
            let existingNudge = Dyn.get result "nudge"
            if not (Dyn.isNullish existingNudge) && (string existingNudge).Contains(reverieNudge) then result
            else Dyn.withKey result "nudge" (box reverieNudge)
        else result
    else result
