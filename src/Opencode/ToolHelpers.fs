module VibeFs.Opencode.ToolHelpers

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Shell
open VibeFs.Shell.Dyn

let formatDomainError (context: string) (error: DomainError) : string =
    $"{context} failed: {Domain.formatDomainError error}"

/// Helpers for reading optional fields off host objects.
let optStr (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(string v)

let optInt (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<int> v)

let optBool (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<bool> v)
