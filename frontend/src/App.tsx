import { Navigate, Route, Routes } from 'react-router'
import { LoginPage } from '@/features/auth/LoginPage'
import { CarrierListPage } from '@/features/carriers/CarrierListPage'
import { LotDetailPage } from '@/features/lots/LotDetailPage'
import { LotListPage } from '@/features/lots/LotListPage'
import { StationPage } from '@/features/operator/StationPage'
import { StationPickerPage } from '@/features/operator/StationPickerPage'
import { WorkOrderDetailPage } from '@/features/work-orders/WorkOrderDetailPage'
import { WorkOrderListPage } from '@/features/work-orders/WorkOrderListPage'
import { useAuth } from '@/shared/auth/AuthContext'
import { RequireAuth, RequireRole } from '@/shared/auth/RequireRole'
import { AppLayout } from '@/shared/layout/AppLayout'

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
                <StationPickerPage />
              </RequireRole>
            }
          />
          <Route
            path="station/:equipmentCode"
            element={
              <RequireRole roles={['OPERATOR', 'ADMIN']}>
                <StationPage />
              </RequireRole>
            }
          />
          <Route path="lots" element={<LotListPage />} />
          <Route path="lots/:lotId" element={<LotDetailPage />} />
          <Route path="carriers" element={<CarrierListPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Route>
    </Routes>
  )
}
