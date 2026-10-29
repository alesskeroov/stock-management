// Dev-only visual fixture. All fetch calls are mocked; no API or database is contacted.
import { useEffect } from 'react'
import { createRoot } from 'react-dom/client'
import App from '../src/App'
import '../src/index.css'

const item = { id: 'line-1', productId: 'product-1', productName: 'A4 kağız', sku: 'OFF-PAP-001', orderedQty: 100, receivedQty: 60, remainingQty: 40, unitPrice: 8.4, lineTotal: 840 }
const order = { id: 'order-1', poNumber: 'PO-2026-9001', supplierId: 'supplier-1', supplierName: 'Demo ofis təchizatçısı', supplierPhone: '', supplierEmail: '', supplierAddress: '', supplierTaxId: '', warehouseId: 'warehouse-1', warehouseName: 'Əsas anbar', status: 'partially_received', orderDate: '2026-10-29', expectedDate: '2026-10-30', currency: 'AZN', totalAmount: 840, note: '', createdBy: 'preview-user', items: [item], goodsReceipts: [], overReceiptTolerancePercent: 0 }
const product = { id: item.productId, name: item.productName, sku: item.sku, barcode: '', description: '', categoryId: 'category-1', unitPrice: 8.4, unitOfMeasure: 'paket', isActive: true, quantity: 60, reorderLevel: 10, createdAt: '2026-10-29T08:00:00Z', updatedAt: '2026-10-29T08:00:00Z' }
const receipt = { id: 'receipt-1', grnNumber: 'GRN-2026-9001', purchaseOrderId: order.id, poNumber: order.poNumber, warehouseName: order.warehouseName, receivedDate: '2026-10-29', receivedBy: 'preview-user', receivedByName: 'Demo istifadəçi', note: null, reversedAt: null, reverseReason: null, items: [{ id: 'receipt-line-1', productName: item.productName, receivedQty: 60, rejectedQty: 0, unitCost: 8.4, batchNo: null, expiryDate: null }] }
const page = (items: unknown[]) => ({ items, currentPage: 1, pageSize: 10, totalCount: items.length, totalPages: 1, hasNext: false, hasPrevious: false })
sessionStorage.setItem('stockroom.auth', JSON.stringify({ accessToken: 'preview-only', refreshToken: 'preview-only', accessTokenExpiresAtUtc: '2099-01-01', user: { id: 'preview-user', userName: 'preview', email: 'preview@example.invalid', firstName: 'Demo', lastName: 'İstifadəçi', roles: ['Admin'] } }))
window.fetch = async (input, init) => {
  const path = new URL(String(input), location.origin).pathname
  if (init?.method && init.method !== 'GET') return Response.json({ error: 'Sınaq önbaxışında yazma əməliyyatı söndürülüb.' }, { status: 409 })
  if (path === '/api/products') return Response.json(page([product]))
  if (path === '/api/categories/options') return Response.json([{ id: 'category-1', name: 'Ofis ləvazimatları', description: '' }])
  if (path === '/api/inventory/warehouses') return Response.json([{ id: 'warehouse-1', name: order.warehouseName }])
  if (path === '/api/notifications/unread-count') return Response.json(0)
  if (path === '/api/purchase-orders/options/suppliers') return Response.json([{ id: order.supplierId, name: order.supplierName }])
  if (path === '/api/purchase-orders/options') return Response.json({ suppliers: [{ id: order.supplierId, name: order.supplierName }], warehouses: [{ id: order.warehouseId, name: order.warehouseName }], products: [{ id: item.productId, name: item.productName, sku: item.sku }] })
  if (path === '/api/purchase-orders') return Response.json(page([order]))
  if (path === '/api/goods-receipts') return Response.json(page([receipt]))
  return Response.json([])
}

export default function Preview() {
  const toggleDark = (checked: boolean) => {
    // Activate the real dark CSS rules for visual QA without changing OS/browser settings.
    for (const sheet of Array.from(document.styleSheets)) {
      for (const rule of Array.from(sheet.cssRules)) {
        if (rule instanceof CSSMediaRule && (rule.conditionText.includes('prefers-color-scheme: dark') || rule.media.mediaText === 'screen and (min-width: 0px)' || rule.media.mediaText === 'screen and (min-width: 999999px)'))
          rule.media.mediaText = checked ? 'screen and (min-width: 0px)' : 'screen and (min-width: 999999px)'
      }
    }
  }
  useEffect(() => { toggleDark(false) }, [])
  return <><label style={{ display: 'block', padding: 8, background: '#fafafa', color: '#1d1d1f', fontSize: 14 }}>Yalnız sınaq məlumatı · API-yə yazılmır <input type="checkbox" onChange={(event) => toggleDark(event.target.checked)}/> Tünd rejim önbaxışı</label><App/></>
}
createRoot(document.getElementById('root')!).render(<Preview/>)
