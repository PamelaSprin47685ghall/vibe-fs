module Wanxiangshu.Shell.OpencodeContextCodec

open Wanxiangshu.Shell.Dyn

let getAbortSignalFromContext (context: obj) : obj =
    if Dyn.isNullish context then null
    else
        let abort = Dyn.get context "abort"
        if Dyn.isNullish abort then null else abort