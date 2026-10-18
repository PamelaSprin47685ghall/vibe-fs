namespace Wanxiangshu.Host

open Fable.Core
open Fable.Core.JsInterop

/// Canonical JSON identity protocol (§5.0). The single implementation lives in
/// wanxiangli/events (CUT-18); this module is the typed F# projection of that
/// contract and owns no encoding algorithm of its own.
module CanonicalJson =

    [<Import("CanonicalEventCodec", "wanxiangli/events")>]
    let private codecClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : obj = jsNative

    // Stateless library object; one shared instance.
    let private codec: obj = construct codecClass

    let canonicalJson (value: obj) : string =
        unbox<string> (codec?canonicalJson value)

    let equal (left: obj) (right: obj) : bool =
        canonicalJson left = canonicalJson right

    [<Emit("""
    (function (keys, value) {
        if (value === null || typeof value !== "object" || Array.isArray(value)) {
            return value;
        }

        const result = {};
        Object.keys(value).forEach(function (key) {
            if (keys.indexOf(key) < 0) {
                result[key] = value[key];
            }
        });
        return result;
    })($0, $1)
    """)>]
    let private removeKeys (keys: string array) (value: obj) : obj = jsNative

    let withoutKeys (keys: string array) (value: obj) : obj = removeKeys keys value
