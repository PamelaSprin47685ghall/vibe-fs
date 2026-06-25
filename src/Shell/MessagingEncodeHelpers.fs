module VibeFs.Shell.MessagingEncodeHelpers

open Fable.Core
open VibeFs.Shell.Dyn

let replacePartsOnRawMessage (rawMsg: obj) (encodedParts: obj array) : obj =
    Dyn.withKey rawMsg "parts" (box encodedParts)