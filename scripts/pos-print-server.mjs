#!/usr/bin/env node
/**
 * Local POS print agent for automatic thermal receipt printing.
 * Renders Persian receipts as PNG images so ESC/POS thermal drivers print correctly.
 */

import { createServer } from 'node:http'
import { execFile } from 'node:child_process'
import { mkdir, writeFile, unlink } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'
import { renderReceiptPng } from './pos-print/receipt-image.mjs'

const execFileAsync = promisify(execFile)
const ROOT_DIR = join(dirname(fileURLToPath(import.meta.url)), '..')
const PORT = Number(process.env.POS_PRINT_PORT ?? 9101)
const CUSTOMER_PRINTER = process.env.CUSTOMER_PRINTER?.trim() || ''
const BARISTA_PRINTER = process.env.BARISTA_PRINTER?.trim() || CUSTOMER_PRINTER
const RECEIPT_OUTPUT_DIR = process.env.POS_RECEIPT_OUTPUT_DIR?.trim() || join(ROOT_DIR, '.pos-receipts')
const ALLOW_FILE_FALLBACK = process.env.POS_PRINT_FILE_FALLBACK !== '0'

function setCors(res) {
  res.setHeader('Access-Control-Allow-Origin', '*')
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type')
}

function sendJson(res, statusCode, payload) {
  setCors(res)
  res.writeHead(statusCode, { 'Content-Type': 'application/json; charset=utf-8' })
  res.end(JSON.stringify(payload))
}

async function readJsonBody(req) {
  const chunks = []
  for await (const chunk of req) {
    chunks.push(chunk)
  }

  if (!chunks.length) {
    return {}
  }

  return JSON.parse(Buffer.concat(chunks).toString('utf8'))
}

async function savePngFallback(pngBuffer, label) {
  await mkdir(RECEIPT_OUTPUT_DIR, { recursive: true })
  const filePath = join(RECEIPT_OUTPUT_DIR, `${label}-${Date.now()}.png`)
  await writeFile(filePath, pngBuffer)
  return {
    mode: 'file',
    printer: 'file-fallback',
    filePath,
  }
}

async function printPng(pngBuffer, printerName, label) {
  if (!pngBuffer?.length) {
    throw new Error(`${label} receipt image is empty`)
  }

  const filePath = join(tmpdir(), `sugarista-${label}-${Date.now()}.png`)
  await writeFile(filePath, pngBuffer)

  try {
    const args = [
      ...(printerName ? ['-d', printerName] : []),
      '-o',
      'job-sheets=none',
      '-o',
      'fit-to-page',
      filePath,
    ]
    const { stdout, stderr } = await execFileAsync('lp', args)
    await unlink(filePath).catch(() => {})
    return {
      mode: 'printer',
      format: 'png',
      printer: printerName || 'default',
      stdout: stdout?.trim() ?? '',
      stderr: stderr?.trim() ?? '',
    }
  } catch (error) {
    await unlink(filePath).catch(() => {})
    if (!ALLOW_FILE_FALLBACK) {
      throw error
    }

    return savePngFallback(pngBuffer, label)
  }
}

async function printStructuredReceipt(receipt, printerName, label, type) {
  const pngBuffer = await renderReceiptPng(receipt, type)
  return printPng(pngBuffer, printerName, label)
}

async function handlePrint(body) {
  const customerReceipt = body.customer ?? body.customerReceipt
  const baristaReceipt = body.barista ?? body.baristaReceipt

  if (!customerReceipt || !baristaReceipt) {
    throw new Error('Structured customer and barista receipt payloads are required.')
  }

  const customer = await printStructuredReceipt(customerReceipt, CUSTOMER_PRINTER, 'customer', 'customer')
  const barista = await printStructuredReceipt(
    baristaReceipt,
    BARISTA_PRINTER || CUSTOMER_PRINTER,
    'barista',
    'barista',
  )

  return {
    ok: true,
    customer,
    barista,
    timestamp: new Date().toISOString(),
    outputDir: RECEIPT_OUTPUT_DIR,
  }
}

async function handleTestPrint() {
  return handlePrint({
    customer: {
      type: 'customer',
      title: 'رسید مشتری',
      cafeName: 'شوگاریستا',
      address: 'تهران',
      contactPhone: '021-00000000',
      trackingCode: 'TEST-001',
      printedAt: new Date().toLocaleString('fa-IR'),
      guestName: 'مهمان',
      guestPhone: '',
      service: 'داخل سالن',
      table: 'میز ۳',
      note: '',
      paymentMethod: 'نقد',
      totalAmount: 180000,
      lines: [
        {
          name: 'لاته',
          descriptor: 'متوسط · شیر بادام',
          quantity: 1,
          unitPrice: 180000,
          lineTotal: 180000,
        },
      ],
    },
    barista: {
      type: 'barista',
      title: 'رسید باریستا',
      cafeName: 'شوگاریستا',
      trackingCode: 'TEST-001',
      printedAt: new Date().toLocaleString('fa-IR'),
      guestName: 'مهمان',
      service: 'داخل سالن',
      table: 'میز ۳',
      note: '',
      lines: [
        {
          name: 'لاته',
          sizeLabel: 'متوسط',
          quantity: 1,
          modifierLabels: ['شیر بادام'],
          baristaNote: 'شکر کم',
        },
      ],
    },
  })
}

const server = createServer(async (req, res) => {
  setCors(res)

  if (req.method === 'OPTIONS') {
    res.writeHead(204)
    res.end()
    return
  }

  try {
    if (req.method === 'GET' && req.url === '/health') {
      sendJson(res, 200, {
        ok: true,
        port: PORT,
        customerPrinter: CUSTOMER_PRINTER || 'default',
        baristaPrinter: BARISTA_PRINTER || CUSTOMER_PRINTER || 'default',
        format: 'png',
      })
      return
    }

    if (req.method === 'POST' && req.url === '/test') {
      const result = await handleTestPrint()
      sendJson(res, 200, result)
      return
    }

    if (req.method === 'POST' && req.url === '/print') {
      const body = await readJsonBody(req)
      const result = await handlePrint(body)
      sendJson(res, 200, result)
      return
    }

    sendJson(res, 404, { ok: false, error: 'Not found' })
  } catch (error) {
    sendJson(res, 500, {
      ok: false,
      error: error instanceof Error ? error.message : 'Unknown print error',
    })
  }
})

server.listen(PORT, '127.0.0.1', () => {
  console.log(`Sugarista POS print agent listening on http://127.0.0.1:${PORT}`)
  console.log(`Customer printer: ${CUSTOMER_PRINTER || 'default'}`)
  console.log(`Barista printer: ${BARISTA_PRINTER || CUSTOMER_PRINTER || 'default'}`)
  console.log('Receipt format: PNG (Persian-safe thermal printing)')
  console.log('Endpoints: GET /health, POST /print, POST /test')
})
