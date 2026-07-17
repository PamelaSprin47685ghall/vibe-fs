module Wanxiangshu.Runtime.WebSearchCodec

open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.SearchPrompts

let parseSearchResults (results: obj) : SearchResult list =
    if Dyn.isNullish results || not (Dyn.isArray results) then
        []
    else
        (results :?> obj array)
        |> Array.map (fun r ->
            { title = Dyn.str r "title"
              url = Dyn.str r "url"
              content = Dyn.str r "content" })
        |> List.ofArray
