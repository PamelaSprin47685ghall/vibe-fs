
import { class_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { Operators_IsNull } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { max } from "../fable_modules/fable-library-js.5.6.0/Double.js";
import { substring } from "../fable_modules/fable-library-js.5.6.0/String.js";

export class JsTcs$1 {
    constructor() {
        this.completed = false;
        this.resolveFn = undefined;
        this.p = (new Promise((res, _arg) => {
            this.resolveFn = ((arg) => {
                res(arg);
            });
        }));
    }
}

export function JsTcs$1_$reflection(gen0) {
    return class_type("Wanxiangshu.Next.Process.JsTcs`1", [gen0], JsTcs$1);
}

export function JsTcs$1_$ctor() {
    return new JsTcs$1();
}

export function JsTcs$1__get_Task(_) {
    return _.p;
}

export function JsTcs$1__get_IsCompleted(_) {
    return _.completed;
}

export function JsTcs$1__SetResult_2B595(_, res) {
    _.completed = true;
    const matchValue = _.resolveFn;
    if (matchValue == null) {
    }
    else {
        matchValue(res);
    }
}

export function JsTcs$1__TrySetResult_2B595(_, res) {
    if (_.completed) {
        return false;
    }
    else {
        _.completed = true;
        const matchValue = _.resolveFn;
        if (matchValue == null) {
            return false;
        }
        else {
            matchValue(res);
            return true;
        }
    }
}

export function ProcessPump_pumpStream(stream, cancellation, maxChars) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => {
        const tcs = JsTcs$1_$ctor();
        let text = "";
        let truncated = false;
        return Operators_IsNull(stream) ? builder$0040.Return(["", false]) : builder$0040.Using(cancellation.register(() => {
            JsTcs$1__TrySetResult_2B595(tcs, [text, truncated]);
        }), (_arg_1) => {
            stream.on("data", ((chunk) => {
                const s = chunk.toString("utf-8");
                if ((text.length + s.length) > maxChars) {
                    const allowed = max(0, maxChars - text.length) | 0;
                    if (allowed > 0) {
                        text = (text + substring(s, 0, allowed));
                    }
                    truncated = true;
                }
                else {
                    text = (text + s);
                }
            }));
            stream.on("end", (() => {
                JsTcs$1__TrySetResult_2B595(tcs, [text, truncated]);
            }));
            stream.on("error", ((_arg) => {
                JsTcs$1__TrySetResult_2B595(tcs, [text, truncated]);
            }));
            return builder$0040.ReturnFrom(JsTcs$1__get_Task(tcs));
        });
    }));
}

