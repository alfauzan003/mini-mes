import { Navigate, Route, Routes } from 'react-router'
import { LoginPage } from '@/features/auth/LoginPage'
import { CarrierListPage } from '@/features/carriers/CarrierListPage'
import { DashboardPage } from '@/features/dashboard/DashboardPage'
import { EquipmentDetailPage } from '@/features/equipment/EquipmentDetailPage'
import { EquipmentListPage } from '@/features/equipment/EquipmentListPage'
import { LotDetailPage } from '@/features/lots/LotDetailPage'
import { LotListPage } from '@/features/lots/LotListPage'
import { InspectPage } from '@/features/quality/InspectPage'
import { QualityPage } from '@/features/quality/QualityPage'
import { SpecsPage } from '@/features/quality/SpecsPage'
import { StationPage } from '@/features/operator/StationPage'
import { StationPickerPage } from '@/features/operator/StationPickerPage'
import { WorkOrderDetailPage } from '@/features/work-orders/WorkOrderDetailPage'
import { WorkOrderListPage } from '@/features/work-orders/WorkOrderListPage'
import { useAuth } from '@/shared/auth/AuthContext'
import { RequireAuth, RequireRole } from '@/shared/auth/RequireRole'
import { AppLayout } from '@/shared/layout/AppLayout'

function HomeRedirect() {
  const { user } = useAuth()
  const home = user?.role === 'OPERATOR' ? '/station' : user?.role === 'QC' ? '/quality' : '/dashboard'
  return <Navigate to={home} replace />
}

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<HomeRedirect />} />
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="equipment" element={<EquipmentListPage />} />
          <Route path="equipment/:code" element={<EquipmentDetailPage />} />
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
          <Route path="quality" element={<QualityPage />} />
          <Route
            path="quality/inspect/:lotId"
            element={
              <RequireRole roles={['QC', 'ADMIN']}>
                <InspectPage />
              </RequireRole>
            }
          />
          <Route path="quality/specs" element={<SpecsPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Route>
    </Routes>
  )
}
