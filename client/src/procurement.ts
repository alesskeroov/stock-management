export function purchaseOrderStatusLabel(status: string): string {
  const labels: Record<string, string> = {
    draft: 'Qaralama', pending_approval: 'Təsdiq gözləyir', approved: 'Təsdiqlənib',
    partially_received: 'Hissəli qəbul', received: 'Tam qəbul edilib', cancelled: 'Ləğv edilib',
  }
  return labels[status] ?? status
}

type ReceiptLine = { orderedQty: number; receivedQty: number }
type ReceiptDraft = { receivedQty: number; rejectedQty: number; unitCost: number }

export function maximumReceiptQuantity(line: ReceiptLine, tolerancePercent = 0) {
  return Math.max(0, line.orderedQty - line.receivedQty + line.orderedQty * tolerancePercent / 100)
}

export function receiptLineErrors(line: ReceiptLine, draft: ReceiptDraft, tolerancePercent = 0) {
  const errors: Partial<Record<keyof ReceiptDraft, string>> = {}
  for (const key of ['receivedQty', 'rejectedQty', 'unitCost'] as const) {
    const value = draft[key]
    const scale = key === 'unitCost' ? 2 : 3
    if (!Number.isFinite(value) || value < 0)
      errors[key] = key === 'unitCost' ? 'Maya dəyəri sıfır və ya daha böyük olmalıdır.' : 'Miqdar sıfır və ya daha böyük olmalıdır.'
    else if (value !== Number(value.toFixed(scale))) errors[key] = `Ən çox ${scale} onluq rəqəm daxil edin.`
  }
  const maximum = maximumReceiptQuantity(line, tolerancePercent)
  if (!errors.receivedQty && draft.receivedQty > maximum + 1e-9)
    errors.receivedQty = `Qəbul miqdarı ${maximum.toLocaleString('az-AZ', { maximumFractionDigits: 3 })} həddini keçə bilməz.`
  return errors
}
