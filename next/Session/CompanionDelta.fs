namespace Wanxiangshu.Next.Session

open Fable.Core.JsInterop

type ProjectionSnapshot = string

module CompanionDelta =
    let private deterministicPatch (previous: obj) (current: obj) : string =
        emitJsExpr
            (previous, current)
            """
            (function (previous, current) {
                function canonical(value) {
                    if (Array.isArray(value)) return value.map(canonical);
                    if (value !== null && typeof value === "object") {
                        const result = {};
                        Object.keys(value).sort().forEach(function (key) {
                            result[key] = canonical(value[key]);
                        });
                        return result;
                    }
                    return value;
                }

                function isObject(value) {
                    return value !== null && typeof value === "object" && !Array.isArray(value);
                }

                function pointer(base, key) {
                    return base + "/" + String(key).replace(/~/g, "~0").replace(/\//g, "~1");
                }

                const operations = [];

                function walk(oldValue, newValue, path, oldPresent, newPresent) {
                    if (!oldPresent && !newPresent) return;
                    if (!oldPresent) {
                        operations.push({ op: "add", path: path, value: canonical(newValue) });
                        return;
                    }
                    if (!newPresent) {
                        operations.push({ op: "remove", path: path });
                        return;
                    }
                    if (JSON.stringify(canonical(oldValue)) === JSON.stringify(canonical(newValue))) return;

                    if (Array.isArray(oldValue) && Array.isArray(newValue)) {
                        const common = Math.min(oldValue.length, newValue.length);
                        for (let index = 0; index < common; index += 1) {
                            walk(oldValue[index], newValue[index], pointer(path, index), true, true);
                        }
                        for (let index = oldValue.length - 1; index >= newValue.length; index -= 1) {
                            operations.push({ op: "remove", path: pointer(path, index) });
                        }
                        for (let index = common; index < newValue.length; index += 1) {
                            operations.push({
                                op: "add",
                                path: pointer(path, index),
                                value: canonical(newValue[index])
                            });
                        }
                        return;
                    }

                    if (isObject(oldValue) && isObject(newValue)) {
                        const keys = Array.from(
                            new Set(Object.keys(oldValue).concat(Object.keys(newValue)))
                        ).sort();
                        for (const key of keys) {
                            walk(
                                oldValue[key],
                                newValue[key],
                                pointer(path, key),
                                Object.prototype.hasOwnProperty.call(oldValue, key),
                                Object.prototype.hasOwnProperty.call(newValue, key)
                            );
                        }
                        return;
                    }

                    operations.push({ op: "replace", path: path, value: canonical(newValue) });
                }

                walk(previous, current, "", true, true);
                return JSON.stringify({ ops: operations });
            })($0, $1)
            """

    let jsonDelta (previous: ProjectionSnapshot option) (current: ProjectionSnapshot) : ProjectionSnapshot option =
        match previous with
        | None -> Some current
        | Some prev ->
            try
                let patch =
                    deterministicPatch (Fable.Core.JS.JSON.parse prev) (Fable.Core.JS.JSON.parse current)

                if patch = "{\"ops\":[]}" then None else Some patch
            with _ ->
                Some current
