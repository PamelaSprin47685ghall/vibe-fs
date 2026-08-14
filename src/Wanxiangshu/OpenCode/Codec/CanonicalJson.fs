namespace Wanxiangshu.OpenCode

open Fable.Core
open Fable.Core.JsInterop

/// Stable JSON primitives shared by OpenCode projections.  JSON.stringify
/// preserves insertion order, so raw object construction order must not leak
/// into a durable projection or prefix comparison.
module CanonicalJson =

    let private normalizeJson (value: obj) : obj =
        emitJsExpr
            value
            """
            (function (value) {
                function normalize(current) {
                    if (Array.isArray(current)) {
                        return current.map(normalize);
                    }

                    if (current !== null && typeof current === "object") {
                        const result = {};
                        Object.keys(current).sort().forEach(function (key) {
                            result[key] = normalize(current[key]);
                        });
                        return result;
                    }

                    return current;
                }

                return normalize(value);
            })($0)
            """

    let canonicalJson (value: obj) : string =
        Fable.Core.JS.JSON.stringify (normalizeJson value)

    /// Are two values the same once canonicalised.
    ///
    /// One function, because "same message" has one meaning. The Companion prefix
    /// walk used to take a `messageId` reader AND an equality function and check the
    /// ids first — but a message's id is part of its canonical JSON, so equal
    /// canonical text already implies equal ids. That first check could never
    /// change an answer.
    let equal (left: obj) (right: obj) : bool =
        canonicalJson left = canonicalJson right

    let withoutKeys (keys: string array) (value: obj) : obj =
        emitJsExpr
            (value, keys)
            """
            (function (value, keys) {
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
            """
