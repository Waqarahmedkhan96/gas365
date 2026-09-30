import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  ArrowRight,
  BarChart3,
  Building2,
  CheckCircle2,
  ClipboardList,
  Cylinder,
  FileText,
  HandCoins,
  History,
  Lock,
  Mail,
  MapPin,
  Menu,
  MessageCircle,
  Package,
  Phone,
  ShieldCheck,
  ShoppingCart,
  Truck,
  UserCog,
  Users,
  Wallet,
  X,
  XCircle,
  type LucideIcon,
} from 'lucide-react';
import { BrandMark } from './components/icons';
import { money } from './report-types';
import { hasContact, siteConfig } from './site-config';
import type { BillingPeriod, Plan } from './platform-types';

const problems: {
  icon: LucideIcon;
  before: string;
  after: string;
  text: string;
}[] = [
  {
    icon: FileText,
    before: 'Registers and loose slips',
    after: 'Every sale recorded once',
    text: 'Sales, payments and receipts are saved as you work, with a printable receipt for the customer.',
  },
  {
    icon: HandCoins,
    before: 'Udhaar you cannot trace',
    after: 'A clear credit book',
    text: 'See what each customer owes, how old it is and every payment against it.',
  },
  {
    icon: Cylinder,
    before: 'Cylinders that go missing',
    after: 'Cylinders you can account for',
    text: 'Filled, empty and borrowed cylinders are tracked per branch, including deposits.',
  },
  {
    icon: BarChart3,
    before: 'No idea of the day’s profit',
    after: 'Daily figures you can trust',
    text: 'Revenue, profit, expenses and cash are calculated from posted records, not guesses.',
  },
];

const features: { icon: LucideIcon; title: string; points: string[] }[] = [
  {
    icon: ShoppingCart,
    title: 'Sales and receipts',
    points: [
      'Refills by weight, full cylinders and exchanges',
      'Discounts with permission',
      'PDF receipts',
    ],
  },
  {
    icon: Cylinder,
    title: 'Cylinders and deposits',
    points: [
      'Borrowed cylinders with deposits',
      'Returns, usage fees and refunds',
      'Filling loss recorded per cylinder',
    ],
  },
  {
    icon: Package,
    title: 'Stock and warehouses',
    points: [
      'Balances per warehouse and state',
      'Transfers between stores',
      'Reorder alerts',
    ],
  },
  {
    icon: HandCoins,
    title: 'Udhaar and credit',
    points: [
      'Customer and supplier balances',
      'Aging by invoice date',
      'Overdue alerts',
    ],
  },
  {
    icon: Wallet,
    title: 'Cash register',
    points: [
      'Open and close the drawer daily',
      'Expected versus counted cash',
      'Expenses, loans and owner funds',
    ],
  },
  {
    icon: Truck,
    title: 'Purchases and suppliers',
    points: [
      'Purchase drafts and posting',
      'Transport and loading costs',
      'Supplier payments',
    ],
  },
  {
    icon: BarChart3,
    title: 'Reports',
    points: [
      'Daily sales, profit and stock',
      'Cash reconciliation',
      'CSV, Excel and PDF export',
    ],
  },
  {
    icon: UserCog,
    title: 'Team and branches',
    points: [
      'Roles and permissions',
      'Branch-level access',
      'Full activity history',
    ],
  },
];

const steps = [
  {
    title: 'Create your account',
    text: 'Sign up with your email. The 14-day trial starts straight away.',
  },
  {
    title: 'Set up your business',
    text: 'Add your branch, store and cash drawer with the guided setup.',
  },
  {
    title: 'Add stock and customers',
    text: 'Enter your items and opening stock, or import them from Excel.',
  },
  {
    title: 'Start selling',
    text: 'Record sales, collect udhaar and close the register every evening.',
  },
];

const security: { icon: LucideIcon; title: string; text: string }[] = [
  {
    icon: Lock,
    title: 'Encrypted connections',
    text: 'All traffic uses HTTPS, and sign-in sessions are protected cookies.',
  },
  {
    icon: ShieldCheck,
    title: 'Role-based access',
    text: 'Decide exactly what each team member can see and do.',
  },
  {
    icon: Building2,
    title: 'Separate branches',
    text: 'Staff only see the branches they are assigned to.',
  },
  {
    icon: History,
    title: 'Activity history',
    text: 'Every important change is recorded with who made it and when.',
  },
  {
    icon: ClipboardList,
    title: 'Corrections, not deletions',
    text: 'Posted sales and payments are reversed with a reason, never silently edited.',
  },
  {
    icon: Users,
    title: 'Support only with permission',
    text: 'Our team can view your data only when you allow it, read-only and for a limited time.',
  },
];

const faqs = [
  {
    q: 'Is Gas365 suitable for a single small shop?',
    a: 'Yes. Start with one branch and one drawer. Add branches, stores and team members when you grow.',
  },
  {
    q: 'Can I manage more than one branch?',
    a: 'Yes. Each branch has its own stock, cash and sales, and you can compare branches in reports.',
  },
  {
    q: 'How does the free trial work?',
    a: 'You get 14 days of full access. After that, choose a plan to keep adding records. Your data stays readable and exportable.',
  },
  {
    q: 'Can I bring my existing customers and stock?',
    a: 'Yes. Import customers, suppliers, items and opening balances from Excel or CSV.',
  },
  {
    q: 'How do I pay?',
    a: 'Choose a monthly or yearly plan in the app and pay by bank transfer. Yearly billing gives you two months free.',
  },
];

function Pricing() {
  const [period, setPeriod] = useState<BillingPeriod>('Monthly');
  const plans = useQuery({
    queryKey: ['public-plans'],
    queryFn: async () => {
      const response = await fetch('/api/public/plans', {
        credentials: 'same-origin',
      });
      if (!response.ok) throw new Error('Plans are unavailable right now.');
      return (await response.json()) as Plan[];
    },
    retry: false,
  });
  return (
    <section
      id="pricing"
      className="mk-section"
      aria-labelledby="pricing-title"
    >
      <div className="mk-heading">
        <p className="mk-eyebrow">Pricing</p>
        <h2 id="pricing-title">Simple plans in rupees</h2>
        <p>
          Every plan starts with a 14-day free trial. Pay monthly, or yearly and
          get two months free.
        </p>
        <div
          className="billing-toggle"
          role="radiogroup"
          aria-label="Billing period"
        >
          {(['Monthly', 'Yearly'] as const).map((value) => (
            <button
              key={value}
              type="button"
              role="radio"
              aria-checked={period === value}
              onClick={() => setPeriod(value)}
            >
              {value}
              {value === 'Yearly' && (
                <span className="billing-badge">2 months free</span>
              )}
            </button>
          ))}
        </div>
      </div>
      {plans.isPending && <p role="status">Loading plans…</p>}
      {plans.error && (
        <p role="alert" className="mk-center">
          {plans.error.message}
        </p>
      )}
      {plans.data && (
        <ul className="plan-grid mk-plans">
          {plans.data.map((p) => (
            <li key={p.id} className="plan-card">
              <h3>{p.name}</h3>
              <p className="plan-price">
                {p.monthlyPrice === null
                  ? 'Price on request'
                  : period === 'Yearly' && p.yearlyPrice !== null
                    ? `${money(p.yearlyPrice)} per year`
                    : `${money(p.monthlyPrice)} per month`}
              </p>
              {period === 'Yearly' && p.yearlySaving !== null && (
                <p className="plan-saving">You save {money(p.yearlySaving)}</p>
              )}
              <ul className="plan-features">
                <li>
                  {p.maxBranches} {p.maxBranches === 1 ? 'branch' : 'branches'}
                </li>
                <li>
                  {p.maxUsers} {p.maxUsers === 1 ? 'user' : 'users'}
                </li>
                <li>All features included</li>
              </ul>
              <Link className="mk-button" to="/register">
                Start free trial
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

export function MarketingPage() {
  const [menu, setMenu] = useState(false);
  useEffect(() => {
    document.title = 'Gas365 · LPG business software for Pakistan';
  }, []);
  const nav = [
    ['#features', 'Features'],
    ['#how', 'How it works'],
    ['#pricing', 'Pricing'],
    ['#faq', 'FAQ'],
    ...(hasContact() ? [['#contact', 'Contact']] : []),
  ];
  return (
    <div className="marketing">
      <a className="skip-link" href="#main">
        Skip to main content
      </a>
      <header className="mk-header">
        <Link to="/" className="mk-brand" aria-label="Gas365 home">
          <BrandMark size={34} />
          <span aria-hidden="true">
            Gas<span>365</span>
          </span>
        </Link>
        <button
          type="button"
          className="mk-menu-button"
          aria-expanded={menu}
          aria-controls="mk-nav"
          aria-label={menu ? 'Close menu' : 'Open menu'}
          onClick={() => setMenu((m) => !m)}
        >
          {menu ? (
            <X aria-hidden="true" size={22} />
          ) : (
            <Menu aria-hidden="true" size={22} />
          )}
        </button>
        <nav
          id="mk-nav"
          aria-label="Website"
          className={menu ? 'mk-nav open' : 'mk-nav'}
        >
          <ul>
            {nav.map(([href, label]) => (
              <li key={href}>
                <a href={href} onClick={() => setMenu(false)}>
                  {label}
                </a>
              </li>
            ))}
          </ul>
          <div className="mk-nav-actions">
            <Link className="mk-button outline" to="/login">
              Sign in
            </Link>
            <Link className="mk-button" to="/register">
              Start free trial
            </Link>
          </div>
        </nav>
      </header>
      <main id="main" className="mk-main">
        <section className="mk-hero" aria-labelledby="hero-title">
          <div className="mk-hero-copy">
            <p className="mk-pill">Built for LPG retailers and distributors</p>
            <h1 id="hero-title">Run your LPG business from one clear screen</h1>
            <p className="mk-lead">
              Sales, cylinders, stock, udhaar and daily cash closing for gas
              shops and distributors across Pakistan. Every rupee and every
              cylinder accounted for.
            </p>
            <div className="mk-cta">
              <Link className="mk-button large" to="/register">
                Start 14-day free trial
                <ArrowRight aria-hidden="true" size={18} />
              </Link>
              <a className="mk-button large outline" href="#pricing">
                See pricing
              </a>
            </div>
            <ul className="mk-trust">
              <li>
                <CheckCircle2 aria-hidden="true" size={18} /> No card needed
              </li>
              <li>
                <CheckCircle2 aria-hidden="true" size={18} /> Prices in PKR
              </li>
              <li>
                <CheckCircle2 aria-hidden="true" size={18} /> Works on phone and
                computer
              </li>
            </ul>
          </div>
          <figure className="mk-preview">
            <figcaption className="sr-only">
              Illustration of the Gas365 overview with sample figures
            </figcaption>
            <div className="mk-preview-bar" aria-hidden="true">
              <span />
              <span />
              <span />
              <strong>Overview</strong>
            </div>
            <div className="mk-preview-body" aria-hidden="true">
              <div className="mk-preview-cards">
                <div>
                  <small>Net revenue</small>
                  <strong>PKR 84,500</strong>
                </div>
                <div>
                  <small>Cash in drawer</small>
                  <strong>PKR 48,250</strong>
                </div>
                <div>
                  <small>Udhaar</small>
                  <strong>PKR 12,300</strong>
                </div>
              </div>
              <div className="mk-preview-chart">
                {[40, 62, 48, 75, 58, 88, 70].map((h, i) => (
                  <span key={i} className={`bar-${h}`} />
                ))}
              </div>
              <ul className="mk-preview-list">
                <li>
                  <span>INV-021 · Hotel customer</span>
                  <em>Paid</em>
                </li>
                <li>
                  <span>INV-022 · Walk-in refill</span>
                  <em>Paid</em>
                </li>
                <li>
                  <span>INV-023 · Restaurant</span>
                  <em className="due">Udhaar</em>
                </li>
              </ul>
            </div>
            <p className="mk-sample">Sample data for illustration</p>
          </figure>
        </section>

        <section className="mk-section" aria-labelledby="problems-title">
          <div className="mk-heading">
            <p className="mk-eyebrow">Why Gas365</p>
            <h2 id="problems-title">From paper registers to clear records</h2>
            <p>
              The everyday problems of an LPG business, solved in one place.
            </p>
          </div>
          <ul className="mk-grid four">
            {problems.map(({ icon: Icon, before, after, text }) => (
              <li key={after} className="mk-card">
                <span className="mk-icon" aria-hidden="true">
                  <Icon size={22} />
                </span>
                <p className="mk-before">
                  <XCircle aria-hidden="true" size={16} /> {before}
                </p>
                <h3>
                  <CheckCircle2 aria-hidden="true" size={18} /> {after}
                </h3>
                <p>{text}</p>
              </li>
            ))}
          </ul>
        </section>

        <section
          id="features"
          className="mk-section alt"
          aria-labelledby="features-title"
        >
          <div className="mk-heading">
            <p className="mk-eyebrow">Features</p>
            <h2 id="features-title">Everything an LPG business needs</h2>
            <p>Made for how gas shops and distributors actually work.</p>
          </div>
          <ul className="mk-grid four">
            {features.map(({ icon: Icon, title, points }) => (
              <li key={title} className="mk-card">
                <span className="mk-icon" aria-hidden="true">
                  <Icon size={22} />
                </span>
                <h3>{title}</h3>
                <ul className="mk-points">
                  {points.map((p) => (
                    <li key={p}>
                      <CheckCircle2 aria-hidden="true" size={16} /> {p}
                    </li>
                  ))}
                </ul>
              </li>
            ))}
          </ul>
        </section>

        <section id="how" className="mk-section" aria-labelledby="how-title">
          <div className="mk-heading">
            <p className="mk-eyebrow">How it works</p>
            <h2 id="how-title">Up and running in four steps</h2>
          </div>
          <ol className="mk-steps">
            {steps.map((s, i) => (
              <li key={s.title}>
                <span className="mk-step-number" aria-hidden="true">
                  {String(i + 1).padStart(2, '0')}
                </span>
                <h3>{s.title}</h3>
                <p>{s.text}</p>
              </li>
            ))}
          </ol>
          <div className="mk-center">
            <Link className="mk-button large" to="/register">
              Create your account
            </Link>
          </div>
        </section>

        <Pricing />

        <section className="mk-section alt" aria-labelledby="security-title">
          <div className="mk-heading">
            <p className="mk-eyebrow">Security</p>
            <h2 id="security-title">Your business data stays yours</h2>
          </div>
          <ul className="mk-grid three">
            {security.map(({ icon: Icon, title, text }) => (
              <li key={title} className="mk-card compact">
                <span className="mk-icon" aria-hidden="true">
                  <Icon size={20} />
                </span>
                <h3>{title}</h3>
                <p>{text}</p>
              </li>
            ))}
          </ul>
        </section>

        <section id="faq" className="mk-section" aria-labelledby="faq-title">
          <div className="mk-heading">
            <p className="mk-eyebrow">Questions</p>
            <h2 id="faq-title">Frequently asked questions</h2>
          </div>
          <div className="mk-faq">
            {faqs.map((f) => (
              <details key={f.q}>
                <summary>{f.q}</summary>
                <p>{f.a}</p>
              </details>
            ))}
          </div>
        </section>

        <section className="mk-final" aria-labelledby="final-title">
          <h2 id="final-title">Ready to go digital?</h2>
          <p>
            Start your 14-day free trial today. No card needed, and your data is
            always yours to export.
          </p>
          <div className="mk-cta center">
            <Link className="mk-button large light" to="/register">
              Start free trial <ArrowRight aria-hidden="true" size={18} />
            </Link>
            {siteConfig.whatsappNumber && (
              <a
                className="mk-button large whatsapp"
                href={`https://wa.me/${siteConfig.whatsappNumber}`}
                rel="noopener noreferrer"
                target="_blank"
              >
                <MessageCircle aria-hidden="true" size={18} /> WhatsApp us
              </a>
            )}
          </div>
        </section>
      </main>
      <footer className="mk-footer">
        <div className="mk-footer-grid">
          <div>
            <Link to="/" className="mk-brand light" aria-label="Gas365 home">
              <BrandMark size={30} />
              <span aria-hidden="true">
                Gas<span>365</span>
              </span>
            </Link>
            <p>
              LPG business software for retailers and distributors in Pakistan.
            </p>
          </div>
          <nav aria-label="Footer">
            <h2>Product</h2>
            <ul>
              <li>
                <a href="#features">Features</a>
              </li>
              <li>
                <a href="#pricing">Pricing</a>
              </li>
              <li>
                <Link to="/login">Sign in</Link>
              </li>
              <li>
                <Link to="/status">System status</Link>
              </li>
            </ul>
          </nav>
          {hasContact() && (
            <div id="contact">
              <h2>Contact</h2>
              <ul>
                {siteConfig.email && (
                  <li>
                    <Mail aria-hidden="true" size={16} />
                    <a href={`mailto:${siteConfig.email}`}>
                      {siteConfig.email}
                    </a>
                  </li>
                )}
                {siteConfig.phoneDisplay && (
                  <li>
                    <Phone aria-hidden="true" size={16} />
                    {siteConfig.phoneDisplay}
                  </li>
                )}
                {siteConfig.city && (
                  <li>
                    <MapPin aria-hidden="true" size={16} />
                    {siteConfig.city}
                  </li>
                )}
              </ul>
            </div>
          )}
        </div>
        <p className="mk-copyright">
          © {new Date().getFullYear()} Gas365. All rights reserved.
        </p>
      </footer>
    </div>
  );
}
