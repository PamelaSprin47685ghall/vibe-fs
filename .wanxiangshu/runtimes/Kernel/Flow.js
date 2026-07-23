
import { Record, Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { record_type, string_type, int64_type, lambda_type, union_type, class_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { Result_IsOk, FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";
import { fromResult } from "../fable_modules/fable-library-js.5.6.0/Task.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { OperationCanceledException } from "../fable_modules/fable-library-js.5.6.0/AsyncBuilder.js";
import { throwIfCancellationRequested } from "../fable_modules/fable-library-js.5.6.0/Async.js";
import { equals } from "../fable_modules/fable-library-js.5.6.0/BigInt.js";
import { Exception, getEnumerator } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { Queue$1__Dequeue, Queue$1__get_Count, Queue$1__Enqueue_2B595, Queue$1_$ctor } from "../fable_modules/fable-library-js.5.6.0/System.Collections.Generic.js";
import { Operators_Lock } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { toArray } from "../fable_modules/fable-library-js.5.6.0/Seq.js";
import { ofArray, empty } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { map } from "../fable_modules/fable-library-js.5.6.0/Array.js";

export class Flow$3 extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["Flow"];
    }
}

export function Flow$3_$reflection(gen0, gen1, gen2) {
    return union_type("Wanxiangshu.Next.Kernel.Flow`3", [gen0, gen1, gen2], Flow$3, () => [[["Item", lambda_type(gen0, lambda_type(class_type("System.Threading.CancellationToken"), class_type("System.Threading.Tasks.Task`1", [union_type("Microsoft.FSharp.Core.FSharpResult`2", [gen2, gen1], FSharpResult$2, () => [[["ResultValue", gen2]], [["ErrorValue", gen1]]])])))]]]);
}

export class ProgressGuard$2 extends Record {
    constructor(Stamp, NoProgress) {
        super();
        this.Stamp = Stamp;
        this.NoProgress = NoProgress;
    }
}

export function ProgressGuard$2_$reflection(gen0, gen1) {
    return record_type("Wanxiangshu.Next.Kernel.ProgressGuard`2", [gen0, gen1], ProgressGuard$2, () => [["Stamp", lambda_type(gen0, int64_type)], ["NoProgress", lambda_type(string_type, gen1)]]);
}

export class FlowBuilder$2 {
    constructor(progress) {
        this.progress = progress;
    }
}

export function FlowBuilder$2_$reflection(gen0, gen1) {
    return class_type("Wanxiangshu.Next.Kernel.FlowBuilder`2", [gen0, gen1], FlowBuilder$2);
}

export function FlowBuilder$2_$ctor_Z35F942C6(progress) {
    return new FlowBuilder$2(progress);
}

export function FlowBuilder$2__Return_1505(_, value) {
    return new Flow$3((_arg, _arg_1) => fromResult(new FSharpResult$2(0, [value])));
}

export function FlowBuilder$2__ReturnFrom_Z3BB19842(_, flow) {
    return flow;
}

export function FlowBuilder$2__Bind_Z40B88B2D(_, _arg, next) {
    return new Flow$3((ctx, ct) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => builder$0040.Bind(_arg.fields[0](ctx, ct), (_arg_1) => {
            const result = _arg_1;
            if (result.tag === 0) {
                const patternInput = next(result.fields[0]);
                return builder$0040.ReturnFrom(patternInput.fields[0](ctx, ct));
            }
            else {
                return builder$0040.Return(new FSharpResult$2(1, [result.fields[0]]));
            }
        })));
    });
}

export function FlowBuilder$2__Zero(_) {
    return new Flow$3((_arg, _arg_1) => fromResult(new FSharpResult$2(0, [undefined])));
}

export function FlowBuilder$2__Delay_Z73C1716C(_, create) {
    return new Flow$3((ctx, ct) => create().fields[0](ctx, ct));
}

export function FlowBuilder$2__Combine_Z1BEA2E47(this$, first, second) {
    return FlowBuilder$2__Bind_Z40B88B2D(this$, first, () => second);
}

export function FlowBuilder$2__TryFinally_74403B28(_, _arg, compensation) {
    return new Flow$3((ctx, ct) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => builder$0040.TryFinally(builder$0040.Delay(() => builder$0040.ReturnFrom(_arg.fields[0](ctx, ct))), () => {
            compensation();
        })));
    });
}

export function FlowBuilder$2__TryWith_Z70687D1(_, _arg, handler) {
    return new Flow$3((ctx, ct) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => builder$0040.TryWith(builder$0040.Delay(() => builder$0040.ReturnFrom(_arg.fields[0](ctx, ct))), (_arg_1) => {
            if (_arg_1 instanceof OperationCanceledException) {
                const ex = _arg_1;
                return builder$0040.Return((() => {
                    throw ex;
                })());
            }
            else {
                const patternInput = handler(_arg_1);
                return builder$0040.ReturnFrom(patternInput.fields[0](ctx, ct));
            }
        })));
    });
}

export function FlowBuilder$2__Using_Z25CD278(_, resource, body) {
    return new Flow$3((ctx, ct) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            let bodyResult = undefined;
            let bodyEx = undefined;
            return builder$0040.Combine(builder$0040.TryWith(builder$0040.Delay(() => {
                const patternInput = body(resource);
                return builder$0040.Bind(patternInput.fields[0](ctx, ct), (_arg) => {
                    bodyResult = _arg;
                    return builder$0040.Zero();
                });
            }), (_arg_1) => {
                bodyEx = _arg_1;
                return builder$0040.Zero();
            }), builder$0040.Delay(() => builder$0040.Combine(builder$0040.TryWith(builder$0040.Delay(() => {
                let copyOfStruct;
                return builder$0040.Bind((((copyOfStruct = resource, copyOfStruct["System.IAsyncDisposable.DisposeAsync"]())) && typeof ((copyOfStruct = resource, copyOfStruct["System.IAsyncDisposable.DisposeAsync"]())).then === 'function') ? ((copyOfStruct = resource, copyOfStruct["System.IAsyncDisposable.DisposeAsync"]())) : Promise.resolve(), () => builder$0040.Zero());
            }), (_arg_3) => {
                return builder$0040.Zero();
            }), builder$0040.Delay(() => {
                if (bodyEx == null) {
                    if (bodyResult == null) {
                        return builder$0040.Return(new FSharpResult$2());
                    }
                    else {
                        const r_1 = bodyResult;
                        return builder$0040.Return(r_1);
                    }
                }
                else {
                    const bEx = bodyEx;
                    return builder$0040.Return((() => {
                        throw bEx;
                    })());
                }
            }))));
        }));
    });
}

export function FlowBuilder$2__While_31AC1067(_, condition, body) {
    return new Flow$3((ctx, ct) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            let result = new FSharpResult$2(0, [undefined]);
            return builder$0040.Combine(builder$0040.While(() => (condition() && Result_IsOk(result)), builder$0040.Delay(() => {
                throwIfCancellationRequested(ct);
                const matchValue = _.progress;
                if (matchValue != null) {
                    const guard = matchValue;
                    const before = guard.Stamp(ctx);
                    return builder$0040.Bind(body.fields[0](ctx, ct), (_arg_1) => {
                        const current_1 = _arg_1;
                        if (current_1.tag === 0) {
                            if (equals(guard.Stamp(ctx), before)) {
                                result = (new FSharpResult$2(1, [guard.NoProgress("Loop body completed without progress")]));
                                return builder$0040.Zero();
                            }
                            else {
                                return builder$0040.Zero();
                            }
                        }
                        else {
                            result = (new FSharpResult$2(1, [current_1.fields[0]]));
                            return builder$0040.Zero();
                        }
                    });
                }
                else {
                    return builder$0040.Bind(body.fields[0](ctx, ct), (_arg) => {
                        result = _arg;
                        return builder$0040.Zero();
                    });
                }
            })), builder$0040.Delay(() => builder$0040.Return(result)));
        }));
    });
}

export function FlowBuilder$2__For_Z203E708(_, items, body) {
    return new Flow$3((ctx, ct) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            let result = new FSharpResult$2(0, [undefined]);
            return builder$0040.Using(getEnumerator(items), (_arg) => {
                const enum$ = _arg;
                return builder$0040.Combine(builder$0040.While(() => (enum$["System.Collections.IEnumerator.MoveNext"]() && Result_IsOk(result)), builder$0040.Delay(() => {
                    throwIfCancellationRequested(ct);
                    const patternInput = body(enum$["System.Collections.Generic.IEnumerator`1.get_Current"]());
                    return builder$0040.Bind(patternInput.fields[0](ctx, ct), (_arg_1) => {
                        result = _arg_1;
                        return builder$0040.Zero();
                    });
                })), builder$0040.Delay(() => builder$0040.Return(result)));
            });
        }));
    });
}

export function Flow_create(f) {
    return new Flow$3(f);
}

export function Flow_run(ctx, ct, _arg) {
    return _arg.fields[0](ctx, ct);
}

export function Flow_fail(error) {
    return new Flow$3((_arg, _arg_1) => fromResult(new FSharpResult$2(1, [error])));
}

export function Flow_attempt(_arg) {
    return new Flow$3((ctx, ct) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => builder$0040.Bind(_arg.fields[0](ctx, ct), (_arg_1) => builder$0040.Return(new FSharpResult$2(0, [_arg_1])))));
    });
}

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
    return class_type("Wanxiangshu.Next.Kernel.JsTcs`1", [gen0], JsTcs$1);
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

export class AsyncSemaphore {
    constructor(maxCount) {
        this.count = (maxCount | 0);
        this.waiters = Queue$1_$ctor();
        this.lockObj = {};
    }
    Dispose() {
    }
}

export function AsyncSemaphore_$reflection() {
    return class_type("Wanxiangshu.Next.Kernel.AsyncSemaphore", undefined, AsyncSemaphore);
}

export function AsyncSemaphore_$ctor_Z524259A4(maxCount) {
    return new AsyncSemaphore(maxCount);
}

export function AsyncSemaphore__WaitAsync_Z211DAE3E(_, ct) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => {
        throwIfCancellationRequested(ct);
        const tcsOpt = Operators_Lock(_.lockObj, () => {
            if (_.count > 0) {
                _.count = ((_.count - 1) | 0);
                return undefined;
            }
            else {
                const tcs = JsTcs$1_$ctor();
                Queue$1__Enqueue_2B595(_.waiters, tcs);
                return tcs;
            }
        });
        if (tcsOpt == null) {
            return builder$0040.Zero();
        }
        else {
            const tcs_1 = tcsOpt;
            return builder$0040.Bind(JsTcs$1__get_Task(tcs_1), () => builder$0040.Zero());
        }
    }));
}

export function AsyncSemaphore__Release(_) {
    Operators_Lock(_.lockObj, () => {
        if (Queue$1__get_Count(_.waiters) > 0) {
            JsTcs$1__TrySetResult_2B595(Queue$1__Dequeue(_.waiters), undefined);
        }
        else {
            _.count = ((_.count + 1) | 0);
        }
    });
}

export function Parallel_mapBounded(maxConcurrency, cancellation, action, items) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => builder$0040.Combine((maxConcurrency <= 0) ? (((() => {
        throw new Exception("maxConcurrency must be greater than 0 (Parameter \'maxConcurrency\')");
    })(), builder$0040.Zero())) : builder$0040.Zero(), builder$0040.Delay(() => {
        const indexedItems = toArray(items);
        return (indexedItems.length === 0) ? builder$0040.Return(empty()) : builder$0040.Using(AsyncSemaphore_$ctor_Z524259A4(maxConcurrency), (_arg) => {
            const semaphore = _arg;
            const promises = map((value) => value, map((item) => {
                const builder$0040_1 = task();
                return builder$0040_1.Run(builder$0040_1.Delay(() => builder$0040_1.Bind(AsyncSemaphore__WaitAsync_Z211DAE3E(semaphore, cancellation), () => builder$0040_1.TryFinally(builder$0040_1.Delay(() => builder$0040_1.ReturnFrom(action(item, cancellation))), () => {
                    AsyncSemaphore__Release(semaphore);
                }))));
            }, indexedItems));
            return builder$0040.Bind(Promise.all(promises), (_arg_2) => builder$0040.Return(ofArray(_arg_2)));
        });
    }))));
}

