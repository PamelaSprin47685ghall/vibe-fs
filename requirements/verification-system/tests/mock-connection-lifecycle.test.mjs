import assert from 'node:assert/strict'
import http from 'node:http'
import test from 'node:test'
import { startHttpServer, stopHttpServer } from './e2e/support/strict-mock-server.js'

test('WHAT[VERIFICATION-SYSTEM-014] MOCK_014_mock_server_disables_idle_keep_alive_reuse', async () => {
  const { server, url } = await startHttpServer((req, res) => sendOk(res))
  try {
    // The race class is eliminated at the source: the server never advertises
    // an idle keep-alive window for the host to race against.
    assert.equal(server.keepAliveTimeout, 0)
  } finally {
    await stopHttpServer(server)
    void url
  }
})

test('WHAT[VERIFICATION-SYSTEM-014] MOCK_014_every_response_declares_connection_close', async () => {
  const { server, port } = await startHttpServer((req, res) => sendOk(res))
  try {
    const headers = await getHeaders(port, '/v1/chat/completions')
    assert.equal(headers.connection.toLowerCase(), 'close')
  } finally {
    await stopHttpServer(server)
  }
})

function sendOk(res) {
  res.writeHead(200, { 'Content-Type': 'application/json' })
  res.end('{"ok":true}')
}

function getHeaders(port, path) {
  return new Promise((resolve, reject) => {
    const req = http.get({ host: '127.0.0.1', port, path }, (res) => {
      res.resume()
      res.on('end', () => resolve(res.headers))
    })
    req.on('error', reject)
  })
}
