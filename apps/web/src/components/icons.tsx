import {
  Bell,
  Building2,
  ChartColumn,
  CreditCard,
  FileUp,
  Globe,
  HandCoins,
  History,
  Landmark,
  Layers,
  LayoutDashboard,
  MonitorSmartphone,
  Package,
  Receipt,
  Settings,
  ShieldCheck,
  ShoppingCart,
  Store,
  Tags,
  Truck,
  UserCog,
  Users,
  Wallet,
  type LucideIcon,
} from 'lucide-react';

// One icon per navigation page; pages refer to these keys from navigation.ts.
const pageIcons: Record<string, LucideIcon> = {
  overview: LayoutDashboard,
  notifications: Bell,
  sales: ShoppingCart,
  customers: Users,
  prices: Tags,
  inventory: Package,
  purchases: Truck,
  suppliers: Store,
  'cash-register': Wallet,
  expenses: Receipt,
  udhaar: HandCoins,
  loans: Landmark,
  reports: ChartColumn,
  audit: History,
  branches: Building2,
  team: UserCog,
  roles: ShieldCheck,
  settings: Settings,
  imports: FileUp,
  subscription: CreditCard,
  sessions: MonitorSmartphone,
  platform: Globe,
  showcase: Layers,
};

export function PageIcon({
  name,
  size = 18,
  className,
}: {
  name: string;
  size?: number;
  className?: string;
}) {
  const Icon = pageIcons[name] || LayoutDashboard;
  return (
    <Icon
      aria-hidden="true"
      size={size}
      strokeWidth={1.8}
      className={className}
    />
  );
}

// Original Gas365 mark: a flame inside a rounded cylinder outline.
export function BrandMark({ size = 32 }: { size?: number }) {
  return (
    <svg
      aria-hidden="true"
      width={size}
      height={size}
      viewBox="0 0 32 32"
      className="brand-mark"
    >
      <rect width="32" height="32" rx="9" fill="#1F6FA8" />
      <rect
        x="9"
        y="6.5"
        width="14"
        height="19"
        rx="5"
        fill="none"
        stroke="#FFFFFF"
        strokeWidth="1.8"
      />
      <path
        d="M16 11.2c1.9 2 3 3.6 3 5.4a3 3 0 0 1-6 0c0-1.2.6-2.2 1.5-3.2.1 1 .6 1.7 1.3 2 .1-1.6.1-2.8.2-4.2Z"
        fill="#F3A712"
      />
    </svg>
  );
}
