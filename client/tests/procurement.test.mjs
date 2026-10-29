import assert from 'node:assert/strict'
import test from 'node:test'
import { maximumReceiptQuantity, purchaseOrderStatusLabel, receiptLineErrors } from '../src/procurement.ts'

test('A 100-unit order with 60 received allows the remaining 40', () => {
  const line = { orderedQty: 100, receivedQty: 60 }
  assert.equal(maximumReceiptQuantity(line), 40)
  assert.deepEqual(receiptLineErrors(line, { receivedQty: 40, rejectedQty: 0, unitCost: 2 }), {})
  assert.ok(receiptLineErrors(line, { receivedQty: 50, rejectedQty: 0, unitCost: 2 }).receivedQty)
})

test('Configured tolerance is supported without showing negative remaining quantity', () => {
  assert.equal(maximumReceiptQuantity({ orderedQty: 100, receivedQty: 60 }, 5), 45)
  assert.deepEqual(receiptLineErrors({ orderedQty: 100, receivedQty: 60 }, { receivedQty: 45, rejectedQty: 0, unitCost: 2 }, 5), {})
  assert.equal(maximumReceiptQuantity({ orderedQty: 100, receivedQty: 105 }), 0)
})

test('Negative, non-finite and over-precision values produce field-specific errors', () => {
  const line = { orderedQty: 100, receivedQty: 0 }
  assert.ok(receiptLineErrors(line, { receivedQty: -1, rejectedQty: 0, unitCost: 2 }).receivedQty)
  assert.ok(receiptLineErrors(line, { receivedQty: 1, rejectedQty: NaN, unitCost: 2 }).rejectedQty)
  assert.ok(receiptLineErrors(line, { receivedQty: 1, rejectedQty: 0, unitCost: Infinity }).unitCost)
  assert.ok(receiptLineErrors(line, { receivedQty: 0.0001, rejectedQty: 0, unitCost: 2.001 }).receivedQty)
  assert.ok(receiptLineErrors(line, { receivedQty: 1, rejectedQty: 0, unitCost: 2.001 }).unitCost)
  assert.deepEqual(receiptLineErrors(line, { receivedQty: 1.001, rejectedQty: 0.001, unitCost: 2.01 }), {})
})

test('Rejected goods do not consume the accepted quantity allowance', () => {
  assert.deepEqual(receiptLineErrors({ orderedQty: 100, receivedQty: 60 }, { receivedQty: 40, rejectedQty: 2, unitCost: 2 }), {})
})

test('Status codes stay compatible while Azerbaijani labels explain progress', () => {
  assert.equal(purchaseOrderStatusLabel('partially_received'), 'Hissəli qəbul')
  assert.equal(purchaseOrderStatusLabel('received'), 'Tam qəbul edilib')
  assert.equal(purchaseOrderStatusLabel('unknown'), 'unknown')
})
