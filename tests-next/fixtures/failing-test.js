export async function failsImmediately() {
  throw new Error('expected fast failure');
}
