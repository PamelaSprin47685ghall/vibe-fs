module Wanxiangshu.Shell.MessagingEncodeHelpers

open Fable.Core
open Wanxiangshu.Shell.Dyn

let replacePartsOnRawMessage (rawMsg: obj) (encodedParts: obj array) : obj =
    Dyn.withKey rawMsg "parts" (box encodedParts)