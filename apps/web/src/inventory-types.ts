export type Unit = { id: string; code: string; decimalPlaces: number };
export type Category = { id: string; name: string };
export type Warehouse = {
  id: string;
  branchId: string;
  name: string;
  location: string;
  active: boolean;
  version: number;
};
export type Item = {
  id: string;
  sku: string;
  name: string;
  kind: string;
  unitId: string;
  categoryId: string | null;
  brand: string;
  classification: string;
  capacityKg: number | null;
  tareWeightKg: number | null;
  serialTracked: boolean;
  reorderLevel: number;
  active: boolean;
  version: number;
};
export type Page<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};
export type Balance = {
  itemId: string;
  warehouseId: string;
  branchId: string;
  bucket: string;
  quantity: number;
  warehouseValue: number;
  averageCost: number;
  sequence: number;
};
export type Movement = {
  id: string;
  sequence: number;
  documentId: string;
  branchId: string;
  warehouseId: string;
  itemId: string;
  bucket: string;
  quantity: number;
  value: number;
  unitCost: number;
  serials: string[];
};
export type StockDocument = {
  id: string;
  branchId: string;
  actorId: string;
  kind: string;
  reason: string;
  postedAt: string;
  businessDate: string;
  reversesId: string | null;
  reversedById: string | null;
  movements: Movement[];
};
export type Asset = {
  id: string;
  serial: string;
  owner: string;
  inspectionReference: string;
  inspectionDue: string | null;
  version: number;
};
