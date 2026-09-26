export type Supplier = {
  id: string;
  name: string;
  phone: string;
  address: string;
  active: boolean;
  version: number;
};
export type PurchaseLine = {
  itemId: string;
  sku: string;
  name: string;
  unit: string;
  warehouseId: string;
  warehouse: string;
  bucket: string;
  quantity: number;
  price: number;
  weightKg: number;
  serials: string[];
  baseValue: number;
  landedCost: number;
  value: number;
  receiptUnitCost: number;
};
export type PurchaseEntry = {
  id: string;
  kind: string;
  payable: number;
  cash: number;
  reason: string;
  actorId: string;
  postedAt: string;
  businessDate: string;
  reversesId: string | null;
};
export type Purchase = {
  id: string;
  branchId: string;
  supplierId: string;
  supplierName: string;
  status: string;
  version: number;
  total: number;
  paid: number;
  outstanding: number;
  separateCosts: boolean;
  costsPaid: number;
  costsOutstanding: number;
  snapshot: {
    reference: string;
    notes: string;
    allocationRule: string;
    landedCharges: number;
    lines: PurchaseLine[];
  };
  stockDocumentId: string | null;
  correctsId: string | null;
  entries: PurchaseEntry[];
};
export type SupplierProfile = {
  supplier: Supplier;
  balance: number;
  ledger: {
    items: { purchaseId: string; entry: PurchaseEntry }[];
    total: number;
    page: number;
    pageSize: number;
  };
};
