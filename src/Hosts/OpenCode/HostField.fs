module Wanxiangshu.Hosts.Opencode.HostField

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn

let formatDomainError (context: string) (error: DomainError) : string =
    $"{context} failed: {formatDomainError error}"

/// Helpers for reading optional fields off host objects.
let optStr (a: obj) (k: string) =
    let v = Dyn.get a k in if Dyn.isNullish v then None else Some(string v)

let optInt (a: obj) (k: string) =
    let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<int> v)

let optBool (a: obj) (k: string) =
    let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<bool> v)
