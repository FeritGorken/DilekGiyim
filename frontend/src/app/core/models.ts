// Backend'deki Contracts/Dtos.cs ile birebir eşleşir.

export interface Category { id: number; name: string; slug: string; }
export interface Color { name: string; hex: string; }

export type StockStatus = 'in' | 'low' | 'out';

export interface ProductListItem {
  id: number;
  slug: string;
  name: string;
  categoryName: string;
  categorySlug: string;
  price: number;
  salePrice: number | null;
  imageUrl: string | null;
  colors: Color[];
  isNew: boolean;
  stockStatus: StockStatus;
  lowStockCount: number | null;
}

export interface ProductImage { url: string; alt: string; }
export interface Variant { id: number; colorName: string; colorHex: string; size: string; available: number; }

export interface ProductDetail {
  id: number;
  slug: string;
  code: string;
  name: string;
  description: string;
  fabricAndCare: string | null;
  categoryName: string;
  categorySlug: string;
  price: number;
  salePrice: number | null;
  isNew: boolean;
  lowStockDisplay: number;
  images: ProductImage[];
  colors: Color[];
  sizes: string[];
  variants: Variant[];
}

export interface PublicSettings {
  freeShippingThreshold: number;
  shippingFee: number;
  maxQuantityPerItem: number;
  showFreeShippingBar: boolean;
}

export interface CartItem { variantId: number; quantity: number; }

export interface CartLine {
  variantId: number;
  productId: number;
  slug: string;
  productName: string;
  imageUrl: string | null;
  colorName: string;
  size: string;
  unitPrice: number;
  originalPrice: number | null;
  quantity: number;
  requestedQuantity: number;
  maxQuantity: number;
  lineTotal: number;
  warning: string | null;
}

export interface CartQuote {
  lines: CartLine[];
  itemCount: number;
  subtotal: number;
  discount: number;
  shipping: number;
  total: number;
  freeShippingThreshold: number;
  freeShippingRemaining: number;
  couponCode: string | null;
  couponApplied: boolean;
  couponMessage: string | null;
}

export interface CheckoutRequest {
  customerName: string;
  email: string;
  phone: string;
  address: string;
  city: string;
  paymentMethod: string;
  installments: number;
  items: CartItem[];
  couponCode: string | null;
  note: string | null;
}

export interface CheckoutResult { orderNo: string; total: number; }

// ---------- Admin ----------

export type OrderStatus = 'New' | 'Preparing' | 'Shipped' | 'Delivered' | 'Cancelled' | 'Returned';

export const ORDER_STATUS_LABELS: Record<OrderStatus, string> = {
  New: 'Yeni',
  Preparing: 'Hazırlanıyor',
  Shipped: 'Kargoda',
  Delivered: 'Teslim edildi',
  Cancelled: 'İptal',
  Returned: 'İade'
};

export interface OrderSummary {
  id: number;
  orderNo: string;
  createdAt: string;
  customerName: string;
  itemCount: number;
  paymentMethod: string;
  total: number;
  status: OrderStatus;
}

export interface OrderLine { productName: string; colorName: string; size: string; sku: string; unitPrice: number; quantity: number; lineTotal: number; }

export interface OrderDetail extends OrderSummary {
  email: string;
  phone: string;
  address: string;
  city: string;
  installments: number;
  subtotal: number;
  discount: number;
  shipping: number;
  couponCode: string | null;
  trackingNo: string | null;
  note: string | null;
  lines: OrderLine[];
}

export type StockRowStatus = 'ok' | 'critical' | 'out';

export interface StockRow {
  variantId: number;
  productId: number;
  productName: string;
  colorName: string;
  colorHex: string;
  size: string;
  sku: string;
  stock: number;
  criticalThreshold: number;
  status: StockRowStatus;
}

export interface StockList {
  summary: { productCount: number; variantCount: number; totalUnits: number; critical: number; out: number; };
  rows: StockRow[];
}

export type StockMovementType = 'Sale' | 'Restock' | 'Return' | 'Cancel' | 'Adjustment';

export const MOVEMENT_LABELS: Record<StockMovementType, string> = {
  Sale: 'Satış',
  Restock: 'Stok girişi',
  Return: 'İade',
  Cancel: 'İptal',
  Adjustment: 'Düzeltme'
};

export interface StockMovement {
  id: number;
  createdAt: string;
  productName: string;
  colorName: string;
  size: string;
  delta: number;
  type: StockMovementType;
  reference: string | null;
  note: string | null;
}

export interface Dashboard {
  revenue: number;
  revenueChangePct: number;
  orderCount: number;
  pendingOrders: number;
  averageBasket: number;
  criticalCount: number;
  outOfStockCount: number;
  daily: { date: string; total: number; }[];
  critical: StockRow[];
  recentOrders: OrderSummary[];
  topProducts: { name: string; quantity: number; }[];
}

export interface AdminProductListItem {
  id: number;
  name: string;
  code: string;
  categoryName: string;
  price: number;
  salePrice: number | null;
  totalStock: number;
  variantCount: number;
  isPublished: boolean;
  imageUrl: string | null;
}

export interface AdminVariant { id: number | null; colorName: string; colorHex: string; size: string; sku: string; stock: number; }

export interface AdminProduct {
  id: number;
  name: string;
  slug: string;
  code: string;
  description: string;
  fabricAndCare: string | null;
  categoryId: number;
  price: number;
  salePrice: number | null;
  cost: number;
  vatRate: number;
  isPublished: boolean;
  isNew: boolean;
  isFeatured: boolean;
  criticalThreshold: number;
  lowStockDisplay: number;
  imageUrls: string[];
  variants: AdminVariant[];
}

export interface BulkPriceRow { productId: number; name: string; current: number; new: number; }

export type DiscountType = 'Percent' | 'Amount';

export interface Coupon {
  id: number;
  code: string;
  type: DiscountType;
  value: number;
  minCartTotal: number;
  usageLimit: number | null;
  usedCount: number;
  startsAt: string | null;
  endsAt: string | null;
  isActive: boolean;
}

export interface Campaign { id: number; name: string; description: string; startsAt: string; endsAt: string; isActive: boolean; }

export interface StoreSettings {
  freeShippingThreshold: number;
  shippingFee: number;
  minOrderTotal: number;
  maxQuantityPerItem: number;
  showFreeShippingBar: boolean;
  abandonedCartReminder: boolean;
  giftWrapOption: boolean;
}
