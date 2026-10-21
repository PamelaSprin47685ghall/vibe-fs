// .build/process-27272/native-process/BunProcess.js
import { Readable, Writable } from "node:stream";
import * as node$003Afs from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { EventEmitter } from "node:events";
function spawn(file, arguments$, options) {
  const outputs = (() => {
    const files = node$003Afs;
    const directory = files.mkdtempSync(join(tmpdir(), "wanxiangli-pipes-"));
    const paths = [directory + "/stdout", directory + "/stderr"];
    const descriptors = /* @__PURE__ */ new Set();
    const outputs2 = [];
    const reader = (descriptor) => {
      let pending = null;
      let immediate = false;
      let idleDelay = 0;
      let buffer;
      const receive = () => {
        if (stream.destroyed) return;
        buffer ??= Buffer.allocUnsafe(65536);
        let count;
        try {
          count = files.readSync(descriptor, buffer, 0, buffer.length, null);
        } catch (error) {
          if (error.code === "EAGAIN" || error.code === "EWOULDBLOCK") {
            const retry = () => {
              pending = null;
              receive();
            };
            immediate = idleDelay === 0;
            pending = immediate ? setImmediate(retry) : setTimeout(retry, idleDelay);
            idleDelay = idleDelay === 0 ? 1 : Math.min(5, idleDelay * 2);
          } else {
            stream.destroy(error);
          }
          return;
        }
        idleDelay = 0;
        if (count === 0) stream.push(null);
        else stream.push(Buffer.from(buffer.subarray(0, count)));
      };
      const stream = new Readable({
        highWaterMark: 65536,
        read() {
          if (pending === null) receive();
        },
        destroy(error, callback) {
          if (immediate) clearImmediate(pending);
          else clearTimeout(pending);
          pending = null;
          buffer = void 0;
          const owned = descriptor;
          descriptor = -1;
          try {
            files.closeSync(owned);
            callback(error);
          } catch (closeError) {
            callback(error ?? closeError);
          }
        }
      });
      return stream;
    };
    try {
      const created = globalThis.Bun.spawnSync(["/usr/bin/mkfifo", ...paths], {
        cwd: directory,
        env: {},
        stdin: "ignore",
        stdout: "ignore",
        stderr: "pipe"
      });
      if (created.exitCode !== 0) throw Object.assign(new Error(new TextDecoder().decode(created.stderr) || "Could not create output pipes"), { code: "PIPE_SETUP_FAILED" });
      const pipes = paths.map((path) => {
        const read = files.openSync(path, files.constants.O_RDONLY | files.constants.O_NONBLOCK);
        descriptors.add(read);
        const write = files.openSync(path, files.constants.O_WRONLY);
        descriptors.add(write);
        return { read, write };
      });
      for (const path of paths) files.unlinkSync(path);
      files.rmdirSync(directory);
      for (const pipe of pipes) {
        outputs2.push({ readable: reader(pipe.read), writer: pipe.write });
        descriptors.delete(pipe.read);
      }
      return outputs2;
    } catch (error) {
      let cleanupFailure;
      for (const output of outputs2) {
        output.readable.on("error", () => {
        });
        output.readable.destroy();
        cleanupFailure ??= output.readable.errored;
      }
      for (const descriptor of descriptors) {
        try {
          files.closeSync(descriptor);
        } catch (closeError) {
          cleanupFailure ??= closeError;
        }
      }
      try {
        files.rmSync(directory, { recursive: true, force: true });
      } catch (removeError) {
        cleanupFailure ??= removeError;
      }
      if (cleanupFailure) throw Object.assign(new Error(error.message + "; output cleanup was not confirmed: " + cleanupFailure.message), { code: cleanupFailure.code || "PIPE_CLEANUP_FAILED" });
      throw error;
    }
  })();
  return (() => {
    const child = new EventEmitter();
    let startPublished = false;
    let exit;
    let exitPublished = false;
    let closed = false;
    let outputClosed = 0;
    let pendingWrite;
    let nativeProcess;
    const tryClose = () => {
      if (exitPublished && outputClosed === 2 && !closed) {
        closed = true;
        child.emit("close", exit.code, exit.signal);
      }
    };
    const publishExit = () => {
      if (!startPublished || !exit || exitPublished) return;
      exitPublished = true;
      if (pendingWrite) pendingWrite(Object.assign(new Error("Process exited before the input write was acknowledged"), { code: "ERR_STREAM_DESTROYED" }));
      child.stdin.destroy();
      if (exit.error) child.emit("error", exit.error);
      child.emit("exit", exit.code, exit.signal);
      tryClose();
    };
    child.stdout = outputs[0].readable;
    child.stderr = outputs[1].readable;
    for (const output of [child.stdout, child.stderr]) output.once("close", () => {
      outputClosed++;
      tryClose();
    });
    child.stdin = new Writable({
      write(bytes, _encoding, callback) {
        let completed = false;
        const complete = (error) => {
          if (completed) return;
          completed = true;
          pendingWrite = void 0;
          callback(error);
        };
        pendingWrite = complete;
        Promise.resolve().then(() => nativeProcess.stdin.write(bytes)).then(async () => {
          await nativeProcess.stdin.flush();
          setImmediate(() => complete());
        }).catch(complete);
      },
      final(callback) {
        Promise.resolve().then(() => nativeProcess.stdin.end()).then(() => callback(), callback);
      }
    });
    try {
      nativeProcess = globalThis.Bun.spawn([file, ...arguments$], {
        cwd: options.cwd,
        env: options.env,
        detached: options.detached,
        stdin: "pipe",
        stdout: outputs[0].writer,
        stderr: outputs[1].writer,
        onExit(_process, code, signal, error) {
          exit = { code: code ?? null, signal: signal ?? null, error };
          publishExit();
        }
      });
    } catch (error) {
      child.stdin.destroy();
      let cleanupFailure;
      for (const output of outputs) {
        output.readable.on("error", () => {
        });
        output.readable.destroy();
        cleanupFailure ??= output.readable.errored;
        try {
          node$003Afs.closeSync(output.writer);
        } catch (closeError) {
          cleanupFailure ??= closeError;
        }
      }
      if (cleanupFailure) throw Object.assign(new Error(error.message + "; output cleanup was not confirmed: " + cleanupFailure.message), { code: cleanupFailure.code || "PIPE_CLEANUP_FAILED" });
      throw error;
    }
    child.pid = nativeProcess.pid;
    let closeFailure;
    for (const output of outputs) {
      try {
        node$003Afs.closeSync(output.writer);
      } catch (error) {
        closeFailure ??= error;
      }
    }
    queueMicrotask(() => {
      startPublished = true;
      child.emit("spawn");
      if (closeFailure) {
        child.emit("error", closeFailure);
        child.stdout.destroy(closeFailure);
        child.stderr.destroy(closeFailure);
      }
      publishExit();
    });
    return child;
  })();
}

// .build/process-27272/Native.js
import { spawn as spawn_1 } from "node:child_process";

// node_modules/@fable-org/fable-library-js/Util.js
var DateTimeKind = {
  Unspecified: 0,
  Utc: 1,
  Local: 2
};
var Exception = class {
  constructor(msg, innerException) {
    this.message = msg ?? "";
    this.innerException = innerException;
  }
  toString() {
    return this.message;
  }
};
function isArrayLike(x) {
  return Array.isArray(x) || ArrayBuffer.isView(x);
}
function isEnumerable(x) {
  return x != null && typeof x.GetEnumerator === "function";
}
function isComparable(x) {
  return x != null && typeof x.CompareTo === "function";
}
function isEquatable(x) {
  return x != null && typeof x.Equals === "function";
}
function isHashable(x) {
  return x != null && typeof x.GetHashCode === "function";
}
function isDisposable(x) {
  return x != null && typeof x.Dispose === "function";
}
function disposeSafe(x) {
  if (isDisposable(x)) {
    x.Dispose();
  }
}
function defaultOf() {
  return null;
}
function sameConstructor(x, y) {
  return Object.getPrototypeOf(x)?.constructor === Object.getPrototypeOf(y)?.constructor;
}
var Enumerator = class {
  constructor(iter) {
    this.current = defaultOf();
    this.iter = iter;
  }
  ["System.Collections.Generic.IEnumerator`1.get_Current"]() {
    return this.current;
  }
  ["System.Collections.IEnumerator.get_Current"]() {
    return this.current;
  }
  ["System.Collections.IEnumerator.MoveNext"]() {
    const cur = this.iter.next();
    this.current = cur.value;
    return !cur.done;
  }
  ["System.Collections.IEnumerator.Reset"]() {
    throw new Exception("JS iterators cannot be reset");
  }
  Dispose() {
    return;
  }
};
function getEnumerator(e) {
  if (isEnumerable(e)) {
    return e.GetEnumerator();
  } else {
    return new Enumerator(e[Symbol.iterator]());
  }
}
function toIterator(en) {
  return {
    next() {
      const hasNext = en["System.Collections.IEnumerator.MoveNext"]();
      const current = hasNext ? en["System.Collections.Generic.IEnumerator`1.get_Current"]() : void 0;
      return { done: !hasNext, value: current };
    }
  };
}
function dateOffset(date) {
  const date1 = date;
  return typeof date1.offset === "number" ? date1.offset : date.kind === DateTimeKind.Utc ? 0 : date.getTimezoneOffset() * -6e4;
}
function int32ToString(i, radix) {
  i = i < 0 && radix != null && radix !== 10 ? 4294967295 + i + 1 : i;
  return i.toString(radix);
}
var ObjectRef = class _ObjectRef {
  static id(o) {
    if (!_ObjectRef.idMap.has(o)) {
      _ObjectRef.idMap.set(o, ++_ObjectRef.count);
    }
    return _ObjectRef.idMap.get(o);
  }
};
ObjectRef.idMap = /* @__PURE__ */ new WeakMap();
ObjectRef.count = 0;
function stringHash(s) {
  let i = 0;
  let h = 5381;
  const len = s.length;
  while (i < len) {
    h = h * 33 ^ s.charCodeAt(i++);
  }
  return h;
}
function numberHash(x) {
  return x * 2654435761 | 0;
}
function bigintHash(x) {
  return stringHash(x.toString(32));
}
function combineHashCodes(hashes) {
  let h1 = 0;
  const len = hashes.length;
  for (let i = 0; i < len; i++) {
    const h2 = hashes[i];
    h1 = (h1 << 5) + h1 ^ h2;
  }
  return h1;
}
function dateHash(x) {
  return x.getTime();
}
function arrayHash(x) {
  const len = x.length;
  const hashes = new Array(len);
  for (let i = 0; i < len; i++) {
    hashes[i] = structuralHash(x[i]);
  }
  return combineHashCodes(hashes);
}
function structuralHash(x) {
  if (x == null) {
    return 0;
  }
  switch (typeof x) {
    case "boolean":
      return x ? 1 : 0;
    case "number":
      return numberHash(x);
    case "bigint":
      return bigintHash(x);
    case "string":
      return stringHash(x);
    default: {
      if (isHashable(x)) {
        return x.GetHashCode();
      } else if (isArrayLike(x)) {
        return arrayHash(x);
      } else if (x instanceof Date) {
        return dateHash(x);
      } else if (Object.getPrototypeOf(x)?.constructor === Object) {
        const hashes = Object.values(x).map((v) => structuralHash(v));
        return combineHashCodes(hashes);
      } else {
        return numberHash(ObjectRef.id(x));
      }
    }
  }
}
function equalArraysWith(x, y, eq) {
  if (x == null) {
    return y == null;
  }
  if (y == null) {
    return false;
  }
  if (x.length !== y.length) {
    return false;
  }
  for (let i = 0; i < x.length; i++) {
    if (!eq(x[i], y[i])) {
      return false;
    }
  }
  return true;
}
function equalArrays(x, y) {
  return equalArraysWith(x, y, equals);
}
function equalObjects(x, y) {
  const xKeys = Object.keys(x);
  const yKeys = Object.keys(y);
  if (xKeys.length !== yKeys.length) {
    return false;
  }
  xKeys.sort();
  yKeys.sort();
  for (let i = 0; i < xKeys.length; i++) {
    if (xKeys[i] !== yKeys[i] || !equals(x[xKeys[i]], y[yKeys[i]])) {
      return false;
    }
  }
  return true;
}
function equals(x, y) {
  if (x === y) {
    return true;
  } else if (x == null) {
    return y == null;
  } else if (y == null) {
    return false;
  } else if (isEquatable(x)) {
    return x.Equals(y);
  } else if (isArrayLike(x)) {
    return isArrayLike(y) && equalArrays(x, y);
  } else if (typeof x !== "object") {
    return false;
  } else if (x instanceof Date) {
    return y instanceof Date && compareDates(x, y) === 0;
  } else {
    return Object.getPrototypeOf(x)?.constructor === Object && equalObjects(x, y);
  }
}
function compareDates(x, y) {
  let xtime;
  let ytime;
  if ("offset" in x && "offset" in y) {
    xtime = x.getTime();
    ytime = y.getTime();
  } else {
    xtime = x.getTime() + dateOffset(x);
    ytime = y.getTime() + dateOffset(y);
  }
  return xtime === ytime ? 0 : xtime < ytime ? -1 : 1;
}
function comparePrimitives(x, y) {
  if (x === y) {
    return 0;
  }
  if (x < y) {
    return -1;
  }
  if (x > y) {
    return 1;
  }
  return Number.isNaN(x) ? Number.isNaN(y) ? 0 : -1 : 1;
}
function compareArraysWith(x, y, comp) {
  if (x == null) {
    return y == null ? 0 : 1;
  }
  if (y == null) {
    return -1;
  }
  if (x.length !== y.length) {
    return x.length < y.length ? -1 : 1;
  }
  for (let i = 0, j = 0; i < x.length; i++) {
    j = comp(x[i], y[i]);
    if (j !== 0) {
      return j;
    }
  }
  return 0;
}
function compareArrays(x, y) {
  return compareArraysWith(x, y, compare);
}
function compareObjects(x, y) {
  const xKeys = Object.keys(x);
  const yKeys = Object.keys(y);
  if (xKeys.length !== yKeys.length) {
    return xKeys.length < yKeys.length ? -1 : 1;
  }
  xKeys.sort();
  yKeys.sort();
  for (let i = 0, j = 0; i < xKeys.length; i++) {
    const key = xKeys[i];
    if (key !== yKeys[i]) {
      return key < yKeys[i] ? -1 : 1;
    } else {
      j = compare(x[key], y[key]);
      if (j !== 0) {
        return j;
      }
    }
  }
  return 0;
}
function compare(x, y) {
  if (x === y) {
    return 0;
  } else if (x == null) {
    return y == null ? 0 : -1;
  } else if (y == null) {
    return 1;
  } else if (isComparable(x)) {
    return x.CompareTo(y);
  } else if (isArrayLike(x)) {
    return isArrayLike(y) ? compareArrays(x, y) : -1;
  } else if (typeof x !== "object") {
    return comparePrimitives(x, y);
  } else if (x instanceof Date) {
    return y instanceof Date ? compareDates(x, y) : -1;
  } else {
    return Object.getPrototypeOf(x)?.constructor === Object ? compareObjects(x, y) : -1;
  }
}
function createObj(fields) {
  const obj = {};
  for (const kv of fields) {
    obj[kv[0]] = kv[1];
  }
  return obj;
}
var curried = /* @__PURE__ */ new WeakMap();
function curry2(f) {
  return curried.get(f) ?? ((a1) => (a2) => f(a1, a2));
}

// node_modules/@fable-org/fable-library-js/Native.js
function Helpers_allocateArrayFromCons(cons, len) {
  if (cons == null) {
    return new Array(len);
  } else {
    const cons_1 = cons;
    if (typeof cons_1 === "function") {
      return new cons_1(len);
    } else {
      return new Array(len);
    }
  }
}

// node_modules/@fable-org/fable-library-js/Option.js
var Some = class _Some {
  constructor(value2) {
    this.value = value2;
  }
  toJSON() {
    return this.value;
  }
  // Don't add "Some" for consistency with erased options
  toString() {
    return String(this.value);
  }
  GetHashCode() {
    return structuralHash(this.value);
  }
  Equals(other) {
    if (other == null) {
      return false;
    } else {
      return equals(this.value, other instanceof _Some ? other.value : other);
    }
  }
  CompareTo(other) {
    if (other == null) {
      return 1;
    } else {
      return compare(this.value, other instanceof _Some ? other.value : other);
    }
  }
};
function value(x) {
  if (x == null) {
    throw new Exception("Option has no value");
  } else {
    return x instanceof Some ? x.value : x;
  }
}
function some(x) {
  return x == null || x instanceof Some ? new Some(x) : x;
}

// node_modules/@fable-org/fable-library-js/Types.js
function seqToString(self) {
  let count = 0;
  let str = "[";
  for (const x of self) {
    if (count === 0) {
      str += toStringQuoted(x);
    } else if (count === 100) {
      str += "; ...";
      break;
    } else {
      str += "; " + toStringQuoted(x);
    }
    count++;
  }
  return str + "]";
}
function toStringQuoted(x, callStack = 0) {
  return typeof x === "string" ? '"' + x + '"' : toString(x, callStack);
}
function toString(x, callStack = 0) {
  if (x != null && typeof x === "object") {
    if (typeof x.toString === "function" && x.toString !== Object.prototype.toString) {
      return x.toString();
    } else if (Symbol.iterator in x) {
      return seqToString(x);
    } else {
      const cons = Object.getPrototypeOf(x)?.constructor;
      return cons === Object && callStack < 10 ? "{ " + Object.entries(x).map(([k, v]) => k + " = " + toStringQuoted(v, callStack + 1)).join("\n  ") + " }" : cons?.name ?? "";
    }
  }
  return String(x);
}
function recordToJSON(self) {
  const o = {};
  const keys = Object.keys(self);
  for (let i = 0; i < keys.length; i++) {
    o[keys[i]] = self[keys[i]];
  }
  return o;
}
function recordToString(self) {
  return "{ " + Object.entries(self).map(([k, v]) => k + " = " + toStringQuoted(v)).join("\n  ") + " }";
}
function recordGetHashCode(self) {
  const hashes = Object.values(self).map((v) => structuralHash(v));
  return combineHashCodes(hashes);
}
function recordEquals(self, other) {
  if (self === other) {
    return true;
  } else if (!sameConstructor(self, other)) {
    return false;
  } else {
    const thisNames = Object.keys(self);
    for (let i = 0; i < thisNames.length; i++) {
      if (!equals(self[thisNames[i]], other[thisNames[i]])) {
        return false;
      }
    }
    return true;
  }
}
function recordCompareTo(self, other) {
  if (self === other) {
    return 0;
  } else if (!sameConstructor(self, other)) {
    return -1;
  } else {
    const thisNames = Object.keys(self);
    for (let i = 0; i < thisNames.length; i++) {
      const result = compare(self[thisNames[i]], other[thisNames[i]]);
      if (result !== 0) {
        return result;
      }
    }
    return 0;
  }
}
var Record = class {
  toJSON() {
    return recordToJSON(this);
  }
  toString() {
    return recordToString(this);
  }
  GetHashCode() {
    return recordGetHashCode(this);
  }
  Equals(other) {
    return recordEquals(this, other);
  }
  CompareTo(other) {
    return recordCompareTo(this, other);
  }
};

// node_modules/@fable-org/fable-library-js/Double.js
function max(x, y) {
  return Math.max(x, y);
}
function min(x, y) {
  return Math.min(x, y);
}

// node_modules/@fable-org/fable-library-js/Global.js
var SR_inputWasEmpty = "Collection was empty.";

// node_modules/@fable-org/fable-library-js/FSharp.Core.js
function Operators_IsNull(value2) {
  if (equals(value2, defaultOf())) {
    return true;
  } else {
    return false;
  }
}

// node_modules/@fable-org/fable-library-js/Array.js
function fill(target, targetIndex, count, value2) {
  const start = targetIndex | 0;
  return target.fill(value2, start, start + count);
}
function map(f, source, cons) {
  const len = source.length | 0;
  const target = Helpers_allocateArrayFromCons(cons, len);
  for (let i = 0; i <= len - 1; i++) {
    setItem(target, i, f(item(i, source)));
  }
  return target;
}
function initialize(count, initializer, cons) {
  if (count < 0) {
    throw new Exception("The input must be non-negative (Parameter 'count')");
  }
  const result = Helpers_allocateArrayFromCons(cons, count);
  for (let i = 0; i <= count - 1; i++) {
    setItem(result, i, initializer(i));
  }
  return result;
}
function copy(array) {
  return array.slice();
}
function sort(xs, comparer) {
  const xs_1 = xs.slice();
  xs_1.sort((x, y) => comparer.Compare(x, y) | 0);
  return xs_1;
}
function equalsWith(equals2, source1, source2) {
  if (Operators_IsNull(source1)) {
    if (Operators_IsNull(source2)) {
      return true;
    } else {
      return false;
    }
  } else if (Operators_IsNull(source2)) {
    return false;
  } else {
    let i = 0;
    let result = true;
    const length1 = source1.length | 0;
    const length2 = source2.length | 0;
    if (length1 > length2) {
      return false;
    } else if (length1 < length2) {
      return false;
    } else {
      while (i < length1 && result) {
        result = equals2(item(i, source1), item(i, source2));
        i = i + 1 | 0;
      }
      return result;
    }
  }
}
function item(index, array) {
  if (index < 0 ? true : index >= array.length) {
    throw new Exception("Index was outside the bounds of the array. (Parameter 'index')");
  } else {
    return array[index];
  }
}
function setItem(array, index, value2) {
  if (index < 0 ? true : index >= array.length) {
    throw new Exception("Index was outside the bounds of the array. (Parameter 'index')");
  } else {
    array[index] = value2;
  }
}

// node_modules/@fable-org/fable-library-js/Seq.js
function toArray2(xs) {
  if (xs instanceof FSharpList) {
    const a = xs;
    return toArray(a);
  } else {
    return Array.from(xs);
  }
}

// node_modules/@fable-org/fable-library-js/List.js
var FSharpList = class _FSharpList extends Record {
  constructor(head, tail) {
    super();
    this.head = head;
    this.tail = tail;
  }
  toString() {
    const xs = this;
    let result = "[";
    let first = true;
    const enumerator = getEnumerator(xs);
    try {
      while (enumerator["System.Collections.IEnumerator.MoveNext"]()) {
        let x = void 0, matchValue = void 0, s = void 0;
        const x_1 = enumerator["System.Collections.Generic.IEnumerator`1.get_Current"]();
        result = (first ? result : result + "; ") + (x = x_1, matchValue = x, typeof matchValue === "string" ? (s = matchValue, '"' + s + '"') : toString(x));
        first = false;
      }
    } finally {
      disposeSafe(enumerator);
    }
    return result + "]";
  }
  Equals(other) {
    const xs = this;
    if (xs === other) {
      return true;
    } else if (other instanceof _FSharpList) {
      const ys = other;
      const loop = (xs_1_mut, ys_1_mut) => {
        loop: while (true) {
          const xs_1 = xs_1_mut, ys_1 = ys_1_mut;
          const matchValue = xs_1.tail;
          const matchValue_1 = ys_1.tail;
          if (matchValue != null) {
            if (matchValue_1 != null) {
              const xt = value(matchValue);
              const yt = value(matchValue_1);
              if (equals(xs_1.head, ys_1.head)) {
                xs_1_mut = xt;
                ys_1_mut = yt;
                continue loop;
              } else {
                return false;
              }
            } else {
              return false;
            }
          } else if (matchValue_1 != null) {
            return false;
          } else {
            return true;
          }
          break;
        }
      };
      return loop(xs, ys);
    } else {
      return false;
    }
  }
  GetHashCode() {
    const xs = this;
    const loop = (i_mut, h_mut, xs_1_mut) => {
      loop: while (true) {
        const i = i_mut, h = h_mut, xs_1 = xs_1_mut;
        const matchValue = xs_1.tail;
        if (matchValue != null) {
          const t = value(matchValue);
          if (i > 18) {
            return h | 0;
          } else {
            i_mut = i + 1;
            h_mut = (h << 1) + structuralHash(xs_1.head) + 631 * i;
            xs_1_mut = t;
            continue loop;
          }
        } else {
          return h | 0;
        }
        break;
      }
    };
    return loop(0, 0, xs) | 0;
  }
  toJSON() {
    const this$ = this;
    return Array.from(this$);
  }
  CompareTo(other) {
    const xs = this;
    if (other instanceof _FSharpList) {
      const ys = other;
      const loop = (xs_1_mut, ys_1_mut) => {
        loop: while (true) {
          const xs_1 = xs_1_mut, ys_1 = ys_1_mut;
          const matchValue = xs_1.tail;
          const matchValue_1 = ys_1.tail;
          if (matchValue != null) {
            if (matchValue_1 != null) {
              const xt = value(matchValue);
              const yt = value(matchValue_1);
              const c = compare(xs_1.head, ys_1.head) | 0;
              if (c === 0) {
                xs_1_mut = xt;
                ys_1_mut = yt;
                continue loop;
              } else {
                return c | 0;
              }
            } else {
              return 1;
            }
          } else if (matchValue_1 != null) {
            return -1;
          } else {
            return 0;
          }
          break;
        }
      };
      return loop(xs, ys) | 0;
    } else {
      return 1;
    }
  }
  GetEnumerator() {
    const xs = this;
    return ListEnumerator$1_$ctor_3002E699(xs);
  }
  [Symbol.iterator]() {
    return toIterator(getEnumerator(this));
  }
  "System.Collections.IEnumerable.GetEnumerator"() {
    const xs = this;
    return getEnumerator(xs);
  }
};
var ListEnumerator$1 = class {
  constructor(xs) {
    this.xs = xs;
    this.it = this.xs;
    this.current = defaultOf();
  }
  "System.Collections.Generic.IEnumerator`1.get_Current"() {
    const _ = this;
    return _.current;
  }
  "System.Collections.IEnumerator.get_Current"() {
    const _ = this;
    return _.current;
  }
  "System.Collections.IEnumerator.MoveNext"() {
    const _ = this;
    const matchValue = _.it.tail;
    if (matchValue != null) {
      const t = value(matchValue);
      _.current = _.it.head;
      _.it = t;
      return true;
    } else {
      return false;
    }
  }
  "System.Collections.IEnumerator.Reset"() {
    const _ = this;
    _.it = _.xs;
    _.current = defaultOf();
  }
  Dispose() {
  }
};
function ListEnumerator$1_$ctor_3002E699(xs) {
  return new ListEnumerator$1(xs);
}
function FSharpList_get_Empty() {
  return new FSharpList(defaultOf(), void 0);
}
function FSharpList_Cons_305B8EAC(x, xs) {
  return new FSharpList(x, xs);
}
function FSharpList__get_IsEmpty(xs) {
  return xs.tail == null;
}
function FSharpList__get_Length(xs) {
  const loop = (i_mut, xs_1_mut) => {
    loop: while (true) {
      const i = i_mut, xs_1 = xs_1_mut;
      const matchValue = xs_1.tail;
      if (matchValue != null) {
        i_mut = i + 1;
        xs_1_mut = value(matchValue);
        continue loop;
      } else {
        return i | 0;
      }
      break;
    }
  };
  return loop(0, xs) | 0;
}
function FSharpList__get_Head(xs) {
  const matchValue = xs.tail;
  if (matchValue != null) {
    return xs.head;
  } else {
    throw new Exception(SR_inputWasEmpty + " (Parameter 'list')");
  }
}
function FSharpList__get_Tail(xs) {
  const matchValue = xs.tail;
  if (matchValue != null) {
    return value(matchValue);
  } else {
    throw new Exception(SR_inputWasEmpty + " (Parameter 'list')");
  }
}
function singleton(x) {
  return FSharpList_Cons_305B8EAC(x, FSharpList_get_Empty());
}
function toArray(xs) {
  const len = FSharpList__get_Length(xs) | 0;
  const res = fill(new Array(len), 0, len, null);
  const loop = (i_mut, xs_1_mut) => {
    loop: while (true) {
      const i = i_mut, xs_1 = xs_1_mut;
      if (!FSharpList__get_IsEmpty(xs_1)) {
        setItem(res, i, FSharpList__get_Head(xs_1));
        i_mut = i + 1;
        xs_1_mut = FSharpList__get_Tail(xs_1);
        continue loop;
      }
      break;
    }
  };
  loop(0, xs);
  return res;
}
function ofArrayWithTail(xs, tail_1) {
  let res = tail_1;
  for (let i = xs.length - 1; i >= 0; i--) {
    res = FSharpList_Cons_305B8EAC(item(i, xs), res);
  }
  return res;
}
function ofArray(xs) {
  return ofArrayWithTail(xs, FSharpList_get_Empty());
}
function tryFindIndex(f, xs) {
  const loop = (i_mut, xs_1_mut) => {
    loop: while (true) {
      const i = i_mut, xs_1 = xs_1_mut;
      if (FSharpList__get_IsEmpty(xs_1)) {
        return void 0;
      } else if (f(FSharpList__get_Head(xs_1))) {
        return i;
      } else {
        i_mut = i + 1;
        xs_1_mut = FSharpList__get_Tail(xs_1);
        continue loop;
      }
      break;
    }
  };
  return loop(0, xs);
}
function contains(value2, xs, eq) {
  return tryFindIndex((v) => eq.Equals(value2, v), xs) != null;
}

// .build/process-27272/Native.js
import { isAbsolute } from "node:path";
import * as node$003Aprocess from "node:process";
function Native_spawn(file, arguments$, options) {
  if (typeof globalThis.Bun !== "undefined") {
    return spawn(file, arguments$, options);
  } else {
    return spawn_1(file, arguments$, options);
  }
}
function Native_require(condition, message) {
  if (!condition) {
    throw new Exception(message);
  }
}
function Native_get(value2, key) {
  return value2[key];
}
function Native_objectOf(values) {
  return createObj(values);
}
function Native_value(source, name) {
  const property = Object.getOwnPropertyDescriptor(source, name);
  Native_require(property != null && Object.prototype.hasOwnProperty.call(property, "value"), "Expected an own data field: " + name);
  return Native_get(property, "value");
}
function Native_fields(expected, source) {
  Native_require(source !== null && typeof source === "object" && (Object.getPrototypeOf(source) === Object.prototype || Object.getPrototypeOf(source) === null) && Object.getOwnPropertySymbols(source).length === 0, "Expected a plain data object");
  const actual = sort(Object.getOwnPropertyNames(source), {
    Compare: (x, y) => comparePrimitives(x, y) | 0
  });
  Native_require(equalsWith((x_2, y_2) => x_2 === y_2, actual, sort(toArray(expected), {
    Compare: (x_1, y_1) => comparePrimitives(x_1, y_1) | 0
  })), "Unexpected or missing fields");
  for (let idx = 0; idx <= actual.length - 1; idx++) {
    Native_value(source, item(idx, actual));
  }
}
function Native_text(allowEmpty, source) {
  Native_require(typeof source === "string" && source.isWellFormed() && !source.includes("\0"), "Expected valid text without NUL");
  const result = source;
  Native_require(allowEmpty ? true : result.length > 0, "Text must not be empty");
  return result;
}
function Native_integer(maximum, source) {
  Native_require(Number.isSafeInteger(source) && source >= 0 && source <= maximum, "Expected a non-negative safe integer within the limit");
  return source;
}
function Native_failure(operation, error) {
  const message = Native_get(error, "message");
  const code = Native_get(error, "code");
  return Native_objectOf(ofArray([["operation", operation], ["message", message == null ? "Native operation failed" : toString(message)], ["code", code == null ? defaultOf() : toString(code)]]));
}
var StartOptions = class extends Record {
  constructor(Executable, Arguments, Directory, Environment, TailBytes, MaxWriteBytes) {
    super();
    this.Executable = Executable;
    this.Arguments = Arguments;
    this.Directory = Directory;
    this.Environment = Environment;
    this.TailBytes = TailBytes | 0;
    this.MaxWriteBytes = MaxWriteBytes | 0;
  }
};
function StartOptionsModule_read(input) {
  Native_fields(ofArray(["executable", "args", "cwd", "env", "tailBytes", "maxWriteBytes"]), input);
  const path = (name) => {
    const result = Native_text(false, Native_value(input, name));
    Native_require(isAbsolute(result), name + " must be absolute");
    return result;
  };
  const executable = path("executable");
  const directory = path("cwd");
  const arguments$ = Native_value(input, "args");
  Native_require(Array.isArray(arguments$) && Object.getOwnPropertySymbols(arguments$).length === 0, "args must be an array of data strings");
  const count = arguments$.length | 0;
  Native_require(Object.getOwnPropertyNames(arguments$).length === count + 1, "args contains extra properties or holes");
  const arguments$_1 = initialize(count, (index) => Native_text(true, Native_value(arguments$, int32ToString(index))));
  const environment = Native_value(input, "env");
  Native_require(environment !== null && typeof environment === "object" && (Object.getPrototypeOf(environment) === Object.prototype || Object.getPrototypeOf(environment) === null) && Object.getOwnPropertySymbols(environment).length === 0, "env must be a plain data object");
  const pairs = ofArray(map((name_1) => {
    Native_text(false, name_1);
    Native_require(!(name_1.indexOf("=") >= 0), "Environment name contains equals");
    return [name_1, Native_text(true, Native_value(environment, name_1))];
  }, Object.getOwnPropertyNames(environment)));
  const tail = ~~Native_integer(16777216, Native_value(input, "tailBytes")) | 0;
  const write = ~~Native_integer(16777216, Native_value(input, "maxWriteBytes")) | 0;
  Native_require(write > 0, "maxWriteBytes must be positive");
  const platform2 = node$003Aprocess.platform;
  Native_require(platform2 === "linux" ? true : platform2 === "darwin", "POSIX process groups are required");
  return new StartOptions(executable, arguments$_1, directory, Object.fromEntries(toArray(pairs)), tail, write);
}

// .build/process-27272/OutputTail.js
var OutputTail = class {
  constructor(capacity) {
    this.capacity = capacity | 0;
    this.buffer = new Uint8Array(this.capacity);
    this.first = 0;
    this.retained = 0;
    this.observed = 0;
  }
};
function OutputTail_$ctor_Z524259A4(capacity) {
  return new OutputTail(capacity);
}
function OutputTail__get_Observed(_) {
  return _.observed;
}
function OutputTail__get_Retained(_) {
  return _.retained | 0;
}
function OutputTail__Append_Z3F6BC7B1(_, bytes) {
  const count = bytes.length | 0;
  _.observed = _.observed + count;
  if (_.capacity > 0 && count > 0) {
    if (count >= _.capacity) {
      _.buffer.set(bytes.subarray(count - _.capacity, count - _.capacity + _.capacity), 0);
      _.first = 0;
      _.retained = _.capacity | 0;
    } else {
      const next = (_.first + _.retained) % _.capacity | 0;
      const leading = min(count, _.capacity - next) | 0;
      _.buffer.set(bytes.subarray(0, 0 + leading), next);
      _.buffer.set(bytes.subarray(leading, leading + (count - leading)), 0);
      const overflow = max(0, _.retained + count - _.capacity) | 0;
      _.first = (_.first + overflow) % _.capacity | 0;
      _.retained = min(_.capacity, _.retained + count) | 0;
    }
  }
}
function OutputTail__Read_Z3A84238F(_, after, finished) {
  Native_integer(_.observed, after);
  const oldest = _.observed - _.retained;
  const start = max(after, oldest);
  const count = ~~(_.observed - start) | 0;
  const bytes = new Uint8Array(count);
  if (count > 0) {
    const offset = (_.first + ~~(start - oldest)) % _.capacity | 0;
    const leading = min(count, _.capacity - offset) | 0;
    bytes.set(_.buffer.subarray(offset, offset + leading), 0);
    bytes.set(_.buffer.subarray(0, 0 + (count - leading)), leading);
  }
  const patternInput = OutputTail__textView(_, bytes, start, finished);
  return Native_objectOf(ofArray([["from", start], ["to", _.observed], ["discarded", start - after], ["bytes", bytes], ["text", patternInput[0]], ["textFrom", patternInput[1]], ["textTo", patternInput[2]]]));
}
function OutputTail__continuation_244D3E44(this$, value2) {
  if (value2 >= 128) {
    return value2 <= 191;
  } else {
    return false;
  }
}
function OutputTail__textView(this$, bytes, start, finished) {
  let left = 0;
  if (start > 0) {
    while (left < bytes.length && left < 3 && OutputTail__continuation_244D3E44(this$, item(left, bytes))) {
      left = left + 1 | 0;
    }
  }
  let right = bytes.length;
  if (!finished && right > left) {
    let lead = right - 1;
    while (lead > left && OutputTail__continuation_244D3E44(this$, item(lead, bytes))) {
      lead = lead - 1 | 0;
    }
    const head = item(lead, bytes);
    const width = (head >= 194 && head <= 223 ? 2 : head >= 224 && head <= 239 ? 3 : head >= 240 && head <= 244 ? 4 : 1) | 0;
    if (lead + width > right) {
      right = lead | 0;
    }
  }
  return [(() => {
    try {
      return new TextDecoder("utf-8", { fatal: true, ignoreBOM: true }).decode(bytes.slice(left, right - 1 + 1));
    } catch (matchValue) {
      return defaultOf();
    }
  })(), start + left, start + right];
}

// .build/process-27272/ProcessSession.js
import * as node$003Aprocess2 from "node:process";

// node_modules/@fable-org/fable-library-js/MapUtil.js
function addToDict(dict, k, v) {
  if (dict.has(k)) {
    throw new Exception("An item with the same key has already been added. Key: " + k);
  }
  dict.set(k, v);
}

// .build/process-27272/ProcessSession.js
function ProcessSession(input, outputMode) {
  Native_require(outputMode == null ? true : equals(outputMode, "Capture"), "Output mode must be Capture or omitted");
  const capture = !(outputMode == null);
  const options = StartOptionsModule_read(input);
  const stdout = OutputTail_$ctor_Z524259A4(options.TailBytes);
  const stderr = OutputTail_$ctor_Z524259A4(options.TailBytes);
  let child = defaultOf();
  let phase = "Starting";
  let pid = defaultOf();
  let exit = defaultOf();
  let failure = defaultOf();
  let stdinPhase = "Open";
  let started = false;
  let writing = false;
  let busy = false;
  let writeFailed = void 0;
  let nextWait = 0;
  const waits = /* @__PURE__ */ new Map([]);
  let reportStart = (value2) => {
  };
  const startup = new Promise((resolve) => {
    reportStart = resolve;
  });
  let outputReader = void 0;
  let outputDiscarded = false;
  let outputFailure = defaultOf();
  let readingOutput = false;
  let nextChannel = "stdout";
  const guard = (action) => {
    Native_require(!busy, "ProcessSession does not allow reentrant messages");
    busy = true;
    try {
      return action();
    } finally {
      busy = false;
    }
  };
  const rememberFailure = (operation, error) => {
    const detail = Native_failure(operation, error);
    if (Operators_IsNull(failure)) {
      failure = JSON.parse(JSON.stringify(detail));
    }
    return detail;
  };
  const snapshot = () => Native_objectOf(ofArray([["phase", phase], ["pid", pid], ["exit", Operators_IsNull(exit) ? defaultOf() : JSON.parse(JSON.stringify(exit))], ["failure", Operators_IsNull(failure) ? defaultOf() : JSON.parse(JSON.stringify(failure))], ["stdin", stdinPhase], ["stdout", Native_objectOf(ofArray([["observed", OutputTail__get_Observed(stdout)], ["retained", OutputTail__get_Retained(stdout)]]))], ["stderr", Native_objectOf(ofArray([["observed", OutputTail__get_Observed(stderr)], ["retained", OutputTail__get_Retained(stderr)]]))]]));
  const waitResult = (kind) => Native_objectOf(ofArray([["kind", kind], ["state", snapshot()]]));
  const finishOutput = (result) => {
    if (outputReader == null) {
    } else {
      const resolve_1 = outputReader;
      outputReader = void 0;
      resolve_1(result);
    }
  };
  const tryOutput = (channel) => {
    const stream = Native_get(child, channel);
    const count = min(65536, stream.readableLength) | 0;
    if (count <= 0) {
      stream.read(0);
      return void 0;
    } else {
      const received = stream.read(count);
      if (Operators_IsNull(received)) {
        return void 0;
      } else {
        const bytes = copy(received);
        const tail = channel === "stdout" ? stdout : stderr;
        const first = OutputTail__get_Observed(tail);
        OutputTail__Append_Z3F6BC7B1(tail, bytes);
        nextChannel = channel === "stdout" ? "stderr" : "stdout";
        return some(Native_objectOf(ofArray([["kind", "Chunk"], ["channel", channel], ["from", first], ["to", OutputTail__get_Observed(tail)], ["bytes", bytes]])));
      }
    }
  };
  const pumpOutput = () => {
    if (outputReader != null && !readingOutput) {
      readingOutput = true;
      try {
        if (outputDiscarded) {
          finishOutput(Native_objectOf(singleton(["kind", "Discarded"])));
        } else if (!Operators_IsNull(outputFailure)) {
          finishOutput(Native_objectOf(ofArray([["kind", "Failed"], ["error", JSON.parse(JSON.stringify(outputFailure))]])));
        } else if (!Operators_IsNull(child)) {
          const first_1 = tryOutput(nextChannel);
          const second = first_1 == null ? tryOutput(nextChannel === "stdout" ? "stderr" : "stdout") : first_1;
          if (second == null) {
            if (phase === "Closed") {
              finishOutput(waitResult("Closed"));
            }
          } else {
            finishOutput(value(second));
          }
        } else if (phase === "Closed") {
          finishOutput(waitResult("Closed"));
        }
      } finally {
        readingOutput = false;
      }
    }
  };
  const close = () => {
    phase = "Closed";
    if (stdinPhase !== "Failed") {
      stdinPhase = "Closed";
    }
    if (writeFailed == null) {
    } else {
      writeFailed(Native_objectOf(ofArray([["code", "ERR_STREAM_DESTROYED"], ["message", "Process closed during input write"]])));
    }
    const pending = toArray2(waits.values());
    for (let idx = 0; idx <= pending.length - 1; idx++) {
      item(idx, pending)();
    }
    pumpOutput();
  };
  const failedToStart = (error_1) => {
    const detail_1 = rememberFailure("spawn", error_1);
    outputFailure = JSON.parse(JSON.stringify(detail_1));
    reportStart(Native_objectOf(ofArray([["kind", "Failed"], ["error", detail_1]])));
    pumpOutput();
  };
  const streamError = (operation_1, error_3) => {
    const detail_2 = rememberFailure(operation_1, error_3);
    if (operation_1 === "stdin") {
      stdinPhase = "Failed";
      if (writeFailed == null) {
      } else {
        writeFailed(error_3);
      }
    } else {
      outputFailure = JSON.parse(JSON.stringify(detail_2));
      pumpOutput();
    }
  };
  try {
    child = Native_spawn(options.Executable, options.Arguments, Native_objectOf(ofArray([["cwd", options.Directory], ["env", options.Environment], ["shell", false], ["detached", true], ["stdio", ["pipe", "pipe", "pipe"]]])));
    child.on("spawn", ((_arg) => {
      started = true;
      phase = "Running";
      pid = Native_get(child, "pid");
      reportStart(Native_objectOf(ofArray([["kind", "Started"], ["pid", pid]])));
    }));
    child.on("error", ((error_2) => {
      if (!started) {
        failedToStart(error_2);
      } else {
        rememberFailure("process", error_2);
      }
    }));
    child.on("exit", ((code, signal) => {
      exit = Native_objectOf(ofArray([["code", code], ["signal", signal]]));
      phase = "Draining";
    }));
    child.on("close", ((code_1, signal_1) => {
      if (started && Operators_IsNull(exit)) {
        exit = Native_objectOf(ofArray([["code", code_1], ["signal", signal_1]]));
      }
      close();
    }));
    if (capture) {
      Native_get(child, "stdout").on("readable", ((_arg_1) => {
        pumpOutput();
      }));
      Native_get(child, "stderr").on("readable", ((_arg_2) => {
        pumpOutput();
      }));
    } else {
      Native_get(child, "stdout").on("data", ((bytes_1) => {
        OutputTail__Append_Z3F6BC7B1(stdout, bytes_1);
      }));
      Native_get(child, "stderr").on("data", ((bytes_2) => {
        OutputTail__Append_Z3F6BC7B1(stderr, bytes_2);
      }));
    }
    Native_get(child, "stdout").on("error", curry2(streamError)("stdout"));
    Native_get(child, "stderr").on("error", curry2(streamError)("stderr"));
    Native_get(child, "stdin").on("error", curry2(streamError)("stdin"));
    Native_get(child, "stdin").on("finish", ((_arg_3) => {
      if (stdinPhase !== "Failed") {
        stdinPhase = "Closed";
      }
    }));
    Native_get(child, "stdin").on("close", ((_arg_4) => {
      if (stdinPhase !== "Failed") {
        stdinPhase = "Closed";
      }
    }));
  } catch (error_4) {
    failedToStart(error_4);
    close();
  }
  const messages = {
    started() {
      return startup.then((_value) => JSON.parse(JSON.stringify(_value)));
    },
    inspect() {
      return guard(snapshot);
    },
    read(channel_2, after_1) {
      const tupledArg = [channel_2, after_1];
      const channel_1 = tupledArg[0];
      return guard(() => OutputTail__Read_Z3A84238F(channel_1 === "stdout" ? stdout : channel_1 === "stderr" ? stderr : (() => {
        throw new Exception("Channel must be stdout or stderr");
      })(), tupledArg[1], phase === "Closed"));
    },
    write(bytes_4) {
      const input_1 = bytes_4;
      return guard(() => {
        Native_require(input_1 instanceof Uint8Array && !(typeof SharedArrayBuffer !== "undefined" && input_1.buffer instanceof SharedArrayBuffer), "Input must be a non-shared Uint8Array");
        Native_require(input_1.length <= options.MaxWriteBytes, "Input exceeds maxWriteBytes");
        Native_require(phase === "Running" && stdinPhase === "Open", "Input is not open on a running process");
        Native_require(!writing, "An input write is already pending");
        const bytes_3 = copy(input_1);
        writing = true;
        return new Promise((resolve_2) => {
          let finished = false;
          const complete = (error_5) => {
            if (!finished) {
              finished = true;
              writing = false;
              writeFailed = void 0;
              if (error_5 == null) {
                resolve_2(Native_objectOf(ofArray([["kind", "Written"], ["bytes", bytes_3.length]])));
              } else {
                stdinPhase = "Failed";
                resolve_2(Native_objectOf(ofArray([["kind", "Unknown"], ["error", rememberFailure("stdin", error_5)]])));
              }
            }
          };
          writeFailed = complete;
          try {
            Native_get(child, "stdin").write(bytes_3, complete);
          } catch (error_6) {
            complete(error_6);
          }
        });
      });
    },
    endInput() {
      guard(() => {
        if (stdinPhase === "Open" && !Operators_IsNull(child)) {
          stdinPhase = "Ending";
          try {
            Native_get(child, "stdin").end();
          } catch (error_7) {
            streamError("stdin", error_7);
          }
        }
      });
    },
    signal(name_1) {
      const name = name_1;
      return guard(() => {
        Native_require(contains(name, ofArray(["SIGTERM", "SIGKILL", "SIGINT", "SIGHUP"]), {
          Equals: (x, y) => x === y,
          GetHashCode: (x) => stringHash(x) | 0
        }), "Unsupported signal");
        if (phase !== "Running" ? true : Operators_IsNull(pid)) {
          return Native_objectOf(ofArray([["kind", "NotRunning"], ["signal", name]]));
        } else {
          try {
            node$003Aprocess2.kill(-pid, name);
            return Native_objectOf(ofArray([["kind", "Requested"], ["signal", name]]));
          } catch (error_8) {
            return Native_objectOf(ofArray([["kind", "Failed"], ["signal", name], ["error", Native_failure("signal", error_8)]]));
          }
        }
      });
    },
    wait(milliseconds_1) {
      const milliseconds = milliseconds_1;
      return guard(() => {
        Native_integer(2147483647, milliseconds);
        Native_require(milliseconds > 0, "Wait duration must be positive");
        return phase === "Closed" ? Promise.resolve(waitResult("Closed")) : new Promise((resolve_3) => {
          const id = nextWait;
          nextWait = nextWait + 1;
          let finished_1 = false;
          let timer = defaultOf();
          const finish_1 = (kind_1) => {
            if (!finished_1) {
              finished_1 = true;
              clearTimeout(timer);
              waits.delete(id);
              resolve_3(waitResult(kind_1));
            }
          };
          addToDict(waits, id, () => {
            finish_1("Closed");
          });
          timer = setTimeout((() => {
            finish_1("TimedOut");
          }), ~~milliseconds);
        });
      });
    }
  };
  if (capture) {
    messages.takeOutput = (() => guard(() => {
      Native_require(outputReader == null, "An output read is already pending");
      return new Promise((resolve_4) => {
        outputReader = resolve_4;
        pumpOutput();
      });
    }));
    ;
    messages.discardOutput = (() => {
      guard(() => {
        if (!outputDiscarded) {
          outputDiscarded = true;
          if (!Operators_IsNull(child)) {
            Native_get(child, "stdout").destroy();
            Native_get(child, "stderr").destroy();
          }
          pumpOutput();
        }
      });
    });
    ;
  }
  return messages;
}
export {
  ProcessSession
};
