namespace Wanxiangshu.Next.OpenCode

open Fable.Core.JsInterop

module HostEventDirectory =
    let rawDirectory (raw: obj) =
        let event = if isNull raw || isNull raw?event then raw else raw?event
        let properties = if isNull event then null else event?properties

        if not (isNull raw) && not (isNull raw?directory) then
            Some(unbox<string> raw?directory)
        elif not (isNull event) && not (isNull event?directory) then
            Some(unbox<string> event?directory)
        elif not (isNull properties) && not (isNull properties?directory) then
            Some(unbox<string> properties?directory)
        else
            None
