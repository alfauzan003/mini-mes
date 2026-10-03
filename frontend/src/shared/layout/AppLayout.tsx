import { ClipboardList, Cog, Factory, LayoutDashboard, LogOut, Package, ShieldCheck, Siren, Waypoints, type LucideIcon } from 'lucide-react'
import { NavLink, Outlet } from 'react-router'
import { useAlarms } from '@/features/alarms/api'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { Role } from '@/shared/api/types'
import { useAuth } from '@/shared/auth/AuthContext'
import { ConnectionIndicator } from '@/shared/realtime/ConnectionIndicator'

interface NavItem {
  to: string
  label: string
  icon: LucideIcon
  roles: Role[] | 'all'
}

const NAV_ITEMS: NavItem[] = [
  { to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard, roles: 'all' },
  { to: '/equipment', label: 'Equipment', icon: Cog, roles: 'all' },
  { to: '/alarms', label: 'Alarms', icon: Siren, roles: 'all' },
  { to: '/work-orders', label: 'Work Orders', icon: ClipboardList, roles: 'all' },
  { to: '/station', label: 'Operator Station', icon: Factory, roles: ['OPERATOR', 'ADMIN'] },
  { to: '/quality', label: 'Quality', icon: ShieldCheck, roles: 'all' },
  { to: '/lots', label: 'WIP / Lots', icon: Waypoints, roles: 'all' },
  { to: '/carriers', label: 'Carriers', icon: Package, roles: 'all' },
]

function ActiveAlarmBadge() {
  const alarms = useAlarms({ active: true })
  const count = alarms.data?.length ?? 0
  if (count === 0) return null
  return (
    <span className="ml-auto min-w-5 rounded-full bg-red-600 px-1.5 text-center text-xs font-semibold text-white">
      <span aria-hidden="true">{count}</span>
      <span className="sr-only">{`${count} active ${count === 1 ? 'alarm' : 'alarms'}`}</span>
    </span>
  )
}

export function AppLayout() {
  const { user, logout } = useAuth()
  const items = NAV_ITEMS.filter((item) => item.roles === 'all' || (user && item.roles.includes(user.role)))

  return (
    <div className="flex min-h-screen bg-muted/30">
      <aside className="w-56 shrink-0 border-r bg-card">
        <div className="flex h-14 items-center border-b px-4 text-lg font-semibold">Mini MES</div>
        <nav aria-label="Main" className="flex flex-col gap-1 p-2">
          {items.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium text-muted-foreground hover:bg-muted hover:text-foreground',
                  isActive && 'bg-muted text-foreground',
                )
              }
            >
              <Icon className="size-4" />
              {label}
              {to === '/alarms' && <ActiveAlarmBadge />}
            </NavLink>
          ))}
        </nav>
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 items-center justify-end gap-3 border-b bg-card px-6">
          <ConnectionIndicator />
          {user && (
            <div className="text-right text-sm leading-tight">
              <div className="font-medium">{user.displayName}</div>
              <div className="text-xs text-muted-foreground">{user.role}</div>
            </div>
          )}
          <Button variant="outline" size="sm" onClick={logout}>
            <LogOut />
            Logout
          </Button>
        </header>
        <main className="flex-1 p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
