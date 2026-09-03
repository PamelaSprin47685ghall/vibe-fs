namespace Wanxiangshu.Foundation

open Fable.Core

module CanonicalJson =

    [<Emit("""
    (function (value) {
        function compareCodePoints(left, right) {
            const leftPoints = Array.from(left, character => character.codePointAt(0));
            const rightPoints = Array.from(right, character => character.codePointAt(0));
            const length = Math.min(leftPoints.length, rightPoints.length);
            for (let index = 0; index < length; index += 1) {
                if (leftPoints[index] !== rightPoints[index]) {
                    return leftPoints[index] - rightPoints[index];
                }
            }
            return leftPoints.length - rightPoints.length;
        }

        function encode(current, arrayElement) {
            if (Array.isArray(current)) {
                return '[' + Array.from(current, item => encode(item, true) ?? 'null').join(',') + ']';
            }

            if (current !== null && typeof current === 'object') {
                const fields = [];
                for (const key of Object.keys(current).sort(compareCodePoints)) {
                    const encoded = encode(current[key], false);
                    if (encoded !== undefined) {
                        fields.push(JSON.stringify(key) + ':' + encoded);
                    }
                }
                return '{' + fields.join(',') + '}';
            }

            const encoded = JSON.stringify(current);
            return encoded === undefined && arrayElement ? 'null' : encoded;
        }

        return encode(value, false);
    })($0)
    """)>]
    let private encode (value: obj) : string = jsNative

    let canonicalJson (value: obj) : string = encode value

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
