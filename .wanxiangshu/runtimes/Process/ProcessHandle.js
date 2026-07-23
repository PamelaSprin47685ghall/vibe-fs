
import { toString, Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { class_type, union_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { JsTcs$1__TrySetResult_2B595, JsTcs$1_$ctor, Flow_create, JsTcs$1__get_Task } from "../Kernel/Flow.js";
import { Operators_IsNull } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { exists } from "../fable_modules/fable-library-js.5.6.0/Seq.js";
import { DeadlineModule_remaining, DeadlineModule_isExpired } from "./Deadline.js";
import { utcNow } from "../fable_modules/fable-library-js.5.6.0/DateOffset.js";
import { defaultArg, map, bind, orElse, toNullable, toArray } from "../fable_modules/fable-library-js.5.6.0/Option.js";
import { ProcessError } from "./ProcessTypes.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";
import { cancel, cancelAfter, isCancellationRequested, createCancellationToken } from "../fable_modules/fable-library-js.5.6.0/Async.js";
import { defaultOf } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { ProcessResult } from "../Kernel/Fact.js";
import { spawn } from "node:child_process";
import { toArray as toArray_1 } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { ProcessPump_pumpStream } from "./ProcessPump.js";

class ProcessSpawn_ProcessCompletion extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Exited", "Cancelled", "TimedOut"];
    }
}

function ProcessSpawn_ProcessCompletion_$reflection() {
    return union_type("Wanxiangshu.Next.Process.ProcessSpawn.ProcessCompletion", [], ProcessSpawn_ProcessCompletion, () => [[], [], []]);
}

class ProcessSpawn_ProcessHandleImpl {
    constructor(childProc, cts, exitTcs, stdoutTask, stderrTask, cmd) {
        this.childProc = childProc;
        this.cts = cts;
        this.exitTcs = exitTcs;
        this.stdoutTask = stdoutTask;
        this.stderrTask = stderrTask;
        this.cmd = cmd;
        this.killed = false;
    }
    get ExitCodeTask() {
        const _ = this;
        return JsTcs$1__get_Task(_.exitTcs);
    }
    get StdoutTask() {
        const _ = this;
        return _.stdoutTask;
    }
    get StderrTask() {
        const _ = this;
        return _.stderrTask;
    }
    get IsPty() {
        const _ = this;
        return _.cmd.PtyOptions != null;
    }
    ResizePty(cols, rows) {
        const _ = this;
        if ((_.cmd.PtyOptions != null) && !Operators_IsNull(_.childProc.resize)) {
            try {
                _.childProc.resize(cols, rows);
            }
            catch (matchValue) {
            }
        }
    }
    Kill() {
        const _ = this;
        return ProcessSpawn_ProcessHandleImpl__killProc(_);
    }
    RunToCompletion() {
        const _ = this;
        return Flow_create((_ctx, ct) => {
            const builder$0040 = task();
            return builder$0040.Run(builder$0040.Delay(() => builder$0040.TryWith(builder$0040.Delay(() => {
                const isDeadlineExpired = () => exists((arg10$0040) => DeadlineModule_isExpired(utcNow, arg10$0040), toArray(_.cmd.Deadline));
                if (isDeadlineExpired()) {
                    return builder$0040.Bind(ProcessSpawn_ProcessHandleImpl__killProc(_), () => builder$0040.Return(new FSharpResult$2(1, [new ProcessError(2, ["Process deadline expired"])])));
                }
                else {
                    const completion = JsTcs$1_$ctor();
                    const stopProcess = (result) => {
                        JsTcs$1__TrySetResult_2B595(completion, result);
                        try {
                            _.childProc.kill("SIGTERM");
                        }
                        catch (matchValue) {
                        }
                    };
                    return builder$0040.Using(ct.register(() => {
                        stopProcess(new ProcessSpawn_ProcessCompletion(1, []));
                    }), (_arg_1) => builder$0040.Using(_.cts.register(() => {
                        stopProcess(new ProcessSpawn_ProcessCompletion(1, []));
                    }), (_arg_2) => builder$0040.Using(createCancellationToken(), (_arg_3) => {
                        const deadlineCancellation = _arg_3;
                        return builder$0040.Using(deadlineCancellation.register(() => {
                            stopProcess(new ProcessSpawn_ProcessCompletion(2, []));
                        }), (_arg_4) => builder$0040.Combine((isCancellationRequested(ct) ? true : isCancellationRequested(_.cts)) ? ((stopProcess(new ProcessSpawn_ProcessCompletion(1, [])), builder$0040.Zero())) : builder$0040.Zero(), builder$0040.Delay(() => {
                            let matchValue_1;
                            return builder$0040.Combine((matchValue_1 = _.cmd.Deadline, (matchValue_1 == null) ? (builder$0040.Zero()) : ((cancelAfter(deadlineCancellation, DeadlineModule_remaining(utcNow, matchValue_1)), builder$0040.Zero()))), builder$0040.Delay(() => {
                                let builder$0040_1;
                                (builder$0040_1 = task(), builder$0040_1.Run(builder$0040_1.Delay(() => builder$0040_1.Bind(JsTcs$1__get_Task(_.exitTcs), (_arg_5) => {
                                    JsTcs$1__TrySetResult_2B595(completion, new ProcessSpawn_ProcessCompletion(0, []));
                                    return builder$0040_1.Zero();
                                }))));
                                return builder$0040.Bind(JsTcs$1__get_Task(completion), (_arg_6) => {
                                    const terminal = _arg_6;
                                    return (terminal.tag === 2) ? builder$0040.Return(new FSharpResult$2(1, [new ProcessError(2, ["Process deadline expired"])])) : ((terminal.tag === 0) ? ((isCancellationRequested(ct) ? true : isCancellationRequested(_.cts)) ? builder$0040.Return(new FSharpResult$2(1, [new ProcessError(1, ["Operation was cancelled"])])) : (isDeadlineExpired() ? builder$0040.Return(new FSharpResult$2(1, [new ProcessError(2, ["Process deadline expired"])])) : builder$0040.ReturnFrom(ProcessSpawn_ProcessHandleImpl__getOkResult(_)))) : builder$0040.Return(new FSharpResult$2(1, [new ProcessError(1, ["Operation was cancelled"])])));
                                });
                            }));
                        })));
                    })));
                }
            }), (_arg_7) => builder$0040.Bind(ProcessSpawn_ProcessHandleImpl__killProc(_), () => builder$0040.Return(new FSharpResult$2(1, [new ProcessError(3, [_arg_7.message])]))))));
        });
    }
    Dispose() {
        const _ = this;
        try {
            _.childProc.kill("SIGTERM");
        }
        catch (matchValue) {
        }
        try {
        }
        catch (matchValue_1) {
        }
    }
    "System.IAsyncDisposable.DisposeAsync"() {
        const _ = this;
        try {
            _.childProc.kill("SIGTERM");
        }
        catch (matchValue) {
        }
        try {
        }
        catch (matchValue_1) {
        }
        return defaultOf();
    }
}

function ProcessSpawn_ProcessHandleImpl_$reflection() {
    return class_type("Wanxiangshu.Next.Process.ProcessSpawn.ProcessHandleImpl", undefined, ProcessSpawn_ProcessHandleImpl);
}

function ProcessSpawn_ProcessHandleImpl_$ctor_Z1BEF3840(childProc, cts, exitTcs, stdoutTask, stderrTask, cmd) {
    return new ProcessSpawn_ProcessHandleImpl(childProc, cts, exitTcs, stdoutTask, stderrTask, cmd);
}

export function ProcessSpawn_ProcessHandleImpl__killProc(this$) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => builder$0040.Combine(!this$.killed ? ((this$.killed = true, builder$0040.Combine(builder$0040.TryWith(builder$0040.Delay(() => {
        this$.childProc.kill("SIGTERM");
        return builder$0040.Zero();
    }), (_arg) => {
        return builder$0040.Zero();
    }), builder$0040.Delay(() => builder$0040.TryWith(builder$0040.Delay(() => {
        cancel(this$.cts);
        return builder$0040.Zero();
    }), (_arg_1) => {
        return builder$0040.Zero();
    }))))) : builder$0040.Zero(), builder$0040.Delay(() => builder$0040.Return(undefined)))));
}

export function ProcessSpawn_ProcessHandleImpl__getOkResult(this$) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => builder$0040.Bind(JsTcs$1__get_Task(this$.exitTcs), (_arg) => builder$0040.Bind(this$.stdoutTask, (_arg_1) => builder$0040.Bind(this$.stderrTask, (_arg_2) => builder$0040.Return(new FSharpResult$2(0, [new ProcessResult(_arg, _arg_1[0], _arg_2[0], _arg_1[1], _arg_2[1])])))))));
}

export function ProcessSpawn_spawn(cmd, ctx, cancellation) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => builder$0040.TryWith(builder$0040.Delay(() => {
        let matchValue, stdin;
        const opts = {
            cwd: toNullable(orElse(cmd.WorkingDirectory, bind((c) => c.WorkingDirectory, ctx))),
            env: toNullable(map((value_1) => value_1, cmd.Environment)),
        };
        const childProc = spawn(cmd.FileName, toArray_1(cmd.Arguments), opts);
        const cts = createCancellationToken();
        const exitTcs = JsTcs$1_$ctor();
        let spawnError = undefined;
        const stdoutTask = ProcessPump_pumpStream(childProc.stdout, cts, 100 * 1024);
        const stderrTask = ProcessPump_pumpStream(childProc.stderr, cts, 100 * 1024);
        childProc.on("exit", ((code) => {
            JsTcs$1__TrySetResult_2B595(exitTcs, Operators_IsNull(code) ? 0 : code);
        }));
        childProc.on("error", ((err) => {
            const msg = Operators_IsNull(err) ? "Failed to spawn process" : toString(err);
            spawnError = msg;
            JsTcs$1__TrySetResult_2B595(exitTcs, -1);
        }));
        return builder$0040.Combine((matchValue = cmd.Stdin, (matchValue == null) ? (builder$0040.Zero()) : ((stdin = matchValue, builder$0040.TryWith(builder$0040.Delay(() => {
            childProc.stdin.write(stdin, "utf-8");
            childProc.stdin.end();
            return builder$0040.Zero();
        }), (_arg) => {
            return builder$0040.Zero();
        })))), builder$0040.Delay(() => {
            if (Operators_IsNull(childProc.pid)) {
                return builder$0040.Bind(Promise.resolve(), () => builder$0040.Return(new FSharpResult$2(1, [new ProcessError(0, [defaultArg(spawnError, "Failed to spawn process")])])));
            }
            else if (spawnError == null) {
                return builder$0040.Return(new FSharpResult$2(0, [ProcessSpawn_ProcessHandleImpl_$ctor_Z1BEF3840(childProc, cts, exitTcs, stdoutTask, stderrTask, cmd)]));
            }
            else {
                const err_1 = spawnError;
                return builder$0040.Return(new FSharpResult$2(1, [new ProcessError(0, [err_1])]));
            }
        }));
    }), (_arg_2) => builder$0040.Return(new FSharpResult$2(1, [new ProcessError(0, [_arg_2.message])])))));
}

