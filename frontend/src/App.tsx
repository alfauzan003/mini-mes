import { Navigate, Route, Routes } from 'react-router'
import { LoginPage } from '@/features/auth/LoginPage'
import { WorkOrderDetailPage } from '@/features/work-orders/WorkOrderDetailPage'
import { WorkOrderListPage } from '@/features/work-orders/WorkOrderListPage'
import { useAuth } from '@/shared/auth/AuthContext'
import { RequireAuth, RequireRole } from '@/shared/auth/RequireRole'
import { AppLayout } from '@/shared/layout/AppLayout'
import { PlaceholderPage } from '@/shared/ui/PlaceholderPage'

function HomeRedirect() {
  const { user } = useAuth()
  return <Navigate to={user?.role === 'OPERATOR' ? '/station' : '/work-orders'} replace />
}

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<HomeRedirect />} />
          <Route path="work-orders" element={<WorkOrderListPage />} />
          <Route path="work-orders/:id" element={<WorkOrderDetailPage />} />
          <Route
            path="station"
            element={
              <RequireRole roles={['OPERATOR', 'ADMIN']}>
                <PlaceholderPage title="Operator Station" />
              </RequireRole>
            }
          />
          <Route
            path="station/:equipmentCode"
            element={
              <RequireRole roles={['OPERATOR', 'ADMIN']}>
                <PlaceholderPage title="Station" />
              </RequireRole>
            }
          />
          <Route path="lots" element={<PlaceholderPage title="WIP / Lots" />} />
          <Route path="lots/:lotId" element={<PlaceholderPage title="Lot" />} />
          <Route path="carriers" element={<PlaceholderPage title="Carriers" />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Route>
    </Routes>
  )
}
